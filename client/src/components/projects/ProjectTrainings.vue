<script setup lang="ts">
import { computed, nextTick, ref, watch } from 'vue';
import { useRoute } from 'vue-router';
import { exportFile } from 'quasar';
import { IProject, IProcess, IModel, IMetrics } from '../models';
import { formatToDDMMYYYHHmm } from 'src/utils/date';
import { displayServiceText, formatScore as displayScore, metricKey } from 'src/utils/iaml';
import { formatBudget, processState, resultForProcess, scoreForMetrics } from 'src/utils/project-results';
import { displayServiceName } from 'src/utils/service-label';

const props = defineProps<{ project: IProject; active: boolean }>();
const emit = defineEmits<{
  (e: 'configure', processId: number): void;
  (e: 'restart', processId: number): void;
}>();
const route = useRoute();
const openedId = ref<number | null>(null);
const inspectedIds = ref<Record<number, number | undefined>>({});
const baselineOpened = ref(false);
const baselineSection = ref<HTMLElement>();
const baselineDownloadFeedback = ref('');
const processes = computed(() => [...(props.project.processes ?? [])].sort((a, b) =>
  (new Date(b.date).getTime() || 0) - (new Date(a.date).getTime() || 0) || b.id - a.id));
const trainings = computed(() => processes.value.map((process) => {
  const result = resultForProcess(process);
  return {
    process, result, state: processState(process),
    candidates: (process.models ?? []).filter((model) => model.id !== result?.model.id),
  };
}));
const modelCount = computed(() => processes.value.reduce((total, process) => total + (process.models?.length ?? 0), 0));
const baselineMetrics = computed(() => props.project.metricsBaseline?.metrics ?? []);
const baselineStatus = computed(() => {
  const baseline = props.project.metricsBaseline;
  if (!baseline) return 'Baseline unavailable';
  const labels: Record<string, string> = {
    QUEUED: 'Queued', RUNNING: 'Computing', FINISHED: 'Computation complete',
    CACHED: 'Results retrieved', CANCELED: 'Computation canceled', CRASHED: 'Computation failed', TIMEOUT: 'Time limit reached',
  };
  return labels[baseline.status?.current ?? ''] ?? (baselineMetrics.value.length ? 'Results available' : 'No results available');
});

function direction(metrics: IMetrics): string {
  const score = scoreForMetrics(metrics, 'validation');
  return score.greaterIsBetter === undefined ? '' : score.greaterIsBetter ? 'Maximize' : 'Minimize';
}

function modelName(model: IModel, process: IProcess): string {
  return displayServiceName(model.metrics.model_info?.name) || 'Model ' + ((process.models?.indexOf(model) ?? 0) + 1);
}

function sourceLabel(metrics: IMetrics): string {
  return scoreForMetrics(metrics, 'validation').label;
}

function metricDirection(metrics: IMetrics, key: string): string {
  if (key === metricKey(metrics)) return direction(metrics);
  const greater = metrics.metrics_info?.[key]?.greater_is_better;
  return typeof greater === 'boolean' ? greater ? 'Maximize' : 'Minimize' : '';
}

function allMetrics(metrics: IMetrics): { key: string; name: string; validation: number | null; test: number | null; description?: string; direction: string }[] {
  const keys = [...new Set([
    ...Object.keys(metrics.metrics_info ?? {}),
    ...Object.keys(metrics.metrics_train ?? {}),
    ...Object.keys(metrics.metrics_test ?? {}),
  ])]
    .sort((a, b) => Number(b === metricKey(metrics)) - Number(a === metricKey(metrics)));
  return keys.map((key) => ({
    key,
    name: displayServiceName(metrics.metrics_info?.[key]?.name ?? key),
    validation: typeof metrics.metrics_train?.[key] === 'number' ? metrics.metrics_train[key] : null,
    test: typeof metrics.metrics_test?.[key] === 'number' ? metrics.metrics_test[key] : null,
    description: metrics.metrics_info?.[key]?.desc,
    direction: metricDirection(metrics, key),
  }));
}

function inspectedModel(process: IProcess): IModel | undefined {
  return process.models?.find((model) => model.id === inspectedIds.value[process.id]);
}

function inspect(process: IProcess, modelId: number) {
  inspectedIds.value[process.id] = inspectedIds.value[process.id] === modelId ? undefined : modelId;
}

function processMessages(process: IProcess): { title: string; content: string; kind: string }[] {
  const snapshot = process.statusSnapshot as Record<string, unknown> | null | undefined;
  const availabilityError = typeof snapshot?.availability_error === 'string' ? snapshot.availability_error : undefined;
  const errors = [...new Set([process.error, process.statusSnapshot?.error, availabilityError].filter(Boolean))];
  const cacheErrors = [...new Set([process.cacheError, process.statusSnapshot?.cache_error].filter(Boolean))];
  return [
    ...errors.map((content) => ({ title: 'DrIA service message', content: displayServiceText(String(content)), kind: 'error' })),
    ...cacheErrors.map((content) => ({ title: 'Saving results', content: displayServiceText(String(content)), kind: 'warning' })),
  ];
}

function hasContent(value: unknown): boolean {
  if (value === undefined || value === null || value === '') return false;
  return typeof value !== 'object' || Object.keys(value).length > 0;
}

function reports(metrics: IMetrics): { name: string; value: unknown }[] {
  return [
    { name: 'Metric report', value: metrics.metric_report },
    { name: 'Evaluation report', value: metrics.evaluation_report },
    { name: 'Explainability report', value: metrics.explanation_report },
    { name: 'Training audit', value: metrics.training_audit },
  ].filter((report) => hasContent(report.value));
}

function json(value: unknown): string {
  return typeof value === 'string' ? value : JSON.stringify(value, null, 2);
}

function modelLink(processId: number, modelId: number, view: 'performance' | 'explanation') {
  return { name: 'model-show', params: { id: props.project.id, processId, modelId }, query: { view } };
}

function downloadBaseline() {
  if (!props.project.metricsBaseline) return;
  const result = exportFile('project-' + props.project.id + '-baseline.json', json(props.project.metricsBaseline), 'application/json');
  baselineDownloadFeedback.value = result === true ? 'Results downloaded.' : 'The browser did not allow the download.';
}

watch(() => props.project.id, () => {
  openedId.value = processes.value[0]?.id ?? null;
  inspectedIds.value = {};
  baselineDownloadFeedback.value = '';
}, { immediate: true });
watch(processes, () => {
  if (openedId.value !== null && !processes.value.some((process) => process.id === openedId.value)) openedId.value = processes.value[0]?.id ?? null;
});
watch(() => route.hash, async (hash) => {
  if (hash !== '#project-baseline') return;
  baselineOpened.value = true;
  await nextTick();
  requestAnimationFrame(() => {
    baselineSection.value?.scrollIntoView({ block: 'start', behavior: 'smooth' });
    baselineSection.value?.focus({ preventScroll: true });
  });
}, { immediate: true });
</script>

<template>
  <section class="project-trainings" aria-label="Training run history">
    <div class="section-heading">
      <div><h2>Your training runs</h2><p>A training run is a DrIA search, with its settings and the models retained when it finishes.</p></div>
      <div v-if="processes.length" class="history-count">
        {{ processes.length }} training run{{ processes.length !== 1 ? 's' : '' }}<span>{{ modelCount }} model{{ modelCount !== 1 ? 's' : '' }} available</span>
      </div>
    </div>

    <div v-if="!processes.length" class="training-empty">
      <q-icon name="model_training" size="28px" />
      <div><h3>No training runs recorded</h3><p>Searches for this project will appear here with their results.</p></div>
    </div>

    <div v-else class="training-list">
      <q-expansion-item
        v-for="(training, index) in trainings"
        :key="training.process.id"
        :model-value="openedId === training.process.id"
        class="training-card"
        @update:model-value="(value) => { openedId = value ? training.process.id : openedId === training.process.id ? null : openedId; }"
      >
        <template #header>
          <div class="training-header">
            <span class="training-index" aria-hidden="true">{{ String(processes.length - index).padStart(2, '0') }}</span>
            <div class="training-identity">
              <strong>{{ formatToDDMMYYYHHmm(training.process.date) }}</strong>
              <span>{{ index === 0 ? 'Latest training run' : 'Previous training run' }} · {{ training.process.models?.length ?? 0 }} model{{ (training.process.models?.length ?? 0) !== 1 ? 's' : '' }}</span>
            </div>
            <div class="training-budget"><span>Search budget</span><strong>{{ formatBudget(training.process.maxDuration) }}</strong></div>
            <span :class="['training-status', 'training-status--' + training.state.tone]"><span class="status-dot" aria-hidden="true" />{{ training.state.label }}</span>
          </div>
        </template>

        <div v-if="openedId === training.process.id" class="training-content">
          <div v-for="(message, messageIndex) in processMessages(training.process)" :key="messageIndex" :class="['training-message', 'training-message--' + message.kind]" role="status">
            <q-icon :name="message.kind === 'error' ? 'error_outline' : 'info_outline'" size="18px" /><div><strong>{{ message.title }}</strong><p>{{ message.content }}</p></div>
          </div>
          <p v-if="training.state.pendingResults" class="training-pending" role="status"><q-spinner size="16px" color="primary" />Results are being saved. The candidate list may still change.</p>

          <template v-if="training.result">
            <div class="retained-result">
              <div class="retained-model">
                <span class="selection-label">{{ training.result.selectionLabel }}</span>
                <h3>{{ modelName(training.result.model, training.process) }}</h3>
                <p>{{ scoreForMetrics(training.result.model.metrics).name }}<span v-if="direction(training.result.model.metrics)"> · {{ direction(training.result.model.metrics) }}</span></p>
              </div>
              <div class="retained-score">
                <span>{{ sourceLabel(training.result.model.metrics) }}</span>
                <strong :title="displayScore(scoreForMetrics(training.result.model.metrics).value)">{{ displayScore(scoreForMetrics(training.result.model.metrics).value) }}</strong>
              </div>
              <div class="retained-score retained-score--test">
                <span>Holdout test set</span>
                <strong :title="displayScore(scoreForMetrics(training.result.model.metrics, 'test').value)">{{ displayScore(scoreForMetrics(training.result.model.metrics, 'test').value) }}</strong>
              </div>
            </div>
            <div class="retained-actions">
              <q-btn unelevated no-caps label="View results" icon="arrow_forward" color="primary" :to="modelLink(training.process.id, training.result.model.id, 'performance')" />
              <q-btn flat no-caps label="Understand the model" icon="lightbulb_outline" color="primary" :to="modelLink(training.process.id, training.result.model.id, 'explanation')" />
              <q-btn flat no-caps label="All metrics" icon="list_alt" color="primary" :aria-expanded="inspectedIds[training.process.id] === training.result.model.id" @click="inspect(training.process, training.result.model.id)" />
            </div>

            <q-expansion-item
              v-if="training.candidates.length"
              :label="training.candidates.length + ' other candidate' + (training.candidates.length !== 1 ? 's' : '')"
              caption="View their scores and results"
              dense
              class="other-candidates"
            >
              <div class="candidate-columns" aria-hidden="true"><span>Candidate and metric</span><span>Internal evaluation</span><span>Holdout test set</span><span /></div>
              <ul class="candidate-list">
                <li v-for="model in training.candidates" :key="model.id" class="candidate-row">
                  <div class="candidate-model">
                    <router-link :to="modelLink(training.process.id, model.id, 'performance')">{{ modelName(model, training.process) }}</router-link>
                    <span>{{ scoreForMetrics(model.metrics).name }}<span v-if="direction(model.metrics)"> · {{ direction(model.metrics) }}</span></span>
                  </div>
                  <div class="candidate-score"><span>{{ sourceLabel(model.metrics) }}</span><strong :title="displayScore(scoreForMetrics(model.metrics).value)">{{ displayScore(scoreForMetrics(model.metrics).value) }}</strong></div>
                  <div class="candidate-score candidate-score--test"><span class="mobile-score-label">Holdout test set</span><strong :title="displayScore(scoreForMetrics(model.metrics, 'test').value)">{{ displayScore(scoreForMetrics(model.metrics, 'test').value) }}</strong></div>
                  <div class="candidate-actions">
                    <q-btn flat dense round icon="lightbulb_outline" color="primary" :aria-label="'Understand ' + modelName(model, training.process)" :to="modelLink(training.process.id, model.id, 'explanation')"><q-tooltip>Understand the model</q-tooltip></q-btn>
                    <q-btn flat dense no-caps label="Details" color="primary" :aria-expanded="inspectedIds[training.process.id] === model.id" @click="inspect(training.process, model.id)" />
                  </div>
                </li>
              </ul>
              <p class="score-guide">The internal score guides the search. The holdout test set measures final performance; it is not used to rank candidates.</p>
            </q-expansion-item>

            <div v-if="inspectedModel(training.process)" class="candidate-inspector">
              <template v-for="model in [inspectedModel(training.process)!]" :key="model.id">
                <div class="inspector-heading"><div><span>Metrics and reports</span><h4>{{ modelName(model, training.process) }}</h4></div><q-btn flat dense round icon="close" :aria-label="'Close details for ' + modelName(model, training.process)" @click="inspectedIds[training.process.id] = undefined" /></div>
                <p v-if="model.metrics.model_info?.description" class="model-description">{{ model.metrics.model_info.description }}</p>
                <div v-if="allMetrics(model.metrics).length" class="metrics-table-wrap">
                  <table class="metrics-table">
                    <thead><tr><th scope="col">Metric</th><th scope="col">{{ sourceLabel(model.metrics) }}</th><th scope="col">Holdout test set</th></tr></thead>
                    <tbody><tr v-for="metric in allMetrics(model.metrics)" :key="metric.key">
                      <th scope="row"><span>{{ metric.name }}<small v-if="metric.key === metricKey(model.metrics)" class="main-metric-label">Primary metric</small></span><small v-if="metric.description">{{ metric.description }}</small><small v-if="metric.direction">{{ metric.direction }}</small></th>
                      <td :title="displayScore(metric.validation)">{{ displayScore(metric.validation) }}</td><td :title="displayScore(metric.test)">{{ displayScore(metric.test) }}</td>
                    </tr></tbody>
                  </table>
                </div>
                <p v-else class="score-guide">No numeric metrics were returned.</p>
                <details v-for="report in reports(model.metrics)" :key="report.name" class="report-details"><summary>{{ report.name }}</summary><pre tabindex="0">{{ json(report.value) }}</pre></details>
              </template>
            </div>
          </template>
          <div v-else class="models-empty"><q-icon :name="training.state.tone === 'running' ? 'hourglass_top' : 'info_outline'" size="20px" /><p>{{ ['QUEUED', 'RUNNING'].includes(training.process.status) ? 'Models will appear after the search and evaluation.' : training.state.pendingResults ? 'Models are being saved.' : 'No model is available for this training run.' }}</p></div>

          <div class="training-actions">
            <q-btn flat dense no-caps icon="tune" label="View settings" color="primary" @click="emit('configure', training.process.id)" />
            <q-btn flat dense no-caps icon="fork_right" label="Adjust configuration" color="primary" :to="{ name: 'pipeline-fork', params: { id: project.id, processId: training.process.id } }" />
            <q-btn flat dense no-caps icon="refresh" label="Resume search" color="primary" :disable="active" @click="emit('restart', training.process.id)" />
          </div>
          <q-expansion-item v-if="hasContent(training.process.statusSnapshot?.logs) || training.process.iaRemoteId" label="Training run technical details" icon="terminal" dense class="technical-details">
            <dl v-if="training.process.iaRemoteId" class="remote-id"><dt>DrIA identifier</dt><dd>{{ training.process.iaRemoteId }}</dd></dl>
            <pre v-if="hasContent(training.process.statusSnapshot?.logs)" tabindex="0" aria-label="Training run logs">{{ json(training.process.statusSnapshot?.logs) }}</pre>
          </q-expansion-item>
        </div>
      </q-expansion-item>
    </div>

    <section id="project-baseline" ref="baselineSection" class="baseline-section" tabindex="-1" aria-label="Baseline for the study">
      <q-expansion-item v-model="baselineOpened" label="Baseline model" :caption="baselineStatus" icon="balance" class="baseline-card">
        <div v-if="baselineOpened" class="baseline-content">
          <p class="baseline-definition">A simple model trained with the default DrIA settings, without an optimization search. It provides a baseline for your study.</p>
          <p class="score-guide">Its training scores are not directly comparable to cross-validation scores. A comparison requires the same metric evaluated on the same test set.</p>
          <div v-if="project.metricsBaseline?.error" class="training-message training-message--error" role="status"><q-icon name="error_outline" size="18px" /><div><strong>DrIA service message</strong><p>{{ displayServiceText(project.metricsBaseline.error) }}</p></div></div>
          <p v-if="['QUEUED', 'RUNNING'].includes(project.metricsBaseline?.status?.current ?? '')" class="training-pending" role="status"><q-spinner size="16px" color="primary" />The baseline model is being computed.</p>
          <article v-for="(metrics, index) in baselineMetrics" :key="index" class="baseline-result">
            <h3>{{ displayServiceName(metrics.model_info?.name) || (baselineMetrics.length > 1 ? 'Baseline ' + (index + 1) : 'Baseline results') }}</h3>
            <p v-if="metrics.model_info?.description" class="model-description">{{ metrics.model_info.description }}</p>
            <div v-if="allMetrics(metrics).length" class="metrics-table-wrap">
              <table class="metrics-table">
                <thead><tr><th scope="col">Metric</th><th scope="col">{{ sourceLabel(metrics) }}</th><th scope="col">Holdout test set</th></tr></thead>
                <tbody><tr v-for="metric in allMetrics(metrics)" :key="metric.key">
                  <th scope="row"><span>{{ metric.name }}<small v-if="metric.key === metricKey(metrics)" class="main-metric-label">Primary metric</small></span><small v-if="metric.description">{{ metric.description }}</small><small v-if="metric.direction">{{ metric.direction }}</small></th>
                  <td :title="displayScore(metric.validation)">{{ displayScore(metric.validation) }}</td><td :title="displayScore(metric.test)">{{ displayScore(metric.test) }}</td>
                </tr></tbody>
              </table>
            </div>
            <p v-else class="score-guide">No numeric metrics were returned.</p>
          </article>
          <p v-if="!baselineMetrics.length && !['QUEUED', 'RUNNING'].includes(project.metricsBaseline?.status?.current ?? '')" class="baseline-unavailable">No baseline scores are available yet.</p>
          <q-expansion-item v-if="project.metricsBaseline" label="Complete baseline results · JSON" icon="data_object" dense class="technical-details">
            <div class="baseline-json-actions"><q-btn flat dense no-caps icon="download" label="Download results" color="primary" @click="downloadBaseline" /><span v-if="baselineDownloadFeedback" role="status">{{ baselineDownloadFeedback }}</span></div>
            <pre tabindex="0" aria-label="Complete baseline results in JSON format">{{ json(project.metricsBaseline) }}</pre>
          </q-expansion-item>
        </div>
      </q-expansion-item>
    </section>
  </section>
</template>

<style scoped>
.project-trainings { color: var(--project-ink); min-width: 0; }
.section-heading { display: flex; justify-content: space-between; align-items: flex-start; gap: 24px; margin-bottom: 24px; }
h2 { margin: 0 0 8px; font-size: 22px; font-weight: 600; line-height: 1.35; letter-spacing: -.3px; }
.section-heading p { margin: 0; color: var(--project-muted); line-height: 1.7; max-width: 660px; }
.history-count { padding-top: 4px; flex-shrink: 0; font-size: 13px; }
.history-count span { display: block; margin-top: 5px; color: var(--project-muted); font-size: 12px; }
.training-list { border: 1px solid var(--project-border); border-radius: 10px; background: var(--project-surface); overflow: hidden; }
.training-card + .training-card { border-top: 1px solid var(--project-border); }
.training-card > :deep(.q-expansion-item__container > .q-item) { padding: 18px 20px; }
.training-card :deep(.q-item__section--side) { color: var(--project-muted); }
.training-card :deep(.q-item__section--main) { min-width: 0; }
.training-header { display: grid; grid-template-columns: 32px minmax(0, 1.6fr) minmax(0, .8fr) auto; align-items: center; gap: 18px; min-width: 0; width: 100%; }
.training-index { font-size: 12px; color: var(--project-muted); font-variant-numeric: tabular-nums; }
.training-identity { min-width: 0; }
.training-identity strong { display: block; font-weight: 600; font-size: 15px; font-variant-numeric: tabular-nums; }
.training-identity > span { display: block; margin-top: 5px; color: var(--project-muted); font-size: 11px; }
.training-budget span { display: block; font-size: 11px; color: var(--project-muted); margin-bottom: 4px; }
.training-budget strong { font-weight: 500; font-size: 13px; }
.training-status { display: inline-flex; align-items: center; gap: 7px; font-size: 11px; line-height: 1.5; padding: 5px 8px; border-radius: 5px; background: var(--project-paper); max-width: 175px; }
.status-dot { height: 6px; width: 6px; flex-shrink: 0; border-radius: 50%; background: currentColor; }
.training-status--neutral { color: var(--project-muted); }
.training-status--success, .training-status--running { color: var(--q-primary); background: var(--project-tint); }
.training-status--error { color: var(--project-error-ink, var(--project-ink)); }
.training-content { padding: 8px 20px 20px 70px; min-width: 0; }
.retained-result { display: grid; grid-template-columns: minmax(0, 1.5fr) minmax(0, 1fr) minmax(0, .8fr); gap: 18px; padding: 4px 0 4px 16px; border-left: 3px solid var(--project-brand); }
.retained-model { min-width: 0; }
.selection-label { font-size: 11px; font-weight: 500; color: var(--q-primary); }
.retained-model h3 { font-size: 18px; line-height: 1.45; margin: 6px 0; font-weight: 600; overflow-wrap: anywhere; }
.retained-model p { margin: 0; font-size: 12px; color: var(--project-muted); line-height: 1.6; overflow-wrap: anywhere; }
.retained-score span { display: block; font-size: 11px; color: var(--project-muted); line-height: 1.5; margin-bottom: 8px; }
.retained-score strong { font-size: 24px; font-weight: 500; font-variant-numeric: tabular-nums; line-height: 1.4; overflow-wrap: anywhere; }
.retained-score--test { padding-left: 18px; border-left: 1px solid var(--project-border); }
.retained-actions { display: flex; flex-wrap: wrap; gap: 8px; margin: 20px 0 22px; }
.retained-actions .q-btn { min-height: 38px; font-size: 12px; max-width: 100%; }
.other-candidates { border-top: 1px solid var(--project-border); margin-top: 16px; }
.other-candidates > :deep(.q-expansion-item__container > .q-item) { padding: 14px 0; font-size: 13px; }
.other-candidates :deep(.q-item__label--caption) { font-size: 11px; color: var(--project-muted); }
.candidate-columns, .candidate-row { display: grid; grid-template-columns: minmax(0, 1.4fr) minmax(0, 1fr) minmax(0, .75fr) auto; gap: 16px; align-items: center; }
.candidate-columns { color: var(--project-muted); font-size: 10px; padding: 9px 0; border-bottom: 1px solid var(--project-border); }
.candidate-columns > span:last-child { width: 88px; }
.candidate-list { list-style: none; margin: 0; padding: 0; }
.candidate-row { padding: 14px 0; border-bottom: 1px solid var(--project-border); }
.candidate-model { min-width: 0; }
.candidate-model a { color: var(--project-ink); font-size: 13px; font-weight: 600; text-decoration: none; overflow-wrap: anywhere; }
.candidate-model a:hover { color: var(--q-primary); text-decoration: underline; text-underline-offset: 3px; }
.candidate-model > span { display: block; margin-top: 5px; font-size: 11px; color: var(--project-muted); line-height: 1.6; overflow-wrap: anywhere; }
.candidate-score { min-width: 0; }
.candidate-score > span { display: block; font-size: 10px; line-height: 1.5; color: var(--project-muted); margin-bottom: 4px; overflow-wrap: anywhere; }
.candidate-score strong { font-size: 14px; font-weight: 500; font-variant-numeric: tabular-nums; overflow-wrap: anywhere; }
.candidate-score--test { align-self: end; padding-bottom: 2px; }
.candidate-score--test .mobile-score-label { display: none; }
.candidate-actions { display: flex; align-items: center; gap: 4px; }
.candidate-actions .q-btn { font-size: 11px; }
.score-guide { color: var(--project-muted); font-size: 11px; line-height: 1.7; margin: 14px 0 0; }
.candidate-inspector { margin-top: 20px; padding: 18px; border-radius: 8px; background: var(--project-paper); min-width: 0; }
.inspector-heading { display: flex; justify-content: space-between; align-items: flex-start; gap: 12px; margin-bottom: 16px; }
.inspector-heading > div { min-width: 0; }
.inspector-heading span { color: var(--project-muted); font-size: 11px; }
.inspector-heading h4 { margin: 5px 0 0; font-size: 15px; font-weight: 600; line-height: 1.5; overflow-wrap: anywhere; }
.model-description { font-size: 12px; color: var(--project-muted); line-height: 1.7; margin: 0 0 16px; overflow-wrap: anywhere; }
.metrics-table-wrap { max-width: 100%; overflow-x: auto; }
.metrics-table { width: 100%; border-collapse: collapse; table-layout: fixed; font-size: 12px; }
.metrics-table thead th { text-align: left; font-weight: 500; color: var(--project-muted); font-size: 11px; }
.metrics-table th, .metrics-table td { padding: 12px 10px; border-bottom: 1px solid var(--project-border); vertical-align: top; overflow-wrap: anywhere; line-height: 1.6; }
.metrics-table th:first-child { padding-left: 0; width: 44%; }
.metrics-table thead th:not(:first-child) { width: 28%; text-align: right; }
.metrics-table tbody th { text-align: left; font-weight: 500; }
.metrics-table td { white-space: normal; font-variant-numeric: tabular-nums; text-align: right; }
.metrics-table th span { display: block; overflow-wrap: anywhere; }
.metrics-table th small { display: block; margin-top: 4px; font-weight: 400; color: var(--project-muted); line-height: 1.6; }
.metrics-table th small.main-metric-label { font-size: 10px; color: var(--q-primary); }
.report-details { border-top: 1px solid var(--project-border); padding: 12px 0; font-size: 12px; }
.report-details:first-of-type { margin-top: 18px; }
.report-details summary { cursor: pointer; }
.training-actions { display: flex; flex-wrap: wrap; gap: 4px 12px; margin-top: 22px; padding-top: 14px; border-top: 1px solid var(--project-border); }
.training-actions .q-btn { font-size: 11px; min-height: 34px; max-width: 100%; }
.technical-details { color: var(--project-muted); font-size: 11px; margin-top: 8px; }
.technical-details :deep(.q-item) { padding-left: 0; padding-right: 0; font-size: 11px; }
.technical-details :deep(.q-item__label) { white-space: normal; overflow-wrap: anywhere; }
.technical-details :deep(.q-item__section--avatar) { min-width: 28px; }
.technical-details :deep(.q-icon) { font-size: 17px; }
.technical-details pre, .report-details pre { max-width: 100%; margin: 12px 0 0; padding: 14px; background: var(--project-surface); border: 1px solid var(--project-border); border-radius: 6px; max-height: 320px; overflow: auto; font: 11px/1.7 monospace; }
.remote-id { display: flex; flex-wrap: wrap; gap: 8px; margin: 10px 0; font-size: 11px; }
.remote-id dd { margin: 0; font-family: monospace; overflow-wrap: anywhere; }
.training-message { display: flex; align-items: flex-start; gap: 10px; padding: 12px 14px; border: 1px solid var(--project-border); border-radius: 6px; background: var(--project-paper); margin-bottom: 16px; }
.training-message .q-icon { flex-shrink: 0; }
.training-message strong { font-size: 12px; }
.training-message p { margin: 5px 0 0; font-size: 12px; line-height: 1.6; white-space: pre-wrap; overflow-wrap: anywhere; }
.training-message--error { color: var(--project-error-ink, var(--project-ink)); background: var(--project-error-surface, var(--project-paper)); border-color: var(--project-error-border, var(--project-border)); border-left-width: 3px; }
.training-message--warning { color: var(--project-warning-ink, var(--project-ink)); }
.training-pending { display: flex; gap: 8px; align-items: center; color: var(--project-muted); font-size: 12px; line-height: 1.7; margin: 0 0 18px; }
.training-pending .q-spinner { flex-shrink: 0; }
.models-empty { display: flex; align-items: flex-start; gap: 10px; color: var(--project-muted); padding: 8px 0; }
.models-empty .q-icon { flex-shrink: 0; }
.models-empty p { margin: 0; font-size: 13px; line-height: 1.7; }
.training-empty { padding: 28px; display: flex; align-items: flex-start; gap: 16px; background: var(--project-surface); border: 1px solid var(--project-border); border-radius: 10px; }
.training-empty .q-icon { color: var(--q-primary); flex-shrink: 0; }
.training-empty h3 { margin: 0 0 8px; font-size: 16px; font-weight: 600; }
.training-empty p { margin: 0; color: var(--project-muted); line-height: 1.7; }
.baseline-section { margin-top: 28px; scroll-margin-top: 24px; border-radius: 8px; }
.baseline-section:focus-visible { outline: 2px solid var(--project-brand); outline-offset: 4px; }
.baseline-card { border-top: 1px solid var(--project-border); }
.baseline-card > :deep(.q-expansion-item__container > .q-item) { padding: 18px 0; }
.baseline-card :deep(.q-item__section--main) { min-width: 0; }
.baseline-card :deep(.q-item__label) { white-space: normal; overflow-wrap: anywhere; }
.baseline-card :deep(.q-item__label--caption) { color: var(--project-muted); margin-top: 6px; font-size: 11px; line-height: 1.5; }
.baseline-card :deep(.q-item__section--side) { color: var(--project-muted); }
.baseline-content { padding: 0 0 12px; min-width: 0; }
.baseline-definition { margin: 0; font-size: 13px; line-height: 1.7; max-width: 800px; }
.baseline-content > .score-guide { margin: 8px 0 18px; }
.baseline-result { margin-top: 20px; padding-top: 20px; border-top: 1px solid var(--project-border); min-width: 0; }
.baseline-result h3 { margin: 0 0 14px; font-size: 15px; font-weight: 600; line-height: 1.5; overflow-wrap: anywhere; }
.baseline-unavailable { margin: 0; color: var(--project-muted); font-size: 12px; line-height: 1.7; }
.baseline-json-actions { display: flex; align-items: center; flex-wrap: wrap; gap: 10px; padding-top: 8px; }
.baseline-json-actions .q-btn { font-size: 11px; }
.baseline-json-actions span { color: var(--project-muted); font-size: 11px; }
@media (max-width: 899px) {
  .training-header { grid-template-columns: 28px minmax(0, 1fr) auto; gap: 14px; }
  .training-budget { grid-column: 2; grid-row: 2; display: flex; align-items: baseline; gap: 8px; }
  .training-budget span { margin: 0; }
  .training-status { grid-column: 3; grid-row: 1 / span 2; }
  .training-content { padding-left: 62px; }
  .retained-result { grid-template-columns: minmax(0, 1fr) minmax(0, .8fr); }
  .retained-model { grid-column: 1 / -1; }
}
@media (max-width: 599px) {
  h2 { font-size: 20px; }
  .section-heading { display: block; margin-bottom: 20px; }
  .history-count { padding: 0; margin-top: 12px; font-size: 12px; }
  .history-count span { display: inline; margin-left: 12px; font-size: 11px; }
  .training-card > :deep(.q-expansion-item__container > .q-item) { padding: 16px 12px; }
  .training-header { grid-template-columns: 20px minmax(0, 1fr); gap: 10px; }
  .training-identity strong { font-size: 13px; }
  .training-identity > span { font-size: 10px; }
  .training-budget { grid-column: 2; }
  .training-budget span, .training-budget strong { font-size: 10px; }
  .training-status { grid-column: 2; grid-row: 3; justify-self: start; margin-top: 2px; font-size: 10px; max-width: none; }
  .training-index { align-self: start; margin-top: 2px; font-size: 11px; }
  .training-card > :deep(.q-expansion-item__container > .q-item > .q-item__section--side) { padding-left: 8px; }
  .training-content { padding: 4px 14px 18px; }
  .retained-result { gap: 16px; padding-left: 12px; }
  .retained-model h3 { font-size: 16px; }
  .retained-score strong { font-size: 20px; }
  .retained-score--test { padding-left: 12px; }
  .retained-actions { gap: 4px; margin-bottom: 18px; }
  .retained-actions .q-btn { font-size: 11px; }
  .candidate-columns { display: none; }
  .candidate-row { grid-template-columns: minmax(0, 1fr) minmax(0, 1fr); gap: 10px 18px; padding: 16px 0; }
  .candidate-model { grid-column: 1 / -1; }
  .candidate-score--test { align-self: auto; padding: 0; }
  .candidate-score--test .mobile-score-label { display: block; }
  .candidate-actions { grid-column: 1 / -1; justify-content: flex-start; margin-top: 2px; }
  .candidate-inspector { padding: 14px 12px; }
  .metrics-table { font-size: 11px; }
  .metrics-table th, .metrics-table td { padding: 10px 6px; font-size: 10.5px; }
  .metrics-table thead th { font-size: 10px; }
  .metrics-table th small { font-size: 10px; }
  .training-actions { gap: 4px; }
  .training-actions .q-btn { font-size: 10px; }
}
</style>
