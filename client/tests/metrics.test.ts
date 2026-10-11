import assert from 'node:assert/strict';
import test from 'node:test';
import type { IMetrics } from '../src/components/models';
import { compareMetrics, formatScore, inferCsvColumnValues, metricScore } from '../src/utils/iaml';

function metrics(score: number | null, greaterIsBetter = true): IMetrics {
  return {
    id: 1,
    model_info: { name: 'Model', description: '', description_long: '' },
    objective: { key: 'criterion', greater_is_better: greaterIsBetter },
    score_source: 'cross_validation',
    metrics_info: {},
    metrics_train: { criterion: 0.99 },
    metrics_test: { criterion: score },
  };
}

test('scores round to at most four decimal places without scientific notation', () => {
  assert.equal(formatScore(0.123456), '0.1235');
  assert.equal(formatScore(0.25), '0.25');
  assert.equal(formatScore(3.064e-28), '0');
  assert.equal(formatScore(-3.064e-28), '0');
});

test('missing and non-finite scores are unavailable rather than shown as zero', () => {
  for (const value of [null, undefined, NaN, Infinity, -Infinity, '0.5']) {
    assert.equal(formatScore(value), 'Unavailable');
  }
});

test('a missing test score never falls back to the training score', () => {
  assert.equal(metricScore(metrics(null)), null);
  assert.equal(metricScore(metrics(NaN)), null);
  assert.equal(metricScore(metrics(0.72)), 0.72);
});

test('ranking places higher accuracy and lower error first', () => {
  assert.ok(compareMetrics(metrics(0.9), metrics(0.7)) < 0);
  assert.ok(compareMetrics(metrics(2, false), metrics(7, false)) < 0);
  assert.ok(compareMetrics(metrics(null), metrics(0.7)) > 0);
  assert.equal(compareMetrics(metrics(null), metrics(null)), 0);
});

test('scores from different criteria or evaluation sources cannot be ranked together', () => {
  const differentCriterion = metrics(0.9);
  differentCriterion.objective = { key: 'another_criterion', greater_is_better: true };
  const inSample = { ...metrics(0.9), score_source: 'in_sample' as const };
  assert.equal(compareMetrics(metrics(0.7), differentCriterion), 0);
  assert.equal(compareMetrics(metrics(0.7), inSample), 0);
});

test('metric metadata supplies direction when an explicit objective is absent', () => {
  const lower = metrics(2);
  const higher = metrics(7);
  for (const result of [lower, higher]) {
    delete result.objective;
    result.main_metric = 'criterion';
    result.metrics_info.criterion = { name: 'Error', desc: '', greater_is_better: false };
  }
  assert.ok(compareMetrics(lower, higher) < 0);
});

test('CSV inference converts whole columns and preserves mixed event labels', () => {
  assert.deepEqual(inferCsvColumnValues(['0', '1', '2e-2']), [0, 1, 0.02]);
  assert.deepEqual(inferCsvColumnValues(['true', 'FALSE']), [true, false]);
  assert.deepEqual(inferCsvColumnValues(['true', 'censored']), ['true', 'censored']);
  assert.deepEqual(inferCsvColumnValues(['NaN', '1']), ['NaN', '1']);
});
