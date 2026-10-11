import assert from 'node:assert/strict';
import test from 'node:test';
import type { IRecipe, IStudy } from '../src/components/models';
import { buildGuidedStudy, inferPredictionKind } from '../src/utils/project-study';

function baseStudy(): IStudy {
  return {
    schema_version: 1,
    pipeline: { type: 'choice', children: [{ type: 'use', component: 'minimal_predictor' }] },
    metrics: { type: 'collection', children: [] },
    statistics: { type: 'use', component: 'descriptive_statistics' },
    options: { max_duration: 300, max_stage_duration: 300, time_before_sample_use: 600 },
    fit_options: { retain_unknown_option: true },
  };
}

function chosenMetric(study: IStudy): IRecipe | undefined {
  return study.metrics?.children?.find((recipe) => recipe.alias === study.options?.main_metric);
}

test('the category choices optimize the promised metric', () => {
  for (const [priority, component] of [
    ['recommended', 'BalancedAccuracyMetric'],
    ['recall', 'RecallMetric'],
    ['precision', 'PrecisionMetric'],
    ['f1', 'F1ScoreMetric'],
  ] as const) {
    const study = buildGuidedStudy(baseStudy(), {
      predictionKind: 'category', priority, searchDuration: 300, positiveLabel: false,
    });
    assert.equal(chosenMetric(study)?.component, component);
    if (priority !== 'recommended') {
      assert.deepEqual(chosenMetric(study)?.params?.pos_label, { type: 'const', value: false });
    }
  }
});

test('quantity choices distinguish average errors from squared large errors', () => {
  const recommended = buildGuidedStudy(baseStudy(), {
    predictionKind: 'quantity', priority: 'recommended', searchDuration: 300,
  });
  const squared = buildGuidedStudy(baseStudy(), {
    predictionKind: 'quantity', priority: 'mse', searchDuration: 300,
  });
  assert.equal(chosenMetric(recommended)?.component, 'MeanAbsoluteErrorMetric');
  assert.equal(chosenMetric(squared)?.component, 'MeanSquaredErrorMetric');
});

test('survival uses concordance and refuses an incompatible error metric', () => {
  const study = buildGuidedStudy(baseStudy(), {
    predictionKind: 'survival', priority: 'recommended', searchDuration: 300,
  });
  assert.equal(chosenMetric(study)?.component, 'ConcordanceIndexMetric');
  assert.throws(() => buildGuidedStudy(baseStudy(), {
    predictionKind: 'survival', priority: 'mse', searchDuration: 300,
  }));
});

test('guidance preserves the pipeline and source configuration while adjusting budgets', () => {
  const base = baseStudy();
  const original = JSON.stringify(base);
  const study = buildGuidedStudy(base, {
    predictionKind: 'quantity', priority: 'recommended', searchDuration: 120,
  });
  assert.equal(JSON.stringify(base), original);
  assert.deepEqual(study.pipeline, base.pipeline);
  assert.deepEqual(study.statistics, base.statistics);
  assert.deepEqual(study.fit_options, base.fit_options);
  assert.equal(study.options?.max_duration, 120);
  assert.equal(study.options?.max_stage_duration, 120);
  assert.equal(study.options?.time_before_sample_use, 120);
});

test('editing an existing metric preserves its alias and parameter metadata', () => {
  const base = baseStudy();
  base.metrics!.children = [
    { type: 'use', component: 'RecallMetric', alias: 'existing_recall', params: {
      pos_label: { type: 'parameter', value: 'old', description: 'Selected class' },
    } },
    { type: 'use', component: 'BalancedAccuracyMetric', alias: 'other_score' },
  ];
  const study = buildGuidedStudy(base, {
    predictionKind: 'category', priority: 'recall', positiveLabel: 0, searchDuration: 300,
  });
  assert.equal(study.metrics?.children?.length, 2);
  assert.equal(study.options?.main_metric, 'existing_recall');
  assert.deepEqual(chosenMetric(study)?.params?.pos_label, {
    type: 'parameter', value: 0, description: 'Selected class',
  });
});

test('default metric families restore an excluded objective and preserve other members', () => {
  const base = baseStudy();
  base.metrics = {
    type: 'collection', family: 'metrics', defaults: true,
    exclude: ['RecallMetric', 'PrecisionMetric'],
    members: [
      { component: 'RecallMetric', removed: true, recipe: { type: 'use', component: 'RecallMetric', alias: 'recall' } },
      { component: 'BalancedAccuracyMetric', removed: false, recipe: { type: 'use', component: 'BalancedAccuracyMetric' } },
    ],
  };
  const study = buildGuidedStudy(base, {
    predictionKind: 'category', priority: 'recall', positiveLabel: true, searchDuration: 300,
  });
  assert.equal(study.options?.main_metric, 'recall');
  assert.deepEqual(study.metrics?.exclude, ['PrecisionMetric']);
  const recall = study.metrics?.members.find((member: { component: string }) => member.component === 'RecallMetric');
  assert.equal(recall.removed, false);
  assert.deepEqual(recall.recipe.params.pos_label, { type: 'const', value: true });
  assert.ok(study.metrics?.members.some((member: { component: string }) => member.component === 'BalancedAccuracyMetric'));
});

test('the guided objective avoids an alias already used by another score', () => {
  const base = baseStudy();
  base.metrics!.children = [{ type: 'use', component: 'R2ScoreMetric', alias: 'guided_objective' }];
  const study = buildGuidedStudy(base, {
    predictionKind: 'quantity', priority: 'recommended', searchDuration: 300,
  });
  assert.equal(study.options?.main_metric, 'guided_objective_2');
  assert.equal(chosenMetric(study)?.component, 'MeanAbsoluteErrorMetric');
});

test('invalid budgets and metric choices are rejected before sending the study', () => {
  for (const searchDuration of [59, 3601, 60.5, NaN]) {
    assert.throws(() => buildGuidedStudy(baseStudy(), {
      predictionKind: 'category', priority: 'recommended', searchDuration,
    }));
  }
  assert.throws(() => buildGuidedStudy(baseStudy(), {
    predictionKind: 'quantity', priority: 'recall', searchDuration: 300,
  }));
  assert.throws(() => buildGuidedStudy(baseStudy(), {
    predictionKind: 'category', priority: 'recall', searchDuration: 300, positiveLabel: Infinity,
  }));
});

test('target inference distinguishes numerical category labels from continuous measurements', () => {
  const numeric = { type: 'numeric', populatedCount: 100, distinctValuesTruncated: false };
  assert.equal(inferPredictionKind({ ...numeric, distinctValues: [0, 1] }), 'category');
  assert.equal(inferPredictionKind({ ...numeric, distinctValues: [0.1, 0.2] }), 'quantity');
  assert.equal(inferPredictionKind({ ...numeric, distinctValues: Array.from({ length: 11 }, (_, index) => index) }), 'quantity');
  assert.equal(inferPredictionKind({ ...numeric, populatedCount: 5, distinctValues: [0, 1] }), 'quantity');
});

test('an incomplete or invalid target catalogue never implies a categorical target', () => {
  assert.equal(inferPredictionKind({
    type: 'numeric', populatedCount: 100, distinctValues: [0, 1], distinctValuesTruncated: true,
  }), undefined);
  assert.equal(inferPredictionKind({
    type: 'numeric', populatedCount: 100, distinctValues: [NaN], distinctValuesTruncated: false,
  }), undefined);
  assert.equal(inferPredictionKind({
    type: 'text', populatedCount: 0, distinctValues: [], distinctValuesTruncated: false,
  }), undefined);
});
