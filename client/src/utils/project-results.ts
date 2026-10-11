import type { IBaselineMetrics, IMetrics, IModel, IProcess, IRecipe } from 'src/components/models';
import { recipeEntries } from './pipeline-editor';
import { displayServiceName } from './service-label';

export interface ProjectResult {
  model: IModel;
  process: IProcess;
  selectionLabel: string;
  selectedByIaml: boolean;
}

export interface ProjectScore {
  key: string;
  name: string;
  value: number | null;
  source: 'test' | 'cross_validation' | 'in_sample' | 'unknown';
  label: string;
  greaterIsBetter: boolean | undefined;
}

export interface BaselineComparison {
  comparable: boolean;
  reason: string;
  modelScore?: number;
  baselineScore?: number;
  improvement?: number;
  key?: string;
  name?: string;
}

type JsonObject = Record<string, unknown>;

function object(value: unknown): JsonObject | undefined {
  return value !== null && typeof value === 'object' && !Array.isArray(value)
    ? value as JsonObject : undefined;
}

function text(value: unknown): string {
  return typeof value === 'string' && value.trim() ? value : '';
}

function finite(value: unknown): number | null {
  return typeof value === 'number' && Number.isFinite(value) ? value : null;
}

function normalize(value: unknown): string {
  return text(value).trim().replace(/\s+/g, ' ').toLocaleLowerCase('en-US');
}

function objectiveKey(metrics: unknown): string {
  const data = object(metrics);
  return text(object(data?.objective)?.key) || text(data?.main_metric) || text(data?.main_metrics);
}

function metricInfo(metrics: unknown, key: string): JsonObject | undefined {
  return object(object(object(metrics)?.metrics_info)?.[key]);
}

function direction(metrics: unknown, key: string): boolean | undefined {
  const data = object(metrics);
  const objective = object(data?.objective);
  const explicit = objective?.key === key ? objective.greater_is_better : undefined;
  const metadata = metricInfo(metrics, key)?.greater_is_better;
  // A conflicting definition does not justify assuming that a larger score wins.
  if (typeof explicit === 'boolean' && typeof metadata === 'boolean' && explicit !== metadata)
    return undefined;
  return typeof explicit === 'boolean' ? explicit : typeof metadata === 'boolean' ? metadata : undefined;
}

/** Compare the metric implementation and fixed values, rather than editor metadata. */
export function metricDefinition(recipe: IRecipe | undefined, key: string): string | undefined {
  if (!recipe || !key) return undefined;
  const nodes: IRecipe[] = [];
  function visit(node: IRecipe) {
    if (node.component) nodes.push(node);
    recipeEntries(node, []).filter((entry) => !entry.removed).forEach((entry) => visit(entry.node));
  }
  visit(recipe);
  const aliased = nodes.filter((node) => node.alias === key);
  // Native keys use snake_case while recipe components use class names.
  const identifier = (value: string) => value.replace(/_/g, '').toLowerCase();
  const matches = aliased.length ? aliased : nodes.filter((node) => !node.alias
    && (node.component === key || identifier(node.component!.replace(/Metric$/, '')) === identifier(key)));
  if (matches.length !== 1) return undefined;
  const node = matches[0];
  const params: JsonObject = {};
  for (const [name, parameter] of Object.entries(node.params ?? {})) {
    const descriptor = object(parameter);
    if (descriptor?.type === 'const' || descriptor?.type === 'parameter') {
      // A search domain is not evidence of the value used during evaluation.
      if (!Object.prototype.hasOwnProperty.call(descriptor, 'value')
        || (descriptor.type === 'parameter' && (descriptor.fixed !== true || descriptor.optimizable === true)))
        return undefined;
      params[name] = descriptor.value;
    } else if (['int', 'float', 'categorical'].includes(text(descriptor?.type))) {
      return undefined;
    } else {
      params[name] = parameter;
    }
  }
  function stable(value: unknown): unknown {
    if (Array.isArray(value)) return value.map(stable);
    const data = object(value);
    return data ? Object.fromEntries(Object.keys(data).sort().map((name) => [name, stable(data[name])])) : value;
  }
  return JSON.stringify(stable({ component: node.component, params }));
}

/** Read exactly one evaluation source. A missing test score never falls back to CV. */
export function scoreForMetrics(metrics: IMetrics, source: 'test' | 'validation' = 'validation'): ProjectScore {
  const data = object(metrics);
  const key = objectiveKey(metrics);
  const info = metricInfo(metrics, key);
  const evaluationSource = source === 'test' ? 'test'
    : data?.score_source === 'cross_validation' ? 'cross_validation'
      : data?.score_source === 'in_sample' ? 'in_sample' : 'unknown';
  const scores = object(source === 'test' ? data?.metrics_test : data?.metrics_train);
  return {
    key,
    name: displayServiceName(text(info?.name) || key) || 'Metric not specified',
    value: key ? finite(scores?.[key]) : null,
    source: evaluationSource,
    label: {
      test: 'Holdout test set',
      cross_validation: 'Cross-validation',
      in_sample: 'Training data',
      unknown: 'Internal evaluation (source not specified)',
    }[evaluationSource],
    greaterIsBetter: direction(metrics, key),
  };
}

function processModels(process: IProcess): IModel[] {
  const models = object(process)?.models;
  return Array.isArray(models) ? models.filter((model): model is IModel => {
    const value = object(model);
    return Boolean(value && typeof value.id === 'number' && Number.isInteger(value.id)
      && value.id >= 0 && object(value.metrics));
  }) : [];
}

function snapshot(process: IProcess): JsonObject | undefined {
  return object(object(process)?.statusSnapshot);
}

/** IAML's chosen_candidate is a remote rid: it matches metrics.id, not model.id. */
export function resultForProcess(process: IProcess): ProjectResult | undefined {
  const models = processModels(process);
  if (!models.length) return undefined;
  const data = object(process);
  const state = snapshot(process);
  const remoteId = state?.chosen_candidate !== undefined
    ? state.chosen_candidate : data?.chosenCandidate;
  const conflictingSelection = state?.chosen_candidate !== undefined && data?.chosenCandidate !== undefined
    && state.chosen_candidate !== data.chosenCandidate;
  if (!conflictingSelection && typeof remoteId === 'number' && Number.isInteger(remoteId) && remoteId >= 0) {
    const matches = models.filter((model) => object(model.metrics)?.id === remoteId);
    if (matches.length === 1) {
      return { model: matches[0], process, selectionLabel: 'Model selected by DrIA', selectedByIaml: true };
    }
  }

  const readings = models.map((model) => ({ model, score: scoreForMetrics(model.metrics) }));
  const first = readings[0].score;
  const firstInfo = metricInfo(models[0].metrics, first.key);
  const comparable = Boolean(first.key) && first.greaterIsBetter !== undefined
    && readings.every(({ model, score }) => {
      const info = metricInfo(model.metrics, score.key);
      return score.key === first.key && score.source === 'cross_validation'
        && score.greaterIsBetter === first.greaterIsBetter
        && normalize(info?.name) === normalize(firstInfo?.name)
        && normalize(info?.desc) === normalize(firstInfo?.desc);
    });
  if (models.length > 1 && comparable) {
    const available = readings.filter((reading): reading is typeof reading & { score: ProjectScore & { value: number } } => reading.score.value !== null);
    available.sort((a, b) => first.greaterIsBetter
      ? b.score.value - a.score.value : a.score.value - b.score.value);
    if (available.length) {
      return {
        model: available[0].model, process,
        selectionLabel: 'Best available validation score', selectedByIaml: false,
      };
    }
  }
  return {
    model: models[0], process,
    selectionLabel: models.length === 1 ? 'Available model' : 'First available model',
    selectedByIaml: false,
  };
}

/** Prefer the newest available run, rather than ranking different study objectives. */
export function latestProjectResult(processes: IProcess[]): ProjectResult | undefined {
  if (!Array.isArray(processes)) return undefined;
  const dateValue = (process: IProcess): number => {
    const value = object(process)?.date;
    const date = value instanceof Date ? value.getTime() : typeof value === 'string' ? Date.parse(value) : NaN;
    return Number.isFinite(date) ? date : -Infinity;
  };
  const available = processes.filter((process) => {
    const status = text(object(process)?.status);
    return object(process) && !['QUEUED', 'RUNNING'].includes(status)
      && snapshot(process)?.results_persisted !== false;
  }).slice().sort((a, b) => {
    const aDate = dateValue(a);
    const bDate = dateValue(b);
    if (aDate !== bDate) return aDate > bDate ? -1 : 1;
    return (finite(object(b)?.id) ?? 0) - (finite(object(a)?.id) ?? 0);
  });
  for (const process of available) {
    const result = resultForProcess(process);
    if (result) return result;
  }
  return undefined;
}

/** Baseline in-sample scores cannot be compared to CV; use matching test definitions. */
export function baselineComparison(model: IModel, baseline?: IBaselineMetrics): BaselineComparison {
  const unavailable = (reason: string): BaselineComparison => ({ comparable: false, reason });
  const reference = object(baseline);
  if (!reference) return unavailable('The baseline model is not available yet.');
  const status = text(object(reference.status)?.current);
  if (['QUEUED', 'RUNNING'].includes(status)) return unavailable('The baseline model is being trained.');
  const metrics = object(model)?.metrics as IMetrics;
  const modelScore = scoreForMetrics(metrics, 'test');
  if (modelScore.value === null)
    return unavailable('No holdout test score is available for this model. Internal scores for the baseline and optimized model cannot be compared.');
  const candidates = Array.isArray(reference.metrics) ? reference.metrics.filter((value) => object(value)) as IMetrics[] : [];
  const modelInfo = metricInfo(metrics, modelScore.key);
  const name = normalize(modelInfo?.name);
  const description = normalize(modelInfo?.desc);
  if (!name || !description || modelScore.greaterIsBetter === undefined)
    return unavailable('The metric definition or optimization direction does not allow a reliable comparison with the baseline.');
  const matching = candidates.filter((candidate) => {
    const reading = scoreForMetrics(candidate, 'test');
    const info = metricInfo(candidate, reading.key);
    return reading.key === modelScore.key && normalize(info?.name) === name
      && normalize(info?.desc) === description && reading.greaterIsBetter === modelScore.greaterIsBetter;
  });
  if (matching.length !== 1)
    return unavailable('The baseline metric cannot be reliably matched to the metric used for this model.');
  const baselineScore = scoreForMetrics(matching[0], 'test');
  if (baselineScore.value === null) return unavailable('No holdout test score is available for the baseline model.');
  const improvement = modelScore.greaterIsBetter
    ? modelScore.value - baselineScore.value : baselineScore.value - modelScore.value;
  if (!Number.isFinite(improvement)) return unavailable('The difference between scores cannot be calculated.');
  return {
    comparable: true,
    reason: 'The same metric is evaluated on the holdout test set. A positive difference favors the optimized model.',
    modelScore: modelScore.value, baselineScore: baselineScore.value, improvement,
    key: modelScore.key, name: modelScore.name,
  };
}

export function processState(process: IProcess): {
  label: string;
  tone: 'neutral' | 'running' | 'success' | 'error';
  pendingResults: boolean;
  error: string;
} {
  const data = object(process);
  const state = snapshot(process);
  const status = text(data?.status) || text(object(state?.status)?.current);
  const completed = ['FINISHED', 'CACHED'].includes(status);
  const pendingResults = completed && (state?.results_persisted === false
    || (state?.results_persisted !== true && !processModels(process).length));
  const error = [...new Set([
    text(data?.error) || text(state?.error),
    text(data?.cacheError) || text(state?.cache_error),
    text(state?.availability_error),
  ].filter(Boolean))].join('\n\n');
  if (pendingResults) return { label: 'Retrieving results', tone: 'running', pendingResults, error };
  const presentation: Record<string, { label: string; tone: 'neutral' | 'running' | 'success' | 'error' }> = {
    QUEUED: { label: 'Queued', tone: 'running' },
    RUNNING: { label: 'Search in progress', tone: 'running' },
    FINISHED: { label: 'Finished', tone: 'success' },
    CACHED: { label: 'Results restored', tone: 'success' },
    CANCELED: { label: 'Canceled', tone: 'neutral' },
    CRASHED: { label: 'Training failed', tone: 'error' },
    TIMEOUT: { label: 'Time limit reached', tone: 'error' },
  };
  return { ...(presentation[status] ?? { label: 'Status not specified', tone: 'neutral' }), pendingResults, error };
}

export function formatBudget(seconds: unknown): string {
  if (typeof seconds !== 'number' || !Number.isFinite(seconds) || seconds < 0) return 'Not specified';
  const total = Math.round(seconds);
  const hours = Math.floor(total / 3600);
  const minutes = Math.floor((total % 3600) / 60);
  const remainder = total % 60;
  return [hours ? `${hours} h` : '', minutes ? `${minutes} min` : '', remainder || !total ? `${remainder} s` : '']
    .filter(Boolean).join(' ');
}
