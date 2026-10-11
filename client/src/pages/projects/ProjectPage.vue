<script setup lang="ts">
import 'src/css/project-creation.scss';
import { computed, onMounted, onUnmounted, ref, watch } from 'vue';
import { useRoute, useRouter } from 'vue-router';
import { IProject } from 'components/models';
import ProjectDataExplorer from 'components/projects/ProjectDataExplorer.vue';
import ProjectTrainings from 'components/projects/ProjectTrainings.vue';
import ProjectConfiguration from 'components/projects/ProjectConfiguration.vue';
import ProjectModelSummary from 'components/projects/ProjectModelSummary.vue';
import { GetProjectWithNestedDataById } from 'src/api/projects.api';
import { restartProcessAsync, getProcessStatus } from 'src/api/processes.api';
import { apiError, formatScore as score, isTemporaryApiError } from 'src/utils/iaml';
import { baselineComparison, formatBudget, latestProjectResult, metricDefinition, processState, scoreForMetrics } from 'src/utils/project-results';
import { formatToDDMMYYYHHmm } from 'src/utils/date';
import { displayServiceName } from 'src/utils/service-label';

const route = useRoute();
const router = useRouter();
const project = ref<IProject>();
const projectId = computed(() => Number(route.params.id));
const views = ['overview', 'training', 'data', 'configuration'] as const;
type ProjectView = typeof views[number];
const view = computed<ProjectView>({
  get: () => views.includes(route.query.view as ProjectView) ? route.query.view as ProjectView : 'overview',
  set: (value) => { void router.replace({ query: { ...route.query, view: value }, hash: '' }); },
});
const configuredProcessId = computed(() => Number(route.query.processId) || undefined);
const dataVisited = ref(false);
const loading = ref(true);
const error = ref('');
const restarting = ref(false);
const restartDialog = ref(false);
const restartId = ref<number>();
const restartBudget = ref(300);
const restartError = ref('');
const progress = ref(0);
const isFinalizing = ref(false);
const remainingSeconds = ref(0);
const processes = computed(() => [...(project.value?.processes ?? [])].sort((a, b) =>
  Date.parse(String(b.date)) - Date.parse(String(a.date)) || b.id - a.id));
const latest = computed(() => processes.value[0]);
const active = computed(() => processes.value.filter((p) => ['QUEUED', 'RUNNING'].includes(p.status)));
const isProcessing = computed(() => active.value.length > 0);
const pendingResults = computed(() => processes.value.some((p) => processState(p).pendingResults));
const baselineActive = computed(() => ['QUEUED', 'RUNNING'].includes(project.value?.metricsBaseline?.status?.current ?? ''));
const result = computed(() => latestProjectResult(processes.value));
const validation = computed(() => result.value ? scoreForMetrics(result.value.model.metrics, 'validation') : undefined);
const test = computed(() => result.value ? scoreForMetrics(result.value.model.metrics, 'test') : undefined);
const showOtherMetrics = ref(false);
const modelSummaryOpen = ref(false);
const otherMetrics = computed(() => {
  const metrics = result.value?.model.metrics;
  if (!metrics) return [];
  const keys = new Set([
    ...Object.keys(metrics.metrics_info ?? {}),
    ...Object.keys(metrics.metrics_train ?? {}),
    ...Object.keys(metrics.metrics_test ?? {}),
  ]);
  return [...keys].filter((key) => key !== validation.value?.key).map((key) => ({
    key,
    name: displayServiceName(metrics.metrics_info?.[key]?.name || key),
    greaterIsBetter: metrics.metrics_info?.[key]?.greater_is_better,
    validation: metrics.metrics_train?.[key],
    test: metrics.metrics_test?.[key],
  })).sort((a, b) => a.name.localeCompare(b.name, 'en-US'));
});
const reference = computed<ReturnType<typeof baselineComparison> | undefined>(() => {
  if (!result.value) return undefined;
  const comparison = baselineComparison(result.value.model, project.value?.metricsBaseline);
  if (!comparison.comparable) return comparison;
  const original = processes.value.at(-1);
  const key = validation.value?.key ?? '';
  const initialDefinition = metricDefinition(original?.pipeline?.metrics, key);
  const currentDefinition = metricDefinition(result.value.process.pipeline?.metrics, key);
  if (!initialDefinition || initialDefinition !== currentDefinition) return {
    ...comparison,
    comparable: false,
    improvement: undefined,
    reason: 'The metric settings have changed or cannot be verified. The scores remain available, but their difference is not shown as an improvement.',
  };
  return comparison;
});
const referenceChange = computed(() => {
  const value = reference.value?.improvement;
  if (!reference.value?.comparable || value === undefined) return undefined;
  return {
    label: value > 0 ? 'Performance improvement' : value < 0 ? 'Performance decrease' : 'No difference',
    tone: value > 0 ? 'gain' : value < 0 ? 'loss' : 'equal',
    icon: value > 0 ? 'trending_up' : value < 0 ? 'trending_down' : 'drag_handle',
    value: value !== 0 && score(Math.abs(value)) === '0' ? '< 0.0001' : score(Math.abs(value)),
  };
});
const lastState = computed(() => latest.value ? processState(latest.value) : undefined);
const statusText = computed(() => isProcessing.value ? 'Training in progress'
  : pendingResults.value ? 'Retrieving results'
    : lastState.value?.tone === 'error' || latest.value?.status === 'CANCELED' ? lastState.value?.label ?? 'Canceled'
      : result.value ? 'Results available' : 'Project ready');
const statusTone = computed(() => isProcessing.value || pendingResults.value ? 'running'
  : lastState.value?.tone === 'error' ? 'error' : result.value ? 'success' : 'neutral');
const modelCount = computed(() => processes.value.reduce((sum, p) => sum + (p.models?.length ?? 0), 0));
const restartTarget = computed(() => processes.value.find((p) => p.id === restartId.value));
const remainingText = computed(() => {
  const seconds = remainingSeconds.value;
  return seconds < 60 ? `${seconds} second${seconds > 1 ? 's' : ''}` : `${Math.ceil(seconds / 60)} minute${seconds > 60 ? 's' : ''}`;
});
let trackedProcessId: number | undefined;
let startProcessTime: number | undefined;
const processStartTimes = new Map<number, number>();
const stoppedProcessIds = new Set<number>();
let timer: ReturnType<typeof setTimeout> | undefined;
let progressTimer: ReturnType<typeof setInterval> | undefined;
let disposed = false;
let fetchingId: number | undefined;
let projectRevision = 0;

function schedule() {
  if (disposed) return;
  if (timer) clearTimeout(timer);
  timer = setTimeout(() => { void refresh(); }, 3000);
}
async function refresh(retry = false) {
  const id = projectId.value;
  const revision = projectRevision;
  if (fetchingId === id || disposed) return;
  if (retry) stoppedProcessIds.clear();
  if (timer) clearTimeout(timer);
  fetchingId = id;
  const knownPending = isProcessing.value || pendingResults.value || baselineActive.value || Boolean(route.query.jobId);
  try {
    const next = await GetProjectWithNestedDataById(id);
    if (disposed || revision !== projectRevision) return;
    project.value = next;
    error.value = '';
    for (const p of active.value.filter((process) => !stoppedProcessIds.has(process.id))) {
      try {
        const status = await getProcessStatus(p.id);
        if (disposed || revision !== projectRevision) return;
        p.status = status.status?.current ?? p.status;
        const running = status.status?.history?.find((entry: { status: string; date: string }) => entry.status === 'RUNNING');
        const startedAt = running?.date ? Date.parse(running.date) : NaN;
        if (Number.isFinite(startedAt) && startedAt <= Date.now()) processStartTimes.set(p.id, startedAt);
      } catch (e) {
        if (revision !== projectRevision) return;
        error.value = apiError(e);
        if (!isTemporaryApiError(e)) stoppedProcessIds.add(p.id);
      }
    }
    updateProgress();
    if (active.value.some((p) => !stoppedProcessIds.has(p.id)) || baselineActive.value || pendingResults.value) schedule();
  } catch (e) {
    if (revision !== projectRevision || disposed) return;
    error.value = apiError(e);
    if (knownPending && isTemporaryApiError(e)) schedule();
  } finally {
    if (revision === projectRevision) { fetchingId = undefined; loading.value = false; }
  }
}
function updateProgress() {
  const process = active.value[0];
  if (!process || process.status === 'QUEUED') {
    trackedProcessId = undefined; startProcessTime = undefined;
    progress.value = 0; isFinalizing.value = false; remainingSeconds.value = 0;
    return;
  }
  if (trackedProcessId !== process.id) { trackedProcessId = process.id; startProcessTime = Date.now(); }
  startProcessTime = processStartTimes.get(process.id) ?? startProcessTime;
  const duration = process.maxDuration * 1000;
  if (!duration || startProcessTime === undefined) return;
  const elapsed = Math.max(0, Date.now() - startProcessTime);
  isFinalizing.value = elapsed >= duration;
  progress.value = Math.min(elapsed / duration, 0.99);
  remainingSeconds.value = Math.max(0, Math.ceil((duration - elapsed) / 1000));
}
function configure(processId?: number) {
  void router.replace({ query: { ...route.query, view: 'configuration', processId: processId ? String(processId) : undefined }, hash: '' });
}
function modelLink(viewName = 'performance') {
  if (!result.value) return undefined;
  return { name: 'model-show', params: { id: projectId.value, processId: result.value.process.id, modelId: result.value.model.id }, query: { view: viewName, returnView: 'overview' } };
}
function openRestart(processId = latest.value?.id) {
  if (!processId || isProcessing.value) return;
  const process = processes.value.find((p) => p.id === processId);
  if (!process) return;
  restartId.value = processId;
  restartBudget.value = Math.max(60, Math.min(3600, process.maxDuration || 300));
  restartError.value = '';
  restartDialog.value = true;
}
async function restartProcess() {
  if (!restartTarget.value || restarting.value) return;
  if (!Number.isInteger(restartBudget.value) || restartBudget.value < 60 || restartBudget.value > 3600) {
    restartError.value = 'Choose a whole-number budget between 60 and 3,600 seconds.';
    return;
  }
  restarting.value = true;
  try {
    const response = await restartProcessAsync(restartTarget.value.id, restartBudget.value);
    if (!response) { restartError.value = 'The training run could not start. You can try again.'; return; }
    restartDialog.value = false;
    await router.replace({ name: 'project-show', params: { id: response.projectId }, query: { view: 'overview', jobId: response.id } });
    await refresh(true);
  } catch (e) { restartError.value = apiError(e); }
  finally { restarting.value = false; }
}
watch(view, (next) => { if (next === 'data') dataVisited.value = true; }, { immediate: true });
watch([view, projectId, () => result.value?.model.id], () => { modelSummaryOpen.value = false; });
watch(projectId, () => {
  projectRevision += 1;
  if (timer) clearTimeout(timer);
  project.value = undefined; loading.value = true; error.value = ''; fetchingId = undefined;
  trackedProcessId = undefined; startProcessTime = undefined; processStartTimes.clear(); stoppedProcessIds.clear();
  restartDialog.value = false;
  showOtherMetrics.value = false;
  dataVisited.value = view.value === 'data';
  void refresh();
}, { immediate: true });
onMounted(() => { progressTimer = setInterval(updateProgress, 2000); });
onUnmounted(() => {
  disposed = true;
  if (timer) clearTimeout(timer);
  if (progressTimer) clearInterval(progressTimer);
});
</script>

<template>
  <q-page class="project-workspace project-workspace-colors">
    <div class="workspace-container">
      <div class="workspace-topline"><q-btn flat dense no-caps icon="arrow_back" label="Home" :to="{ name: 'home' }" /></div>
      <div v-if="loading && !project" class="workspace-loading" role="status"><q-spinner size="28px" color="primary" /><p>Opening your project…</p></div>
      <div v-if="error" class="workspace-error" role="alert"><q-icon name="error_outline" size="21px" /><div><strong>{{ project ? 'Project updates have stopped' : 'Unable to open the project' }}</strong><p>{{ error }}</p></div><q-btn flat no-caps label="Try again" :disable="loading" @click="refresh(true)" /></div>
      <template v-if="project">
        <header class="project-heading">
          <div><p class="eyebrow">Your study workspace</p><h1>{{ project.title }}</h1><p v-if="project.description" class="project-description">{{ project.description }}</p></div>
          <div class="heading-aside">
            <div class="heading-actions">
              <span class="project-status" :class="`status-${statusTone}`"><span />{{ statusText }}</span>
              <div class="heading-action-buttons">
                <q-btn outline no-caps icon="add" label="New training run" color="primary" :disable="isProcessing || !latest" @click="openRestart()" />
                <q-btn v-if="result" unelevated no-caps color="primary" icon="analytics" :icon-right="modelSummaryOpen ? 'expand_less' : 'expand_more'" class="model-summary-launcher" aria-haspopup="dialog" :aria-expanded="modelSummaryOpen" aria-label="Show the model summary">
                  <span>{{ result.selectedByIaml ? 'Selected model' : 'Available model' }}</span>
                  <span v-if="validation?.value !== null && validation?.value !== undefined" class="model-summary-launcher-score">{{ score(validation.value) }}</span>
                  <q-menu v-model="modelSummaryOpen" anchor="bottom right" self="top right" :offset="[0, 8]" max-height="calc(100vh - 24px)" class="model-summary-popup project-workspace-colors" role="dialog" aria-label="Model summary">
                    <ProjectModelSummary
                      :result="result"
                      :project-id="projectId"
                      :return-view="view"
                      :validation="validation"
                      :test="test"
                      :historical="latest?.id !== result.process.id"
                    />
                  </q-menu>
                </q-btn>
              </div>
            </div>
          </div>
        </header>
        <section v-if="isProcessing || pendingResults" class="training-live" aria-labelledby="live-title" role="status">
          <div class="live-heading"><span class="live-icon"><q-spinner v-if="isProcessing" size="22px" /><q-icon v-else name="sync" size="22px" /></span><div><h2 id="live-title">{{ isProcessing ? active[0]?.status === 'QUEUED' ? 'Your training run is queued' : isFinalizing ? 'The model is being refitted and evaluated' : 'DrIA is searching for a model suited to your data' : 'Results are being retrieved' }}</h2><p>{{ isProcessing ? `Search budget: ${formatBudget(active[0].maxDuration)}. Final evaluation may take additional time.` : 'The computation is complete. Models and their analyses will appear once they have been saved.' }}</p></div><q-btn flat no-caps dense label="View training run" icon-right="arrow_forward" @click="view = 'training'" /></div>
          <q-linear-progress v-if="isProcessing" :value="progress" :indeterminate="active[0]?.status === 'QUEUED' || isFinalizing" rounded stripe color="primary" size="30px" class="training-progress"><div class="absolute-full flex flex-center"><q-badge color="white" text-color="primary" class="training-progress-label" :label="active[0]?.status === 'QUEUED' ? 'Queued' : isFinalizing ? 'Refitting and final evaluation in progress…' : `${Math.floor(progress * 100)}% · About ${remainingText} of search remaining`" /></div></q-linear-progress>
        </section>
        <section v-else-if="lastState && (lastState.tone === 'error' || latest?.status === 'CANCELED')" class="training-alert" role="status"><q-icon name="error_outline" size="22px" /><div><strong>Latest training run : {{ lastState.label }}</strong><p>{{ result ? 'The previous result remains available below. View the details to understand why the latest training run stopped.' : 'View the training run details to understand why the computation stopped.' }}</p></div><q-btn flat no-caps label="View details" icon-right="arrow_forward" @click="view = 'training'" /></section>

        <q-tabs v-model="view" class="project-tabs" align="left" no-caps outside-arrows mobile-arrows active-color="primary" indicator-color="primary" aria-label="Project sections"><q-tab name="overview" label="Overview" /><q-tab name="training"><span>Training runs <span class="tab-count">{{ processes.length }}</span></span></q-tab><q-tab name="data" label="Data" /><q-tab name="configuration" label="Configuration" /></q-tabs>
        <div class="workspace-content">
          <section v-if="view === 'overview'" class="project-overview" aria-label="Project overview">
            <div class="overview-main">
              <article v-if="result" class="result-panel">
                <div class="section-heading"><div><p class="eyebrow">{{ result.selectedByIaml ? 'Model selected by DrIA' : 'Model to review' }}</p><h2>{{ displayServiceName(result.model.metrics.model_info?.name) || 'Available model' }}</h2><p v-if="!result.selectedByIaml">{{ result.selectionLabel }}</p></div></div>
                <div class="result-provenance"><q-icon name="schedule" size="16px" />Training run on {{ formatToDDMMYYYHHmm(result.process.date) }}<span v-if="latest?.id !== result.process.id" class="historical-result">Earlier result</span></div>
                <div class="result-scoreboard">
                  <div class="main-score"><span class="score-source">{{ validation?.label }}</span><strong>{{ score(validation?.value) }}</strong><span class="score-metric">{{ validation?.name || 'Primary metric not specified' }}</span><span class="metric-direction">{{ validation?.greaterIsBetter === undefined ? 'Metric direction not specified' : validation.greaterIsBetter ? 'Higher values are better' : 'Lower values are better' }}</span></div>
                  <div class="test-score"><span class="score-source">Final holdout test set</span><strong v-if="test?.value !== null">{{ score(test?.value) }}</strong><strong v-else class="score-unavailable">{{ project.evaluationMode === 'cross_validation' ? 'Not planned' : 'Unavailable' }}</strong><p>{{ project.evaluationMode === 'cross_validation' ? 'This project uses internal validation without a separate test set.' : 'Measured on observations reserved for testing, separate from those used to select the model.' }}</p></div>
                </div>
                <div class="result-actions">
                  <q-btn unelevated no-caps color="primary" icon="science" label="Try a prediction" :to="{ name: 'model-test', params: { id: projectId, processId: result.process.id, modelId: result.model.id }, query: { returnView: 'overview' } }" />
                  <q-btn outline no-caps color="primary" label="Analyze this model" icon-right="arrow_forward" :to="modelLink()" />
                  <q-btn flat no-caps color="primary" icon="psychology" label="Understand its predictions" :to="modelLink('explanation')" />
                </div>
                <section v-if="otherMetrics.length" class="additional-metrics" aria-label="Other model metrics">
                  <div class="additional-metrics-heading">
                    <span>Explore more results</span>
                    <q-toggle v-model="showOtherMetrics" color="primary" :label="`Other metrics (${otherMetrics.length})`" size="sm" aria-controls="overview-other-metrics" />
                  </div>
                  <div v-if="showOtherMetrics" id="overview-other-metrics" class="additional-metrics-content">
                    <table class="additional-metrics-table">
                      <caption class="sr-only">Additional metrics for the displayed model, measured on validation data and the holdout test set.</caption>
                      <thead><tr><th scope="col">Metric</th><th scope="col">{{ validation?.label }}</th><th scope="col">Holdout test set</th></tr></thead>
                      <tbody><tr v-for="metric in otherMetrics" :key="metric.key"><th scope="row"><span>{{ metric.name }}</span><small v-if="metric.greaterIsBetter !== undefined">{{ metric.greaterIsBetter ? 'Maximize' : 'Minimize' }}</small></th><td :data-metric="metric.key" data-source="validation">{{ score(metric.validation) }}</td><td :data-metric="metric.key" data-source="test">{{ score(metric.test) }}</td></tr></tbody>
                    </table>
                    <q-btn flat dense no-caps color="primary" label="View metric definitions" icon-right="arrow_forward" :to="modelLink()" class="metric-definitions-link" />
                  </div>
                </section>
                <section class="reference-comparison" aria-labelledby="reference-heading">
                  <div class="reference-heading">
                    <h3 id="reference-heading">Baseline comparison</h3>
                    <div v-if="reference?.name" class="reference-criterion"><span>{{ reference.name }}</span><span>Holdout test set</span></div>
                  </div>
                  <div v-if="reference?.baselineScore !== undefined || reference?.modelScore !== undefined" class="reference-scores" :class="{ 'reference-scores--comparable': referenceChange }">
                    <div class="reference-score">
                      <span class="reference-score-label">Baseline</span>
                      <strong>{{ score(reference?.baselineScore) }}</strong>
                    </div>
                    <div class="reference-score reference-score--current">
                      <span class="reference-score-label">{{ result.selectedByIaml ? 'Selected model' : 'Displayed model' }}</span>
                      <strong>{{ score(reference?.modelScore) }}</strong>
                    </div>
                    <div v-if="referenceChange" class="reference-score reference-difference" :class="`reference-difference--${referenceChange.tone}`">
                      <div class="reference-change-label"><q-icon :name="referenceChange.icon" size="15px" aria-hidden="true" /><span>{{ referenceChange.label }}</span></div>
                      <strong>{{ referenceChange.value }}</strong>
                    </div>
                  </div>
                  <div v-if="!referenceChange" class="reference-unavailable"><q-spinner v-if="baselineActive" size="16px" aria-hidden="true" /><q-icon v-else name="info_outline" size="16px" aria-hidden="true" /><p>{{ reference?.reason }}</p></div>
                  <div class="reference-footer">
                    <p class="reference-context">Baseline without optimization.<span v-if="reference?.comparable"> Same metric, same test set.</span></p>
                    <q-btn flat dense no-caps color="primary" label="View baseline" icon-right="arrow_forward" class="reference-details-link" :to="{ query: { ...route.query, view: 'training' }, hash: '#project-baseline' }" />
                  </div>
                </section>
              </article>
              <article v-else class="result-empty"><span class="empty-symbol"><q-icon :name="isProcessing || pendingResults ? 'model_training' : 'science'" size="34px" /></span><p class="eyebrow">Your next result</p><h2>{{ isProcessing || pendingResults ? 'Your first model is on its way' : 'No model available yet' }}</h2><p>{{ isProcessing || pendingResults ? 'Once training is complete, the selected model, its performance and links to its analysis will appear here.' : 'Review training runs and their configuration to continue your study.' }}</p><q-btn v-if="latest && !isProcessing && !pendingResults" unelevated no-caps color="primary" label="View configuration" icon-right="arrow_forward" @click="configure(latest.id)" /><p v-if="!latest" class="empty-detail">No training runs have been recorded for this project yet.</p></article>
              <div class="overview-note"><q-icon name="info_outline" size="18px" /><p>Performance reflects the data and evaluation used in this study. The model analysis provides all metrics, result limitations and available explanations.</p></div>
            </div>
            <aside class="project-explore" aria-labelledby="explore-title"><p class="eyebrow">Explore further</p><h2 id="explore-title">Explore your study</h2><q-btn flat no-caps align="left" class="explore-link" @click="view = 'training'"><span class="explore-icon"><q-icon name="history" size="21px" /></span><span><strong>Training runs</strong><small>{{ processes.length }} training run{{ processes.length !== 1 ? 's' : '' }} · {{ modelCount }} model{{ modelCount !== 1 ? 's' : '' }} available</small></span><q-icon name="chevron_right" size="20px" /></q-btn><q-btn flat no-caps align="left" class="explore-link" @click="view = 'data'"><span class="explore-icon"><q-icon name="table_view" size="21px" /></span><span><strong>Project data</strong><small>Data table, column summaries and charts</small></span><q-icon name="chevron_right" size="20px" /></q-btn><q-btn flat no-caps align="left" class="explore-link" @click="configure(result?.process.id)"><span class="explore-icon"><q-icon name="account_tree" size="21px" /></span><span><strong>Configuration</strong><small>Preprocessing, models and validation</small></span><q-icon name="chevron_right" size="20px" /></q-btn></aside>
          </section>
          <ProjectTrainings v-else-if="view === 'training'" :project="project" :active="isProcessing" @configure="configure" @restart="openRestart" />
          <ProjectConfiguration v-else-if="view === 'configuration'" :project="project" :process-id="configuredProcessId" @restart="openRestart" />
          <ProjectDataExplorer v-if="dataVisited" v-show="view === 'data'" :key="project.id" :project="project" />
        </div>
      </template>
    </div>
    <q-dialog v-model="restartDialog" :persistent="restarting"><q-card class="restart-card project-workspace-colors"><q-card-section class="restart-heading"><div><p class="eyebrow">Continue your study</p><h2>Restart a training run</h2></div><q-btn v-close-popup flat round dense icon="close" aria-label="Close restart dialog" :disable="restarting" /></q-card-section><q-card-section class="restart-body"><p>The configuration from the training run on <strong>{{ formatToDDMMYYYHHmm(restartTarget?.date) }}</strong> will be reused. Previous results remain available.</p><q-input v-model.number="restartBudget" outlined label="Search budget in seconds" type="number" :min="60" :max="3600" :step="1" :disable="restarting" hint="Between 60 and 3,600 seconds. Final evaluation takes additional time." /><p v-if="restartError" class="download-error" role="alert">{{ restartError }}</p><q-btn v-if="restartTarget" flat no-caps color="primary" label="Adjust configuration before starting" icon="tune" :disable="restarting" :to="{ name: 'pipeline-fork', params: { id: projectId, processId: restartTarget.id } }" class="restart-adapt" /></q-card-section><q-card-actions align="right"><q-btn v-close-popup flat no-caps label="Cancel" :disable="restarting" /><q-btn unelevated no-caps color="primary" label="Start training run" :loading="restarting" @click="restartProcess" /></q-card-actions></q-card></q-dialog>
  </q-page>
</template>

<style scoped>
.project-workspace { padding: 20px 32px 48px; color: var(--project-ink); }
.workspace-container { max-width: 1220px; margin: 0 auto; }
.workspace-topline { display: flex; align-items: center; margin-bottom: 28px; color: var(--project-muted); }
.workspace-topline .q-btn { font-size: 12px; }
.workspace-loading { display: grid; justify-items: center; padding: 80px 20px; color: var(--project-muted); }
.workspace-error { display: flex; align-items: flex-start; gap: 12px; padding: 16px; margin-bottom: 22px; border: 1px solid var(--project-error-border); border-radius: 8px; background: var(--project-error-surface); color: var(--project-error-ink); }
.workspace-error > div { flex: 1; min-width: 0; overflow-wrap: anywhere; }
.workspace-error p { margin: 5px 0 0; }
.project-heading { display: flex; justify-content: space-between; align-items: flex-start; gap: 28px; }
.project-heading > div:first-child { min-width: 0; flex: 1; }
.eyebrow { margin: 0 0 8px; color: var(--project-muted); font-size: 11px; font-weight: 600; letter-spacing: .075em; text-transform: uppercase; line-height: 1.5; }
.project-heading h1 { margin: 0; font-size: clamp(25px, 3vw, 34px); line-height: 1.25; font-weight: 600; letter-spacing: -.025em; overflow-wrap: anywhere; }
.project-description { max-width: 740px; margin: 12px 0 0; color: var(--project-muted); font-size: 14px; line-height: 1.65; overflow-wrap: anywhere; }
.heading-aside { display: flex; flex-direction: column; align-items: flex-end; gap: 16px; min-width: 0; flex-shrink: 0; }
.heading-actions { display: flex; flex-direction: column; align-items: flex-end; gap: 14px; flex-shrink: 0; }
.heading-actions .q-btn { font-size: 12px; }
.heading-action-buttons { display: flex; flex-wrap: wrap; justify-content: flex-end; gap: 10px; min-width: 0; }
.model-summary-launcher { border-radius: 7px; font-weight: 600; }
.model-summary-launcher:focus-visible { outline: 2px solid var(--project-brand); outline-offset: 3px; }
.model-summary-launcher-score { margin-left: 8px; padding: 1px 6px; border-radius: 4px; background: rgba(255, 255, 255, .16); font-weight: 700; font-variant-numeric: tabular-nums; }
.model-summary-popup { width: min(520px, calc(100vw - 24px)); border-radius: 10px; }
.project-status { display: inline-flex; align-items: center; gap: 8px; border: 1px solid var(--project-border); padding: 6px 10px; border-radius: 6px; font-size: 11px; font-weight: 500; background: var(--project-surface); color: var(--project-muted); }
.project-status > span { width: 6px; height: 6px; border-radius: 50%; background: currentColor; }
.status-success { color: var(--q-primary); background: var(--project-tint); border-color: var(--project-selection-surface); }
.status-running { color: var(--q-primary); border-color: var(--project-brand); }
.status-error { color: var(--project-error-ink); background: var(--project-error-surface); border-color: var(--project-error-border); }
.training-live { padding: 18px 22px; margin-top: 22px; background: var(--project-tint); border: 1px solid var(--project-border); border-radius: 8px; }
.live-heading { display: flex; align-items: flex-start; gap: 12px; margin-bottom: 12px; }
.live-heading > div { min-width: 0; flex: 1; }
.live-heading h2 { font-size: 15px; line-height: 1.5; font-weight: 600; margin: 0; }
.live-heading p { font-size: 12px; line-height: 1.6; color: var(--project-muted); margin: 5px 0 0; }
.live-icon { color: var(--q-primary); padding-top: 1px; }
.live-heading .q-btn { font-size: 11px; }
.training-progress { min-height: 30px; }
.training-progress-label { max-width: calc(100% - 16px); white-space: normal; text-align: center; font-size: 11px; line-height: 1.3; }
.training-alert { display: flex; align-items: flex-start; gap: 12px; margin-top: 22px; padding: 16px; border: 1px solid var(--project-error-border); background: var(--project-error-surface); border-radius: 8px; color: var(--project-error-ink); font-size: 13px; }
.training-alert > div { flex: 1; min-width: 0; }
.training-alert p { margin: 6px 0 0; line-height: 1.6; }
.project-tabs { margin-top: 28px; border-bottom: 1px solid var(--project-border); background: var(--project-surface); }
.project-tabs :deep(.q-tab) { min-height: 52px; padding: 0 22px; font-size: 13px; }
.project-tabs :deep(.q-tab__label) { font-size: 13px; }
.project-tabs :deep(.q-tab--active) { font-weight: 600; }
.tab-count { display: inline-block; padding: 1px 6px; margin-left: 5px; border-radius: 4px; background: var(--project-paper); color: var(--project-muted); font-size: 10px; }
.workspace-content { padding-top: 24px; }
.project-overview { display: grid; grid-template-columns: minmax(0, 2.1fr) minmax(270px, 1fr); align-items: start; gap: 24px; }
.overview-main { min-width: 0; }
.result-panel, .result-empty { border: 1px solid var(--project-border); background: var(--project-surface); border-radius: 10px; overflow: hidden; box-shadow: 0 5px 20px var(--project-shadow); }
.section-heading { display: flex; align-items: flex-start; justify-content: space-between; gap: 16px; padding: 24px 26px 12px; }
.section-heading > div { min-width: 0; }
.section-heading .eyebrow { color: var(--q-primary); }
.section-heading h2, .result-empty h2 { margin: 0; font-size: 22px; font-weight: 600; line-height: 1.4; letter-spacing: -.02em; overflow-wrap: anywhere; }
.section-heading > div > p:last-child { margin: 7px 0 0; font-size: 12px; color: var(--project-muted); line-height: 1.6; }
.result-provenance { display: flex; flex-wrap: wrap; align-items: center; gap: 6px; margin: 0 26px; color: var(--project-muted); font-size: 11px; }
.historical-result { margin-left: 6px; padding: 2px 6px; background: var(--project-paper); border-radius: 4px; }
.result-scoreboard { display: grid; grid-template-columns: minmax(0, 1.2fr) minmax(0, 1fr); gap: 24px; margin: 24px 26px; }
.main-score, .test-score { min-width: 0; display: flex; flex-direction: column; align-items: flex-start; }
.test-score { padding-left: 24px; border-left: 1px solid var(--project-border); }
.score-source { font-size: 11px; color: var(--project-muted); line-height: 1.6; }
.main-score > strong { font-size: clamp(26px, 3.7vw, 44px); line-height: 1.25; color: var(--q-primary); font-weight: 600; margin: 9px 0 6px; font-variant-numeric: tabular-nums; overflow-wrap: anywhere; max-width: 100%; }
.score-metric { font-size: 13px; font-weight: 500; line-height: 1.6; overflow-wrap: anywhere; }
.metric-direction { font-size: 11px; line-height: 1.6; margin-top: 6px; color: var(--project-muted); }
.test-score > strong { display: block; font-size: 25px; font-weight: 500; line-height: 1.4; margin: 10px 0 7px; font-variant-numeric: tabular-nums; overflow-wrap: anywhere; }
.test-score > strong.score-unavailable { font-size: 18px; }
.test-score p { font-size: 11px; color: var(--project-muted); line-height: 1.7; margin: 0; }
.additional-metrics { border-top: 1px solid var(--project-border); }
.additional-metrics-heading { display: flex; align-items: center; justify-content: space-between; flex-wrap: wrap; gap: 8px; padding: 9px 26px; }
.additional-metrics-heading > span { font-size: 11px; color: var(--project-muted); }
.additional-metrics-heading :deep(.q-toggle__label) { font-size: 12px; font-weight: 500; }
.additional-metrics-content { padding: 0 26px 15px; }
.additional-metrics-table { width: 100%; border-collapse: collapse; table-layout: fixed; font-size: 12px; }
.additional-metrics-table th, .additional-metrics-table td { padding: 10px 8px; overflow-wrap: anywhere; border-bottom: 1px solid var(--project-border); }
.additional-metrics-table th:first-child { width: 44%; padding-left: 0; text-align: left; }
.additional-metrics-table th:not(:first-child), .additional-metrics-table td { width: 28%; text-align: right; }
.additional-metrics-table thead { color: var(--project-muted); font-size: 10px; }
.additional-metrics-table thead th { font-weight: 500; }
.additional-metrics-table tbody th { font-weight: 500; }
.additional-metrics-table td { font-weight: 600; font-variant-numeric: tabular-nums; }
.additional-metrics-table small { display: block; margin-top: 3px; font-size: 10px; color: var(--project-muted); font-weight: 400; }
.metric-definitions-link { margin-top: 12px; font-size: 11px; max-width: 100%; }
.metric-definitions-link :deep(.q-btn__content) { white-space: normal; text-align: left; }
.sr-only { position: absolute; width: 1px; height: 1px; padding: 0; margin: -1px; overflow: hidden; clip: rect(0, 0, 0, 0); white-space: nowrap; border: 0; }
.reference-comparison { padding: 12px 26px; background: var(--project-paper); border-top: 1px solid var(--project-border); }
.reference-heading { display: flex; align-items: baseline; justify-content: space-between; flex-wrap: wrap; gap: 3px 12px; }
.reference-heading h3 { font-size: 13px; font-weight: 600; line-height: 1.5; margin: 0; letter-spacing: -.01em; }
.reference-criterion { display: flex; align-items: baseline; flex-wrap: wrap; gap: 4px 12px; color: var(--project-muted); font-size: 10px; line-height: 1.6; }
.reference-criterion > span:first-child { color: var(--project-ink); font-weight: 500; overflow-wrap: anywhere; }
.reference-criterion > span + span { padding-left: 12px; border-left: 1px solid var(--project-border); }
.reference-scores { display: grid; grid-template-columns: repeat(2, minmax(0, 1fr)); gap: 16px; margin-top: 10px; }
.reference-scores--comparable { grid-template-columns: repeat(3, minmax(0, 1fr)); }
.reference-score { min-width: 0; display: flex; flex-direction: column; align-items: flex-start; }
.reference-score + .reference-score { padding-left: 16px; border-left: 1px solid var(--project-border); }
.reference-score-label { color: var(--project-muted); font-size: 11px; font-weight: 500; line-height: 1.5; }
.reference-score--current .reference-score-label { color: var(--q-primary); font-weight: 600; }
.reference-score strong { max-width: 100%; margin-top: 3px; font-size: 20px; line-height: 1.3; font-weight: 600; font-variant-numeric: tabular-nums; letter-spacing: -.025em; overflow-wrap: anywhere; }
.reference-change-label { min-width: 0; display: flex; align-items: center; gap: 6px; font-size: 11px; line-height: 1.5; font-weight: 600; }
.reference-change-label .q-icon { flex-shrink: 0; }
.reference-difference--gain { color: var(--q-primary); }
.reference-difference--loss { color: var(--project-error-ink); }
.reference-unavailable { display: flex; align-items: flex-start; gap: 7px; margin-top: 10px; color: var(--project-muted); }
.reference-unavailable > .q-icon, .reference-unavailable > .q-spinner { flex-shrink: 0; margin-top: 1px; }
.reference-unavailable p { margin: 0; font-size: 11px; line-height: 1.6; overflow-wrap: anywhere; }
.reference-footer { display: flex; align-items: center; justify-content: space-between; flex-wrap: wrap; gap: 3px 14px; margin-top: 8px; }
.reference-context { margin: 0; color: var(--project-muted); font-size: 10px; line-height: 1.6; }
.reference-details-link { font-size: 11px; padding: 0; max-width: 100%; }
.reference-details-link :deep(.q-btn__content) { white-space: normal; text-align: left; }
.result-actions { display: flex; align-items: center; flex-wrap: wrap; gap: 10px; padding: 0 26px 22px; }
.result-actions .q-btn { font-size: 12px; max-width: 100%; }
.result-actions :deep(.q-btn__content) { white-space: normal; }
.overview-note { display: flex; align-items: flex-start; gap: 9px; margin-top: 16px; color: var(--project-muted); }
.overview-note .q-icon { flex-shrink: 0; margin-top: 2px; }
.overview-note p { font-size: 11px; line-height: 1.75; margin: 0; }
.project-explore { border: 1px solid var(--project-border); border-radius: 10px; background: var(--project-surface); padding: 22px 18px 12px; }
.project-explore > .eyebrow { margin-left: 8px; }
.project-explore h2 { margin: 0 8px 14px; font-size: 17px; font-weight: 600; line-height: 1.5; }
.explore-link { width: 100%; color: var(--project-ink); padding: 16px 8px; border-top: 1px solid var(--project-border); border-radius: 0; }
.explore-link :deep(.q-btn__content) { flex-wrap: nowrap; align-items: flex-start; gap: 10px; }
.explore-link :deep(.q-btn__content) > span:nth-child(2) { min-width: 0; flex: 1; text-align: left; }
.explore-icon { flex-shrink: 0; color: var(--q-primary); margin-top: 2px; }
.explore-link strong { display: block; font-size: 12px; font-weight: 600; line-height: 1.5; }
.explore-link small { display: block; font-size: 11px; color: var(--project-muted); line-height: 1.7; margin-top: 4px; }
.explore-link :deep(.q-btn__content) > .q-icon { flex-shrink: 0; margin-top: 2px; color: var(--project-muted); }
.result-empty { padding: 32px 26px; }
.empty-symbol { display: grid; place-items: center; width: 62px; height: 62px; color: var(--q-primary); background: var(--project-tint); border-radius: 12px; margin-bottom: 24px; }
.result-empty > p:not(.eyebrow) { margin: 14px 0 20px; color: var(--project-muted); font-size: 13px; line-height: 1.8; }
.result-empty .q-btn { font-size: 12px; }
.restart-card { width: 520px; max-width: calc(100vw - 32px); border: 1px solid var(--project-border); color: var(--project-ink); border-radius: 10px; }
.restart-heading { display: flex; align-items: flex-start; justify-content: space-between; gap: 12px; padding: 22px 24px; border-bottom: 1px solid var(--project-border); background: var(--project-paper); }
.restart-heading h2 { font-size: 20px; margin: 0; line-height: 1.5; font-weight: 600; }
.restart-body { padding: 22px 24px; }
.restart-body > p:first-child { font-size: 13px; color: var(--project-muted); line-height: 1.7; margin: 0 0 20px; }
.restart-adapt { margin-top: 20px; font-size: 12px; padding: 4px 0; }
.restart-adapt :deep(.q-btn__content) { white-space: normal; text-align: left; }
.restart-card :deep(.q-card__actions) { padding: 12px 24px 22px; flex-wrap: wrap; gap: 8px; }
.restart-card :deep(.q-field__bottom) { line-height: 1.5; }
.download-error { color: var(--project-error-ink); font-size: 12px; line-height: 1.7; overflow-wrap: anywhere; }
@media (max-width: 1023px) {
  .project-workspace { padding: 16px 24px 36px; }
  .project-overview { grid-template-columns: minmax(0, 1fr); gap: 20px; }
  .project-explore { display: grid; grid-template-columns: repeat(2, minmax(0, 1fr)); column-gap: 18px; }
  .project-explore > .eyebrow, .project-explore h2 { grid-column: 1 / -1; }
}
@media (max-width: 599px) {
  .project-workspace { padding: 10px 16px 32px; }
  .workspace-topline { margin-bottom: 20px; }
  .project-heading { flex-direction: column; gap: 18px; }
  .heading-aside { width: 100%; align-items: flex-start; }
  .heading-actions { flex-direction: row; flex-wrap: wrap; align-items: center; gap: 12px; }
  .heading-action-buttons { justify-content: flex-start; }
  .model-summary-launcher-score { display: none; }
  .project-status { font-size: 10px; }
  .project-tabs { margin-top: 22px; }
  .project-tabs :deep(.q-tabs__content) { display: grid; grid-template-columns: repeat(2, minmax(0, 1fr)); overflow: visible; }
  .project-tabs :deep(.q-tabs__arrow) { display: none; }
  .project-tabs :deep(.q-tab) { padding: 0 13px; }
  .project-tabs :deep(.q-tab__label) { font-size: 12px; }
  .workspace-content { padding-top: 18px; }
  .section-heading { padding: 20px 18px 12px; }
  .section-heading h2 { font-size: 20px; }
  .result-provenance { margin: 0 18px; }
  .result-scoreboard { margin: 20px 18px; gap: 16px; }
  .test-score { padding-left: 16px; }
  .main-score > strong { font-size: 30px; }
  .test-score > strong { font-size: 23px; }
  .test-score > strong.score-unavailable { font-size: 15px; }
  .additional-metrics-heading { padding: 9px 18px; }
  .additional-metrics-content { padding: 0 18px 15px; }
  .additional-metrics-table th, .additional-metrics-table td { padding-right: 0; }
  .reference-comparison { padding: 12px 18px; }
  .reference-scores { grid-template-columns: repeat(2, minmax(0, 1fr)); gap: 8px 12px; }
  .reference-score + .reference-score { padding-left: 12px; }
  .reference-score strong { font-size: 18px; }
  .reference-score.reference-difference { grid-column: 1 / -1; flex-direction: row; align-items: baseline; justify-content: space-between; gap: 10px; padding-left: 0; border-left: 0; }
  .reference-difference > strong { margin-top: 0; font-size: 17px; }
  .result-actions { padding: 0 18px 20px; flex-direction: column; align-items: stretch; }
  .result-actions .q-btn { width: 100%; }
  .project-explore { display: block; padding: 20px 12px 8px; }
  .training-live { padding: 16px 14px; }
  .live-heading { flex-wrap: wrap; gap: 9px; }
  .live-heading .q-btn { margin-left: 31px; }
  .training-alert { flex-wrap: wrap; }
  .training-alert .q-btn { margin-left: 32px; }
  .workspace-error { flex-wrap: wrap; }
}
</style>
