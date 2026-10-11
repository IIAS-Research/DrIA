import { IRecipe, IStudy } from 'src/components/models';

export type PredictionKind = 'category' | 'quantity' | 'survival';
export type StudyPriority = 'recommended' | 'recall' | 'precision' | 'f1' | 'mae' | 'mse';
export type TargetLabel = string | number | boolean;

export interface GuidedStudySettings {
  predictionKind: PredictionKind;
  priority: StudyPriority;
  positiveLabel?: TargetLabel;
  searchDuration: number;
}

export interface PredictionProfile {
  type: string;
  distinctValues: TargetLabel[];
  distinctValuesTruncated: boolean;
  populatedCount: number;
}

/**
 * Mirror PyIAML's target inference on a fully read column, without changing CSV
 * values. IAML repeats inference on the TRAIN partition after a split: its
 * distinct-value ratio may differ from the complete file's ratio.
 * A truncated catalogue only supplies a lower bound on distinct values.
 */
export function inferPredictionKind(profile: PredictionProfile): 'category' | 'quantity' | undefined {
  if (profile.populatedCount <= 0 || !profile.distinctValues.length)
    return undefined;
  if (profile.type === 'text' || profile.type === 'boolean')
    return 'category';
  if (profile.type !== 'numeric' || profile.distinctValues.some((value) => typeof value !== 'number' || !Number.isFinite(value)))
    return undefined;

  const values = profile.distinctValues as number[];
  const uniqueCount = new Set(values).size;
  // PyIAML treats numeric cardinality above either strict threshold as
  // continuous; sklearn also treats non-integral numeric labels as continuous.
  if (uniqueCount > 10 || uniqueCount > profile.populatedCount * 0.2 || values.some((value) => !Number.isInteger(value)))
    return 'quantity';
  return profile.distinctValuesTruncated ? undefined : 'category';
}

interface MetricMember {
  component: string;
  recipe: IRecipe;
  removed?: boolean;
  replacement?: boolean;
}

const positiveClassMetrics = new Set(['RecallMetric', 'PrecisionMetric', 'F1ScoreMetric']);

function objectiveComponent(settings: GuidedStudySettings): string {
  if (settings.predictionKind === 'survival') {
    if (settings.priority !== 'recommended')
      throw new Error('Choose the recommended metric for survival prediction.');
    return 'ConcordanceIndexMetric';
  }
  if (settings.predictionKind === 'quantity') {
    if (settings.priority === 'recommended' || settings.priority === 'mae')
      return 'MeanAbsoluteErrorMetric';
    if (settings.priority === 'mse')
      return 'MeanSquaredErrorMetric';
    throw new Error('Choose a metric suitable for predicting a quantity.');
  }
  const components: Partial<Record<StudyPriority, string>> = {
    recommended: 'BalancedAccuracyMetric',
    recall: 'RecallMetric',
    precision: 'PrecisionMetric',
    f1: 'F1ScoreMetric',
  };
  const component = components[settings.priority];
  if (!component)
    throw new Error('Choose a metric suitable for predicting a category.');
  return component;
}

function defaultStageDuration(duration: number): number {
  return Math.min(Math.max(900, Math.floor(duration / 5)), duration);
}

function setPositiveClass(recipe: IRecipe, label: TargetLabel): void {
  recipe.params ??= {};
  const parameter = recipe.params.pos_label;
  // Analysis parameters are fixed. Keep the canonical declaration returned by
  // IAML, including its metadata, when only the selected class changes.
  recipe.params.pos_label = parameter && typeof parameter === 'object'
    && (parameter.type === 'parameter' || parameter.type === 'const')
    ? { ...parameter, value: label }
    : { type: 'const', value: label };
}

/** Translate the guided answers without reducing the generated IAML study. */
export function buildGuidedStudy(base: IStudy, settings: GuidedStudySettings): IStudy {
  if (!Number.isInteger(settings.searchDuration) || settings.searchDuration < 60 || settings.searchDuration > 3600)
    throw new Error('Search duration must be between 60 and 3,600 seconds.');
  if (typeof settings.positiveLabel === 'number' && !Number.isFinite(settings.positiveLabel))
    throw new Error('The category to detect must be a finite value.');

  // The API contract is JSON. This also handles Vue reactive objects, unlike
  // structuredClone, and leaves the source configuration available for Back.
  const study: IStudy = JSON.parse(JSON.stringify(base));
  const component = objectiveComponent(settings);
  const metrics = study.metrics ?? {
    type: 'collection', kind: 'metrics', defaults: true, family: 'metrics', children: [],
  };
  study.metrics = metrics;
  metrics.children ??= [];
  const members: MetricMember[] = metrics.members ?? [];
  const useDefaults = !metrics.resolved && (metrics.defaults ?? metrics.family === 'metrics');
  if (useDefaults && settings.predictionKind === 'category' && settings.positiveLabel !== undefined) {
    for (const metric of positiveClassMetrics) {
      if (!members.some((member) => member.component === metric) && !metrics.exclude?.includes(metric))
        members.push({ component: metric, recipe: { type: 'use', component: metric }, removed: false, replacement: false });
    }
    metrics.members = members;
  }

  // An objective can live in explicit children or in a canonical family member.
  // Editing that slot avoids duplicating a metric while keeping other scores.
  let objective = metrics.children.find((recipe) => recipe.type === 'use' && recipe.component === component);
  if (!objective) {
    const member = members.find((candidate) => candidate.recipe.type === 'use' && candidate.recipe.component === component);
    if (member) {
      member.removed = false;
      objective = member.recipe;
    }
  }
  if (!objective) {
    objective = { type: 'use', component };
    if (useDefaults) {
      members.push({ component, recipe: objective, removed: false, replacement: false });
      metrics.members = members;
    }
    else metrics.children.push(objective);
  }
  if (Array.isArray(metrics.exclude))
    metrics.exclude = metrics.exclude.filter((excluded: string) => excluded !== component);

  const recipes = [...metrics.children, ...members.filter((member) => !member.removed).map((member) => member.recipe)];
  if (!objective.alias) {
    const aliases = new Set(recipes.map((recipe) => recipe.alias).filter(Boolean));
    let alias = 'guided_objective';
    for (let suffix = 2; aliases.has(alias); suffix += 1)
      alias = `guided_objective_${suffix}`;
    objective.alias = alias;
  }
  if (settings.predictionKind === 'category' && settings.positiveLabel !== undefined) {
    recipes.filter((recipe) => positiveClassMetrics.has(recipe.component ?? ''))
      .forEach((recipe) => setPositiveClass(recipe, settings.positiveLabel as TargetLabel));
  }

  const options = study.options ?? {};
  study.options = options;
  const previousDuration = options.max_duration ?? 60;
  const previousStageDuration = options.max_stage_duration;
  options.max_duration = settings.searchDuration;
  options.main_metric = objective.alias;
  if (previousStageDuration === defaultStageDuration(previousDuration))
    options.max_stage_duration = defaultStageDuration(settings.searchDuration);
  else if (Number.isInteger(previousStageDuration) && previousStageDuration > 0)
    options.max_stage_duration = Math.min(previousStageDuration, settings.searchDuration);
  if (Number.isInteger(options.time_before_sample_use) && options.time_before_sample_use > settings.searchDuration)
    options.time_before_sample_use = settings.searchDuration;
  return study;
}
