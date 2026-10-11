import axios from 'axios';
import { IMetrics } from 'src/components/models';
import { isSessionExpired } from './session';
import { notify } from './notify';
export function notifyApiError(error: unknown): void {
  if (!isSessionExpired(error)) notify('negative', apiError(error));
}
export function displayServiceText(value?: string | null): string {
  return (value ?? '').replace(/\bIAML\b/g, 'DrIA');
}
export function apiError(error: unknown): string {
  if (axios.isAxiosError(error)) {
    const data = error.response?.data;
    const errors = Array.isArray(data?.errors) ? data.errors
      : data?.errors && typeof data.errors === 'object' ? Object.values(data.errors).flat() : [];
    return displayServiceText(String(data?.error ?? data?.message ?? data?.detail ?? (errors.length ? errors.join(' ; ') : error.message)));
  }
  return displayServiceText(error instanceof Error ? error.message : String(error));
}
export function isTemporaryApiError(error: unknown): boolean {
  if (!axios.isAxiosError(error) || error.code === 'ERR_CANCELED') return false;
  const status = error.response?.status;
  return status === undefined || status >= 500 || status === 408 || status === 429;
}
export function metricKey(metrics: IMetrics): string {
  return metrics.objective?.key ?? metrics.main_metric ?? metrics.main_metrics ?? '';
}
export function metricScore(metrics: IMetrics): number | null {
  const value = (metrics.metrics_test ?? metrics.metrics_train)?.[metricKey(metrics)];
  return typeof value === 'number' && Number.isFinite(value) ? value : null;
}
const scoreNumberFormat = new Intl.NumberFormat('en-US', { maximumFractionDigits: 4 });
export function formatScore(value: unknown): string {
  if (typeof value !== 'number' || !Number.isFinite(value)) return 'Unavailable';
  const formatted = scoreNumberFormat.format(value);
  return formatted === '-0' ? '0' : formatted;
}
export function compareMetrics(a: IMetrics, b: IMetrics): number {
  // Comparisons only make sense for the same objective and evaluation source.
  if (metricKey(a) !== metricKey(b) || a.score_source !== b.score_source)
    return 0;
  const av = metricScore(a);
  const bv = metricScore(b);
  if (av === null)
    return bv === null ? 0 : 1;
  if (bv === null)
    return -1;
  const greater = a.objective?.greater_is_better ?? a.metrics_info?.[metricKey(a)]?.greater_is_better ?? true;
  return greater ? bv - av : av - bv;
}
export function jsonLabel(value: any): string {
  return typeof value === 'string' ? value : JSON.stringify(value);
}
export function inferCsvColumnValues(values: string[]): (string | number | boolean)[] {
  const numeric = /^[+-]?(?:\d+\.?\d*|\.\d+)(?:e[+-]?\d+)?$/i;
  if (values.length && values.every((value) => numeric.test(value.trim()) && Number.isFinite(Number(value)))) {
    return values.map(Number);
  }
  if (values.length && values.every((value) => /^(true|false)$/i.test(value))) {
    return values.map((value) => value.toLowerCase() === 'true');
  }
  // CSV type inference applies to the column: true/censored stays textual.
  return values;
}
export function validateJsonScalar(value: string): true | string {
  if (!value.trim()) return true;
  try {
    const parsed = JSON.parse(value);
    if (typeof parsed === 'string' || typeof parsed === 'boolean'
      || (typeof parsed === 'number' && Number.isFinite(parsed))) return true;
    return 'Use a JSON string, a boolean or a finite number.';
  } catch {
    return 'Invalid JSON value. Enclose text values in quotation marks.';
  }
}
