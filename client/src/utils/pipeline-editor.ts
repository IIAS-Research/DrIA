import type { IPipeline, IRecipe } from 'src/components/models';
import { displayServiceName } from './service-label';

export type RecipePath = (string | number)[];

export interface CatalogueComponent {
  id: string;
  kind: string;
  name?: string;
  description?: string;
  parameters?: Record<string, any>;
  families?: string[];
  available?: boolean;
  capabilities?: string[];
}

export type CatalogueSource = CatalogueComponent[] | { components?: CatalogueComponent[] };

export interface RecipeEntry {
  node: IRecipe;
  path: RecipePath;
  member?: any;
  index: number;
  source: 'children' | 'recipe' | 'members';
  removed: boolean;
}

const fields = ['pipeline', 'metrics', 'statistics', 'explanations'] as const;
const recipeTypes = new Set(['use', 'sequence', 'choice', 'adaptive', 'optional', 'family', 'collection']);

function plainObject(value: unknown): value is Record<string, any> {
  if (value === null || typeof value !== 'object' || Array.isArray(value)) return false;
  const prototype = Object.getPrototypeOf(value);
  return prototype === Object.prototype || prototype === null;
}

/** Guard JSON assignment before any editor traversal or reactive mutation. */
export function isValidStudyShape(value: unknown): value is IPipeline {
  try {
    if (!plainObject(value) || value.schema_version !== 1) return false;
    for (const field of ['options', 'fit_options']) {
      if (field in value && !plainObject(value[field])) return false;
    }
    // Unknown metadata is retained, but it must still be finite, acyclic JSON.
    const ancestors = new Set<object>();
    function jsonValue(item: unknown, depth = 0): boolean {
      if (item === null || typeof item === 'string' || typeof item === 'boolean') return true;
      if (typeof item === 'number') return Number.isFinite(item);
      if (typeof item !== 'object' || depth > 96 || ancestors.has(item)) return false;
      if (!Array.isArray(item) && !plainObject(item)) return false;
      ancestors.add(item);
      const valid = Object.values(item).every((child) => jsonValue(child, depth + 1));
      ancestors.delete(item);
      return valid;
    }
    if (!jsonValue(value)) return false;
    let nodes = 0;
    function recipe(node: unknown, depth = 1): boolean {
      if (!plainObject(node) || depth > 32 || ++nodes > 1024 || !recipeTypes.has(node.type)) return false;
      for (const key of ['alias', 'component', 'family', 'kind']) {
        if (key in node && typeof node[key] !== 'string') return false;
      }
      if ('params' in node && !plainObject(node.params)) return false;
      if ('exclude' in node && (!Array.isArray(node.exclude) || node.exclude.some((id: unknown) => typeof id !== 'string'))) return false;
      if ('children' in node && (!Array.isArray(node.children) || !node.children.every((child: unknown) => recipe(child, depth + 1)))) return false;
      if ('recipe' in node && !recipe(node.recipe, depth + 1)) return false;
      if ('members' in node) {
        if (!Array.isArray(node.members)) return false;
        for (const member of node.members) {
          if (!plainObject(member) || typeof member.component !== 'string' || !recipe(member.recipe, depth + 1)) return false;
          for (const flag of ['removed', 'replacement']) {
            if (flag in member && typeof member[flag] !== 'boolean') return false;
          }
        }
      }
      return true;
    }
    return fields.every((field) => !(field in value) || recipe(value[field]));
  } catch {
    return false;
  }
}

const componentLabels: Record<string, string> = {
  ActSimpleImputer: 'Fill missing values',
  ActCategoricalImputer: 'Fill missing categories',
  ActKNNImputer: 'Nearest-neighbor imputation',
  ActMICEForestImputer: 'Forest imputation',
  ActMeanColumn: 'Mean imputation',
  ActCoerceNumericStrings: 'Detect numbers in text',
  ActDateConverter: 'Detect dates',
  ActDropDuplicateRows: 'Remove duplicate rows',
  ActDropHighMissingColumns: 'Remove features with too many missing values',
  ActDropIdLikeColumns: 'Remove likely identifiers',
  ActDropHighCardinalityCategorical: 'Handle high-cardinality categories',
  ActDropCategoricalColumn: 'Remove categorical features',
  ActDropNumericalColumn: 'Remove numeric features',
  ActDropTextualColumn: 'Remove text features',
  ActDropDateColumn: 'Remove dates',
  ActOrdinalEncoder: 'Ordinal category encoding',
  ActOnehot: 'One feature per category',
  ActFrequencyEncoder: 'Frequency encoding',
  ActTargetEncoder: 'Target encoding',
  ActRareCategoryGrouper: 'Group rare categories',
  ActTextNormalizer: 'Clean text',
  ActTrimSpaces: 'Trim extra spaces',
  ActNormalizeColumnNames: 'Standardize feature names',
  ActSentinelToNaN: 'Detect missing value codes',
  ActMissingIndicator: 'Flag missing values',
  ActMissingCountFeature: 'Count missing values',
  ActSplitDate: 'Extract date components',
  ActStandardScaler: 'Standard scaling',
  ActMinMaxScaler: 'Scale values between 0 and 1',
  ActMaxAbsScaler: 'Scale by maximum absolute value',
  ActRobustScaler: 'Robust scaling for extreme values',
  ActUnitNormScaler: 'Normalize each sample',
  ActPowerTransformer: 'Power transformation',
  ActQuantileTransformer: 'Quantile transformation',
  ActLogTransformer: 'Log transformation',
  ActRemoveHighCorrelatedColumn: 'Remove highly correlated features',
  ActRemoveLowVarianceColumn: 'Remove low-variance features',
  ActSelectPercentile: 'Select a percentage of features',
  ActSelectKBest: 'Select top features',
  ActSelectFromModel: 'Select features using a model',
  ActRFE: 'Recursive feature selection',
  ActPermutationImportanceSelector: 'Select by feature importance',
  ActPCA: 'Principal component analysis (PCA)',
  ActSMOTE: 'Create minority-class samples (SMOTE)',
  ActRandomOverSampling: 'Oversample the minority class',
  ActRandomUnderSampler: 'Undersample the majority class',
  ActRandomForest: 'Random forest · classification',
  ActRandomForestRegressor: 'Random forest · regression',
  ActLogisticRegression: 'Logistic regression',
  ActLinearRegression: 'Linear regression',
  ActDecisionTreeClassifier: 'Decision tree · classification',
  ActDecisionTreeRegressor: 'Decision tree · regression',
  ActCatBoost: 'CatBoost · classification',
  ActCatBoostRegressor: 'CatBoost · regression',
  ActKNN: 'Nearest neighbors · classification',
  ActKNNRegressor: 'Nearest neighbors · regression',
  ActSVMSVC: 'Support vector machine · classification',
  ActSVMSVR: 'Support vector machine · regression',
  AccuracyMetric: 'Accuracy',
  BalancedAccuracyMetric: 'Balanced accuracy',
  RecallMetric: 'Recall',
  PrecisionMetric: 'Precision',
  F1ScoreMetric: 'F1 score',
  SpecificityMetric: 'Specificity',
  RocAucMetric: 'Area under the ROC curve',
  MeanAbsoluteErrorMetric: 'Mean absolute error',
  MeanSquaredErrorMetric: 'Mean squared error',
  R2ScoreMetric: 'Coefficient of determination (R²)',
  ConcordanceIndexMetric: 'Concordance index',
  KernelSHAP: 'Feature contributions (SHAP)',
  ConfusionMatrixPlot: 'Confusion matrix',
  ClassificationReportPlot: 'Classification report',
  ROCAUCPlot: 'ROC curve',
  PrecisionRecallCurvePlot: 'Precision–recall curve',
  ResidualsPlot: 'Prediction residuals',
  PredictionErrorPlot: 'Prediction errors',
};

function catalogueComponents(source?: CatalogueSource): CatalogueComponent[] {
  return Array.isArray(source) ? source : source?.components ?? [];
}

/** The API is JSON; this also copies Vue proxies without touching the source. */
export function cloneJson<T>(value: T): T {
  return value === undefined ? value : JSON.parse(JSON.stringify(value));
}

export function recipeAt(study: IPipeline, path: RecipePath): IRecipe | undefined {
  let current: any = study;
  for (const key of path) {
    if (current === null || typeof current !== 'object') return undefined;
    current = current[key];
  }
  return current && typeof current === 'object' && typeof current.type === 'string' ? current : undefined;
}

export function recipeLabel(node: IRecipe, catalogue: CatalogueComponent[] = []): string {
  if (typeof node.name === 'string' && node.name.trim()) return displayServiceName(node.name);
  // Group aliases/families are service identifiers, not a stable description
  // of their members. Only clean their presentation, without inferring a meaning.
  if (typeof node.alias === 'string' && node.alias.trim()) return displayServiceName(node.alias);
  if (typeof node.component === 'string' && node.component) {
    return displayServiceName(componentLabels[node.component]
      ?? (catalogue.find((component) => component.id === node.component)?.name?.trim() || node.component));
  }
  if (typeof node.family === 'string' && node.family.trim()) return displayServiceName(node.family);
  const labels: Record<string, string> = {
    sequence: 'Sequential steps', choice: 'Alternative paths', adaptive: 'Data-dependent steps',
    optional: 'Optional step', collection: 'Analyses', family: 'Component family', use: 'Component',
  };
  return labels[node.type] ?? displayServiceName(String(node.type ?? 'Step'));
}

export function recipeEntries(node: IRecipe, path: RecipePath): RecipeEntry[] {
  const entries: RecipeEntry[] = [];
  if (Array.isArray(node.children)) {
    node.children.forEach((child, index) => {
      if (child && typeof child === 'object') entries.push({
        node: child, path: [...path, 'children', index], index, source: 'children', removed: false,
      });
    });
  }
  if (node.recipe && typeof node.recipe === 'object') entries.push({
    node: node.recipe, path: [...path, 'recipe'], index: 0, source: 'recipe', removed: false,
  });
  if (Array.isArray(node.members)) {
    node.members.forEach((member: any, index: number) => {
      const recipe: IRecipe = member?.recipe ?? member;
      if (!recipe || typeof recipe !== 'object' || typeof recipe.type !== 'string') return;
      entries.push({
        node: recipe, path: [...path, 'members', index, ...(member.recipe ? ['recipe'] : [])],
        member, index, source: 'members',
        removed: member.removed === true || (!member.replacement
          && Array.isArray(node.exclude) && node.exclude.includes(member.component ?? recipe.component)),
      });
    });
  }
  return entries;
}

export function recipeSummary(node: IRecipe): string {
  const count = recipeEntries(node, []).filter((entry) => !entry.removed).length;
  const mode = node.type === 'family' ? node.mode ?? 'choice' : node.type;
  if (mode === 'sequence') return `${count} step${count === 1 ? '' : 's'} executed in order`;
  if (mode === 'choice') return `${count} alternative${count === 1 ? '' : 's'} to compare${node.optional ? ' · may be skipped' : ''}`;
  if (mode === 'adaptive') return `${count} data-dependent step${count === 1 ? '' : 's'}`;
  if (mode === 'optional') return 'DrIA may apply or skip this step';
  if (mode === 'collection') return `${count} enabled ${count === 1 ? 'analysis' : 'analyses'}`;
  const parameters = Object.keys(node.params ?? {}).length;
  return parameters ? `${parameters} configurable parameter${parameters === 1 ? '' : 's'}` : 'Default parameters';
}

/** Visits stored recipes, including disabled registry slots, without wrappers. */
export function walkRecipes(study: IPipeline, callback: (node: IRecipe, path: RecipePath) => void): void {
  const visited = new Set<IRecipe>();
  function visit(node: IRecipe, path: RecipePath) {
    if (!node || typeof node !== 'object' || visited.has(node)) return;
    visited.add(node);
    callback(node, path);
    recipeEntries(node, path).forEach((entry) => visit(entry.node, entry.path));
  }
  fields.forEach((field) => { if (study[field]) visit(study[field] as IRecipe, [field]); });
}

/** Keep the registry slot: removing it would allow DrIA to restore its default. */
export function setMemberEnabled(node: IRecipe, index: number, enabled: boolean): void {
  const member = node.members?.[index];
  if (!member || typeof member !== 'object') return;
  member.removed = !enabled;
  const component = member.component ?? member.recipe?.component;
  if (!component) return;
  const exclude: string[] = Array.isArray(node.exclude) ? node.exclude : [];
  if (enabled) {
    if (Array.isArray(node.exclude)) node.exclude = exclude.filter((id) => id !== component);
  } else if (!exclude.includes(component)) node.exclude = [...exclude, component];
}

export function newComponentRecipe(component: CatalogueComponent): IRecipe {
  return { type: 'use', kind: component.kind, component: component.id, params: cloneJson(component.parameters ?? {}) };
}

export function newFamilyRecipe(family: string, catalogue: CatalogueSource): IRecipe {
  const components = catalogueComponents(catalogue).filter((component) => component.kind === 'pipeline'
    && component.available !== false && component.families?.includes(family));
  return {
    type: 'family', kind: 'pipeline', family, resolved: false,
    mode: ['features_precleaning', 'cleaning', 'baseline_cleaning'].includes(family) ? 'adaptive' : 'choice',
    children: [], members: components.map((component) => ({
      component: component.id, recipe: newComponentRecipe(component), removed: false, replacement: false,
    })),
  };
}

/** The built-in start preset describes the original tree, so edits invalidate it. */
export function clearPreset(study: IPipeline): void {
  if (study.pipeline) delete study.pipeline.default_preset;
}

/** Immediate editing feedback; service validation remains authoritative. */
export function structuralCheck(study: IPipeline, catalogue?: CatalogueSource): string[] {
  if (!plainObject(study) || study.schema_version !== 1)
    return ['This study configuration is invalid.'];
  const errors = new Set<string>();
  const components = catalogueComponents(catalogue);
  const byId = new Map(components.map((component) => [component.id, component]));
  const activeNodes = new Set<IRecipe>();
  function activeEntries(node: IRecipe) {
    const entries = recipeEntries(node, []);
    // Unresolved groups without canonical members still expand the registry.
    // Explicit children do not replace that expansion.
    const automatic = !node.resolved
      && (node.type === 'family' || node.type === 'collection' && (node.defaults ?? !!node.family));
    if (automatic) {
      const slots = new Set(entries.filter((entry) => entry.source === 'members').map((entry) => entry.member?.component));
      components.filter((component) => node.type === 'family'
        ? component.kind === 'pipeline' && component.families?.includes(node.family ?? '')
        : component.kind === node.kind)
        .filter((component) => !slots.has(component.id))
        .forEach((component, index) => entries.push({
          node: newComponentRecipe(component), path: [], source: 'members', index,
          removed: Array.isArray(node.exclude) && node.exclude.includes(component.id),
        }));
    }
    return entries.filter((entry) => !entry.removed
      && (!entry.node.component || byId.get(entry.node.component)?.available !== false));
  }
  function checkActive(node: IRecipe) {
    if (!plainObject(node)) {
      errors.add('A step has an invalid configuration. Choose a component or group.');
      return;
    }
    if (activeNodes.has(node)) return;
    activeNodes.add(node);
    const label = recipeLabel(node, components);
    const active = activeEntries(node);
    const mode = node.type === 'family' ? node.mode ?? 'choice' : node.type;
    if (node.type === 'use' && (typeof node.component !== 'string' || !node.component.trim()))
      errors.add(`${label}: choose a component.`);
    if (node.type === 'optional' && active.length !== 1) errors.add(`${label}: choose exactly one optional step.`);
    if (mode === 'choice' && !active.length) errors.add(`${label}: enable at least one alternative.`);
    active.forEach((entry) => checkActive(entry.node));
  }
  if (!study.pipeline) errors.add('Add a pipeline to train.');
  else checkActive(study.pipeline);
  const implicitMetrics = study.metrics?.type === 'collection' && !study.metrics.resolved
    && !components.length && (!Array.isArray(study.metrics.members) || !study.metrics.members.length)
    && (study.metrics.defaults ?? !!study.metrics.family);
  if (!study.metrics || !activeEntries(study.metrics).length && study.metrics.type !== 'use' && !implicitMetrics)
    errors.add('Enable at least one metric to evaluate the model.');
  fields.filter((field) => field !== 'pipeline').forEach((field) => {
    if (study[field]) checkActive(study[field] as IRecipe);
  });
  fields.forEach((field) => {
    const aliases = new Set<string>();
    const visited = new Set<IRecipe>();
    function checkAlias(node: IRecipe) {
      if (!plainObject(node) || visited.has(node)) return;
      visited.add(node);
      const alias = typeof node.alias === 'string' ? node.alias.trim() : undefined;
      if (alias) {
        if (aliases.has(alias)) errors.add(`The name “${alias}” is used more than once. Give each step a unique name.`);
        aliases.add(alias);
      }
      activeEntries(node).forEach((entry) => checkAlias(entry.node));
    }
    if (study[field]) checkAlias(study[field] as IRecipe);
  });
  // Catalogue capabilities identify predictors without guessing from their names.
  // Restrict this check to known components; the service checks custom recipes.
  let allComponentsKnown = components.length > 0;
  if (study.pipeline) {
    const visited = new Set<IRecipe>();
    function known(node: IRecipe) {
      if (!plainObject(node) || visited.has(node)) {
        allComponentsKnown = false;
        return;
      }
      visited.add(node);
      if (node.type === 'use' && !byId.has(node.component ?? '')) allComponentsKnown = false;
      activeEntries(node).forEach((entry) => known(entry.node));
    }
    known(study.pipeline);
  }
  if (study.pipeline && allComponentsKnown) {
    type Shape = { count: number; terminal: boolean };
    function unique(shapes: Shape[]): Shape[] {
      return [...new Map(shapes.map((shape) => [`${shape.count}:${shape.terminal}`, shape])).values()];
    }
    function shapes(node: IRecipe): Shape[] {
      if (node.type === 'use') {
        const predictor = byId.get(node.component ?? '')?.capabilities?.includes('predict') === true;
        return [{ count: predictor ? 1 : 0, terminal: predictor }];
      }
      const mode = node.type === 'family' ? node.mode ?? 'choice' : node.type;
      const childShapes = activeEntries(node).map((entry) => shapes(entry.node));
      if (mode === 'choice' || mode === 'optional') {
        const alternatives = unique(childShapes.flat());
        if (node.optional || mode === 'optional') {
          if (alternatives.some((shape) => shape.count > 0))
            errors.add('A prediction model must remain required in its path.');
          alternatives.push({ count: 0, terminal: false });
        }
        return unique(alternatives);
      }
      let result: Shape[] = [{ count: 0, terminal: false }];
      for (const next of childShapes) {
        if (result.some((shape) => shape.terminal))
          errors.add('Place the model last: no step may follow it.');
        result = unique(result.flatMap((previous) => next.map((child) => ({
          count: Math.min(2, previous.count + child.count), terminal: child.terminal,
        }))));
      }
      return result;
    }
    const candidates = shapes(study.pipeline);
    if (!candidates.length || candidates.some((shape) => shape.count !== 1 || !shape.terminal))
      errors.add('Each path must end with exactly one prediction model.');
  }
  return [...errors];
}
