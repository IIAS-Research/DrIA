<script setup lang="ts">
import 'src/css/project-creation.scss';
import { computed, onUnmounted, ref, watch } from 'vue';
import { useRoute, useRouter } from 'vue-router';
import { IModel, IExplanation } from 'src/components/models';
import { getModeleById } from 'src/api/models.api';
import ModelHeader from 'src/components/models/ModelHeader.vue';
import ModelPerformance from 'src/components/models/ModelPerformance.vue';
import ModelExplanations from 'src/components/models/ModelExplanations.vue';
import ModelExplains from 'src/components/models/ModelExplains.vue';

type ModelView = 'performance' | 'explanation' | 'methodology';
const route = useRoute();
const router = useRouter();
const model = ref<IModel>();
const explanation = ref<IExplanation>();
const isLoading = ref(false);
const visitedExplanation = ref(false);
const visitedMethodology = ref(false);
let requestVersion = 0;
const modelId = computed(() => Number(route.params.modelId));
const routeParams = computed(() => ({
  projectId: Number(route.params.id),
  processId: Number(route.params.processId),
  modelId: modelId.value,
}));
const view = computed<ModelView>({
  get: () => ['performance', 'explanation', 'methodology'].includes(String(route.query.view))
    ? route.query.view as ModelView : 'performance',
  set: (next) => { void router.replace({ query: { ...route.query, view: next } }); },
});
const projectViews = ['overview', 'training', 'data', 'configuration'] as const;
type ProjectView = typeof projectViews[number];
const returnView = computed<ProjectView>(() => projectViews.includes(route.query.returnView as ProjectView)
  ? route.query.returnView as ProjectView : 'training');
const projectLocation = computed(() => ({
  name: 'project-show', params: { id: routeParams.value.projectId }, query: { view: returnView.value },
}));
const reports = computed(() => {
  const metrics = model.value?.metrics;
  if (!metrics) return [];
  return [
    { title: 'Metric report', value: metrics.metric_report },
    { title: 'Evaluation report', value: metrics.evaluation_report },
    { title: 'Analysis report', value: metrics.explanation_report },
    { title: 'Training audit', value: metrics.training_audit },
  ].filter((report) => report.value !== null && report.value !== undefined);
});

async function loadModel() {
  const version = ++requestVersion;
  model.value = undefined;
  explanation.value = undefined;
  visitedExplanation.value = view.value === 'explanation';
  visitedMethodology.value = view.value === 'methodology';
  isLoading.value = false;
  if (!Number.isInteger(modelId.value) || modelId.value < 1) return;
  isLoading.value = true;
  const next = await getModeleById(modelId.value);
  if (version !== requestVersion) return;
  model.value = next;
  isLoading.value = false;
}
watch(modelId, loadModel, { immediate: true });
watch(view, (next) => {
  if (next === 'explanation') visitedExplanation.value = true;
  if (next === 'methodology') visitedMethodology.value = true;
});
onUnmounted(() => { requestVersion += 1; });

function reportJson(value: unknown): string {
  return typeof value === 'string' ? value : JSON.stringify(value, null, 2);
}
</script>

<template>
  <q-page class="model-page project-workspace-colors">
    <div class="model-workspace">
      <nav class="model-breadcrumb" aria-label="Back to project">
        <q-btn flat no-caps dense icon="arrow_back" label="Back to project" :to="projectLocation" />
        <span v-if="model">Training run #{{ routeParams.processId }} · Model #{{ model.id }}</span>
      </nav>

      <template v-if="model">
        <ModelHeader :model="model" :route-params="routeParams" :return-view="returnView" />

        <q-tabs
          v-model="view"
          no-caps
          align="left"
          active-color="primary"
          indicator-color="primary"
          class="model-tabs"
          aria-label="Explore the model"
        >
          <q-tab name="performance" label="Results" icon="assessment" />
          <q-tab name="explanation" label="Explainability" icon="psychology" />
          <q-tab name="methodology" label="Pipeline and methods" icon="schema" />
        </q-tabs>

        <section v-show="view === 'performance'" class="model-panel" aria-label="Model results">
          <ModelPerformance :model="model" :explanation="explanation" @request-analysis="view = 'explanation'" />
        </section>

        <section v-show="view === 'explanation'" class="model-panel" aria-label="Understand predictions">
          <div class="panel-heading">
            <span class="section-eyebrow">Understand predictions</span>
            <h2>Which features influence the model?</h2>
            <p>Explainability describes how the model uses features. It complements its performance evaluation.</p>
          </div>
          <div class="explanation-guidance">
            <q-icon name="info_outline" size="20px" />
            <p>Feature importance describes a feature’s influence on predictions. It does not establish a causal relationship.</p>
          </div>
          <div class="explanation-content">
            <ModelExplains
              v-if="visitedExplanation"
              :key="model.id"
              :model="model"
              @explanation-updated="explanation = $event"
            />
          </div>
        </section>

        <section v-show="view === 'methodology'" class="model-panel" aria-label="Model pipeline and methods">
          <div class="panel-heading">
            <span class="section-eyebrow">Traceability</span>
            <h2>How was this model built?</h2>
            <p>Explore the pipeline used by this model, its settings, method references and available reports.</p>
          </div>
          <ModelExplanations v-if="visitedMethodology" :key="model.id" :model="model" />
          <div v-if="reports.length" class="technical-reports">
            <h3>Detailed reports</h3>
            <p>The structured information saved by DrIA for this training run.</p>
            <q-list separator class="reports-list">
              <q-expansion-item v-for="report in reports" :key="report.title" :label="report.title" icon="description">
                <pre class="report-json">{{ reportJson(report.value) }}</pre>
              </q-expansion-item>
            </q-list>
          </div>
        </section>
      </template>

      <div v-else-if="isLoading" class="model-state" role="status">
        <q-spinner color="primary" size="32px" />
        <h1>Loading model</h1>
        <p>Its results and information are being retrieved.</p>
      </div>
      <div v-else class="model-state">
        <q-icon name="cloud_off" size="36px" />
        <h1>The model could not be loaded</h1>
        <p>Try again to view its results.</p>
        <q-btn outline no-caps color="primary" label="Try again" icon="refresh" @click="loadModel" />
      </div>
    </div>
  </q-page>
</template>

<style scoped lang="scss">
.model-page {
  padding: 28px 32px 48px;
  color: var(--project-ink);
}
.model-workspace { width: 100%; max-width: 1320px; min-width: 0; margin: 0 auto; }
.model-breadcrumb { display: flex; align-items: center; flex-wrap: wrap; gap: 12px; margin-bottom: 22px; color: var(--project-muted); font-size: 12px; }
.model-breadcrumb .q-btn { color: var(--q-primary); }
.model-breadcrumb span { overflow-wrap: anywhere; }
.model-tabs { margin-top: 28px; border-bottom: 1px solid var(--project-border); color: var(--project-muted); }
.model-tabs :deep(.q-tab) { min-height: 58px; padding: 0 20px; }
.model-tabs :deep(.q-tab__content) { flex-direction: row; gap: 9px; }
.model-tabs :deep(.q-tab__icon) { width: 20px; height: 20px; font-size: 20px; }
.model-tabs :deep(.q-tab__label) { font-size: 14px; font-weight: 600; }
.model-panel { padding-top: 28px; min-width: 0; }
.section-eyebrow { color: var(--q-primary); font-size: 11px; font-weight: 700; letter-spacing: .09em; text-transform: uppercase; }
.panel-heading h2 { font-size: 22px; line-height: 1.35; font-weight: 600; margin: 8px 0; }
.panel-heading p { font-size: 14px; line-height: 1.6; color: var(--project-muted); margin: 0; max-width: 780px; }
.explanation-guidance { display: flex; align-items: flex-start; gap: 10px; margin: 20px 0; padding: 14px 16px; background: var(--project-tint); border-radius: 10px; }
.explanation-guidance .q-icon { color: var(--q-primary); flex: 0 0 auto; margin-top: 1px; }
.explanation-guidance p { margin: 0; color: var(--project-muted); font-size: 13px; line-height: 1.5; }
.explanation-content { min-width: 0; }
.technical-reports { margin-top: 26px; }
.technical-reports h3 { font-size: 18px; line-height: 1.4; margin: 0 0 6px; font-weight: 600; }
.technical-reports > p { margin: 0 0 14px; font-size: 13px; color: var(--project-muted); }
.reports-list { border: 1px solid var(--project-border); border-radius: 12px; background: var(--project-surface); overflow: hidden; }
.report-json { margin: 0; padding: 20px; max-height: 480px; overflow: auto; background: var(--project-paper); font-size: 12px; line-height: 1.6; white-space: pre-wrap; overflow-wrap: anywhere; }
.model-state { padding: 80px 20px; text-align: center; }
.model-state h1 { font-size: 22px; line-height: 1.4; font-weight: 600; margin: 16px 0 8px; }
.model-state p { margin: 0 0 20px; color: var(--project-muted); }
@media (max-width: 700px) {
  .model-page { padding: 20px 16px 36px; }
  .model-breadcrumb { margin-bottom: 18px; gap: 8px; }
  .model-tabs { margin-top: 22px; }
  .model-tabs :deep(.q-tab) { padding: 0 12px; }
  .model-tabs :deep(.q-tab__content) { gap: 6px; }
  .model-tabs :deep(.q-tab__label) { font-size: 12px; }
  .model-tabs :deep(.q-tab__icon) { display: none; }
  .model-panel { padding-top: 22px; }
  .panel-heading h2 { font-size: 20px; }
}
</style>
