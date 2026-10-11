import axios from 'axios';
import type { ICsvProject, IPrediction, IProject } from 'src/components/models';

export interface PredictionField {
  name: string;
  label: string;
  /** A hint from the preview, not the fitted model's input schema. */
  kind: 'numeric' | 'boolean' | 'text';
  /** Examples from the preview; this list is never exhaustive. */
  suggestions: string[];
}

export type PredictionScalar = string | number | boolean | null;

export interface SurvivalCurve {
  times: number[];
  values: number[];
}

export interface PredictionFailure {
  kind: 'invalid' | 'unsupported' | 'unavailable';
  message: string;
}

type JsonObject = Record<string, unknown>;
const NUMERIC = /^[+-]?(?:\d+\.?\d*|\.\d+)(?:e[+-]?\d+)?$/i;
const BOOLEAN = /^(true|false)$/i;
const MAX_SUGGESTIONS = 8;

function object(value: unknown): JsonObject | undefined {
  return value !== null && typeof value === 'object' && !Array.isArray(value)
    ? value as JsonObject : undefined;
}

/** A 400 can also indicate a broken model or an unsupported operation. */
export function describePredictionFailure(error: unknown): PredictionFailure {
  if (axios.isAxiosError(error) && error.response?.status === 400) {
    const response = error.response.data;
    const data = object(response);
    const details = (typeof response === 'string' ? [response] : [
      data?.error, data?.message, data?.detail,
      ...(Array.isArray(data?.errors) ? data.errors : []),
    ]).filter((value): value is string => typeof value === 'string').join(' ');
    if (/the model does not support\s+\w+/i.test(details))
      return { kind: 'unsupported', message: 'This prediction type is not available for this model.' };
    if (/prediction values cannot be converted|invalid prediction rows|(?:input(?:\s+x)?|array) contains (?:nan|infinity)|found unknown categories|previously unseen labels|could not convert string to (?:float|int)|invalid literal for (?:float|int)|cannot convert (?:float\s+)?nan to integer/i.test(details))
      return { kind: 'invalid', message: 'Invalid values' };
  }
  return { kind: 'unavailable', message: 'Prediction is temporarily unavailable. Please try again shortly.' };
}

function nonemptyText(value: unknown): value is string {
  return typeof value === 'string' && value.trim().length > 0;
}

function isMissing(value: unknown): boolean {
  return value === null || value === undefined || (typeof value === 'string' && !value.trim());
}

function isNumeric(value: unknown): boolean {
  return typeof value === 'number' ? Number.isFinite(value)
    : typeof value === 'string' && NUMERIC.test(value.trim()) && Number.isFinite(Number(value));
}

function isBoolean(value: unknown): boolean {
  return typeof value === 'boolean' || (typeof value === 'string' && BOOLEAN.test(value.trim()));
}

function scalarExample(value: unknown): string | undefined {
  if (typeof value === 'string') return nonemptyText(value) ? value : undefined;
  if (typeof value === 'boolean' || (typeof value === 'number' && Number.isFinite(value)))
    return String(value);
  return undefined;
}

/** Keep original column names and values: IAML performs the schema conversions. */
export function predictionFields(sample: ICsvProject, project: IProject): PredictionField[] {
  const projectData = object(project);
  const sampleData = object(sample);
  const excluded = new Set<string>();
  [projectData?.csvColumnToPredict,
    ...(projectData?.survivalModel ? [projectData.survivalModelDatasetTargetTime,
      projectData.survivalModeldatasetTargetTime] : [])].forEach((name) => {
    if (nonemptyText(name)) excluded.add(name);
  });
  [projectData?.rowDepedencies ? projectData.rowDepedenciesLinked : [],
    projectData?.datasetHasIdColumn ? projectData.datasetHasIdColumnList : []].forEach((names) => {
    if (Array.isArray(names)) names.forEach((name) => {
      if (nonemptyText(name)) excluded.add(name);
    });
  });
  const columns = Array.isArray(sampleData?.columns) ? sampleData.columns : [];
  const rows = Array.isArray(sampleData?.rows) ? sampleData.rows : [];
  const names = new Set<string>();
  const fields: PredictionField[] = [];
  columns.forEach((entry) => {
    const column = object(entry);
    if (!column || !nonemptyText(column.name)) return;
    const name = column.name;
    const label = nonemptyText(column.label) ? column.label : name;
    if (column.isColToPredict || column.isGroupColumn || column.isIdColumn
      || excluded.has(name) || excluded.has(label) || names.has(name)) return;
    names.add(name);
    const values = rows.map((row) => object(row)?.[name]).filter((value) => !isMissing(value));
    const kind = values.length > 0 && values.every(isBoolean) ? 'boolean'
      : values.length > 0 && values.every(isNumeric) ? 'numeric' : 'text';
    const suggestions = new Set<string>();
    values.forEach((value) => {
      const example = scalarExample(value);
      if (example !== undefined && suggestions.size < MAX_SUGGESTIONS) suggestions.add(example);
    });
    fields.push({ name, label, kind, suggestions: [...suggestions] });
  });
  return fields;
}

function predictionResult(response: IPrediction, expectedMethod: string): unknown {
  const data = object(response);
  if (!data || data.status !== 'ok')
    throw new Error('The service did not confirm that this prediction was calculated.');
  if (data.method !== expectedMethod)
    throw new Error('The service response does not match the requested prediction type.');
  return data.prediction;
}

/** A single prediction may be zero, false or null; arrays and objects are not scalars. */
export function parseSinglePrediction(response: IPrediction, expectedMethod = 'predict'): PredictionScalar {
  const result = predictionResult(response, expectedMethod);
  if (!Array.isArray(result) || result.length !== 1)
    throw new Error('The service must return a single prediction for this observation.');
  const value: unknown = result[0];
  if (value === null || typeof value === 'string' || typeof value === 'boolean') return value;
  if (typeof value === 'number' && Number.isFinite(value)) return value;
  throw new Error('The returned result cannot be displayed as a single value.');
}

/** Use the model's own time grid; no new horizon is inferred from the preview. */
export function parseSurvivalCurve(response: IPrediction): SurvivalCurve {
  const result = predictionResult(response, 'predict_survival_function');
  if (!Array.isArray(result) || result.length !== 1)
    throw new Error('The service must return a single curve for this observation.');
  const curve = object(result[0]);
  const times = curve?.times;
  const values = curve?.values;
  if (!Array.isArray(times) || !Array.isArray(values) || times.length === 0 || times.length !== values.length)
    throw new Error('The returned curve does not contain matching times and probabilities.');
  if (!times.every((time: unknown, index: number) => typeof time === 'number' && Number.isFinite(time)
    && time >= 0 && (index === 0 || time > times[index - 1])))
    throw new Error('Curve times must be increasing, finite, non-negative numbers.');
  if (!values.every((value: unknown) => typeof value === 'number' && Number.isFinite(value) && value >= 0 && value <= 1))
    throw new Error('Curve probabilities must be between 0 and 1.');
  return { times: [...times], values: [...values] };
}

/** Read the last known step within the returned domain, without interpolation. */
export function survivalAt(curve: SurvivalCurve, time: number): number | undefined {
  if (!Number.isFinite(time) || !curve.times.length || time < curve.times[0]
    || time > curve.times[curve.times.length - 1]) return undefined;
  let left = 0;
  let right = curve.times.length - 1;
  while (left <= right) {
    const middle = Math.floor((left + right) / 2);
    if (curve.times[middle] <= time) left = middle + 1;
    else right = middle - 1;
  }
  return curve.values[right];
}
