<script setup lang="ts">
import { computed, ref, watch } from 'vue';
import { useQuasar } from 'quasar';
import { IModel, IPlot, IExplanation } from '../models';
import { formatScore as score, metricKey } from 'src/utils/iaml';
import { displayServiceName } from 'src/utils/service-label';

const props = defineProps<{ model: IModel; explanation?: IExplanation }>();
const emit = defineEmits<{ (event: 'request-analysis'): void }>();
const $q = useQuasar();
const selectedPlot = ref(0);
const enlargedPlot = ref(false);
const metrics = computed(() => props.model.metrics);
const objective = computed(() => metricKey(metrics.value));
const searchSource = computed(() => {
  if (metrics.value.score_source === 'cross_validation') return 'Cross-validation';
  if (metrics.value.score_source === 'in_sample') return 'Training';
  return 'Model search';
});
const searchDescription = computed(() => {
  if (metrics.value.score_source === 'cross_validation')
    return 'Cross-validation evaluates the model across multiple splits of the training data.';
  if (metrics.value.score_source === 'in_sample')
    return 'These scores are measured on the data used to train the model.';
  return 'The service does not specify the source of these search scores.';
});
const hasTest = computed(() => metrics.value.metrics_test !== null && metrics.value.metrics_test !== undefined);
const metricRows = computed(() => {
  const keys = [...new Set([
    ...Object.keys(metrics.value.metrics_info ?? {}),
    ...Object.keys(metrics.value.metrics_train ?? {}),
    ...Object.keys(metrics.value.metrics_test ?? {}),
  ])];
  return keys.sort((a, b) => Number(b === objective.value) - Number(a === objective.value)
    || a.localeCompare(b, 'en-US')).map((key) => ({
    key,
    label: displayServiceName(metrics.value.metrics_info?.[key]?.name || key),
    description: metrics.value.metrics_info?.[key]?.desc,
    search: metrics.value.metrics_train?.[key],
    test: metrics.value.metrics_test?.[key],
    greater: metrics.value.metrics_info?.[key]?.greater_is_better
      ?? (key === objective.value ? metrics.value.objective?.greater_is_better : undefined),
  }));
});
const objectiveRow = computed(() => metricRows.value.find((item) => item.key === objective.value));
const plots = computed<IPlot[]>(() => {
  const direct = metrics.value.plots?.filter((plot) => typeof plot.image === 'string' && plot.image.length) ?? [];
  if (direct.length) return direct;
  let explanation = props.explanation;
  if (!explanation && props.model.explanation) {
    try { explanation = JSON.parse(props.model.explanation) as IExplanation; }
    catch { return []; }
  }
  return Object.values(explanation?.results ?? {})
    .filter((result) => result.kind === 'plot' && typeof result.image === 'string' && result.image.length)
    .map((result) => ({ title: result.title || 'Model analysis', description: result.description || '', image: result.image! }));
});
const plotOptions = computed(() => plots.value.map((plot, index) => ({ label: plot.title || `Analysis ${index + 1}`, value: index })));
const currentPlot = computed(() => plots.value[selectedPlot.value]);
function imageUrl(plot: IPlot): string {
  return plot.image.startsWith('data:') ? plot.image : 'data:image/png;base64,' + plot.image;
}
watch(plots, () => { if (selectedPlot.value >= plots.value.length) selectedPlot.value = 0; });
watch(() => props.model.id, () => { selectedPlot.value = 0; enlargedPlot.value = false; });
</script>

<template>
  <div class="performance-content">
    <div class="performance-heading">
      <span class="section-eyebrow">Evaluate the model</span>
      <h2>How does it perform?</h2>
      <p>Search and test scores are shown separately so you can see which data was used to evaluate the model.</p>
    </div>

    <div class="metrics-card">
      <div v-if="objectiveRow" class="objective-heading">
        <div>
          <span class="objective-label">Primary metric</span>
          <h3>{{ objectiveRow.label }}</h3>
        </div>
        <span v-if="objectiveRow.greater !== undefined" class="objective-direction">
          <q-icon :name="objectiveRow.greater ? 'north_east' : 'south_east'" size="17px" />
          {{ objectiveRow.greater ? 'Higher is better' : 'Lower is better' }}
        </span>
      </div>
      <div class="metrics-table-scroll">
        <table class="metrics-table">
          <thead>
            <tr>
              <th scope="col">Metric</th>
              <th scope="col">{{ searchSource }}</th>
              <th scope="col">Holdout test set</th>
            </tr>
          </thead>
          <tbody>
            <tr v-for="item in metricRows" :key="item.key" :class="{ 'objective-row': item.key === objective }">
              <th scope="row">
                <span class="metric-name">{{ item.label }}</span>
                <span v-if="item.key === objective" class="metric-objective">Primary metric</span>
                <span v-if="item.description" class="metric-description">{{ item.description }}</span>
                <span v-if="item.greater !== undefined" class="metric-direction">{{ item.greater ? 'Maximize' : 'Minimize' }}</span>
              </th>
              <td :data-metric="item.key" data-source="search" :class="{ 'score-unavailable': typeof item.search !== 'number' || !Number.isFinite(item.search) }">{{ score(item.search) }}</td>
              <td :data-metric="item.key" data-source="test" :class="{ 'score-unavailable': typeof item.test !== 'number' || !Number.isFinite(item.test) }">{{ score(item.test) }}</td>
            </tr>
            <tr v-if="!metricRows.length"><td colspan="3" class="empty-metrics">No metrics are available for this model.</td></tr>
          </tbody>
        </table>
      </div>
      <div class="evaluation-notes">
        <p><strong>{{ searchSource }}.</strong> {{ searchDescription }}</p>
        <p v-if="hasTest"><strong>Holdout test set.</strong> Scores measured on the file reserved for final evaluation.</p>
        <p v-else><strong>Holdout test set.</strong> No holdout test scores are available for this model.</p>
      </div>
    </div>

    <div class="model-analysis-section">
      <div class="analysis-heading">
        <div>
          <span class="section-eyebrow">Result analysis</span>
          <h3>Explore performance in detail</h3>
        </div>
        <span v-if="plots.length" class="analysis-count">{{ plots.length }} {{ plots.length === 1 ? 'analysis' : 'analyses' }} available</span>
      </div>

      <template v-if="currentPlot">
        <div class="analysis-controls">
          <q-select
            v-model="selectedPlot"
            :options="plotOptions"
            emit-value
            map-options
            outlined
            dense
            label="Analysis to display"
            popup-content-class="project-workspace-colors"
            class="analysis-select"
          />
          <q-btn flat no-caps color="primary" icon="open_in_full" label="Enlarge" aria-label="Enlarge performance chart" @click="enlargedPlot = true" />
        </div>
        <figure class="performance-figure">
          <div class="plot-stage"><img :src="imageUrl(currentPlot)" :alt="currentPlot.title || 'Model performance chart'" /></div>
          <figcaption>
            <h4>{{ currentPlot.title || 'Model analysis' }}</h4>
            <p v-if="currentPlot.description">{{ currentPlot.description }}</p>
          </figcaption>
        </figure>
      </template>
      <div v-else class="analysis-empty">
        <q-icon name="insights" size="24px" />
        <div>
          <h4>No performance charts saved</h4>
          <p>Available scores are shown above. The Explainability tab lets you view or start model analyses.</p>
          <q-btn flat dense no-caps color="primary" label="View analyses" icon-right="arrow_forward" class="q-mt-sm" @click="emit('request-analysis')" />
        </div>
      </div>
    </div>

    <q-dialog v-model="enlargedPlot" :maximized="$q.screen.lt.sm">
      <q-card v-if="currentPlot" class="plot-dialog project-workspace-colors">
        <div class="plot-dialog-heading">
          <h3>{{ currentPlot.title || 'Model analysis' }}</h3>
          <q-btn v-close-popup flat round icon="close" aria-label="Close performance chart" />
        </div>
        <img :src="imageUrl(currentPlot)" :alt="currentPlot.title || 'Model performance chart'" />
        <p v-if="currentPlot.description">{{ currentPlot.description }}</p>
      </q-card>
    </q-dialog>
  </div>
</template>

<style scoped>
.performance-content { min-width: 0; }
.section-eyebrow { color: var(--q-primary); font-size: 11px; font-weight: 700; letter-spacing: .09em; text-transform: uppercase; }
.performance-heading h2 { margin: 8px 0; font-size: 22px; line-height: 1.35; font-weight: 600; }
.performance-heading > p { margin: 0; max-width: 820px; color: var(--project-muted); font-size: 14px; line-height: 1.6; }
.metrics-card { margin-top: 22px; border: 1px solid var(--project-border); border-radius: 14px; background: var(--project-surface); overflow: hidden; }
.objective-heading { display: flex; justify-content: space-between; align-items: center; gap: 16px; padding: 18px 22px; border-bottom: 1px solid var(--project-border); }
.objective-label { display: block; font-size: 11px; color: var(--project-muted); }
.objective-heading h3 { margin: 4px 0 0; font-size: 17px; line-height: 1.4; font-weight: 600; overflow-wrap: anywhere; }
.objective-direction { display: flex; align-items: center; gap: 5px; color: var(--q-primary); font-size: 12px; flex: 0 0 auto; }
.metrics-table-scroll { overflow-x: auto; max-width: 100%; }
.metrics-table { width: 100%; min-width: 460px; border-collapse: collapse; border-spacing: 0; }
.metrics-table th, .metrics-table td { padding: 16px 22px; border: 0; border-bottom: 1px solid var(--project-border); background: transparent; }
.metrics-table thead { background: var(--project-paper); }
.metrics-table thead th { font-size: 12px; font-weight: 600; color: var(--project-muted); text-align: right; vertical-align: bottom; }
.metrics-table thead th:first-child { text-align: left; }
.metrics-table tbody th { width: 54%; text-align: left; font-weight: 400; }
.metrics-table tbody td { width: 23%; text-align: right; white-space: nowrap; font-size: 17px; font-weight: 600; font-variant-numeric: tabular-nums; }
.metrics-table tbody tr:last-child th, .metrics-table tbody tr:last-child td { border-bottom: 0; }
.metrics-table .objective-row { background: var(--project-tint); }
.metric-name { display: inline; font-weight: 600; font-size: 14px; overflow-wrap: anywhere; }
.metric-objective { display: inline-block; margin-left: 8px; padding: 2px 6px; border: 1px solid var(--project-brand); border-radius: 4px; color: var(--q-primary); font-size: 10px; font-weight: 600; vertical-align: middle; }
.metric-description { display: block; margin-top: 5px; max-width: 680px; color: var(--project-muted); font-size: 12px; line-height: 1.5; }
.metric-direction { display: block; margin-top: 5px; color: var(--project-muted); font-size: 11px; }
.metrics-table tbody td.score-unavailable { font-size: 12px; font-weight: 400; color: var(--project-muted); }
.empty-metrics { text-align: center; color: var(--project-muted); font-size: 13px; }
.evaluation-notes { padding: 14px 22px; border-top: 1px solid var(--project-border); }
.evaluation-notes p { margin: 0; font-size: 12px; line-height: 1.6; color: var(--project-muted); }
.evaluation-notes p + p { margin-top: 5px; }
.evaluation-notes strong { font-weight: 600; color: var(--project-ink); }
.model-analysis-section { margin-top: 30px; }
.analysis-heading { display: flex; justify-content: space-between; align-items: center; flex-wrap: wrap; gap: 10px; }
.analysis-heading h3 { font-size: 19px; font-weight: 600; line-height: 1.4; margin: 5px 0 0; }
.analysis-count { font-size: 12px; color: var(--project-muted); }
.analysis-controls { display: flex; align-items: center; gap: 12px; margin: 18px 0 14px; }
.analysis-select { flex: 1 1 auto; max-width: 640px; min-width: 0; }
.analysis-controls .q-btn { flex: 0 0 auto; }
.performance-figure { margin: 0; background: var(--project-surface); border: 1px solid var(--project-border); border-radius: 14px; overflow: hidden; }
.plot-stage { display: flex; align-items: center; justify-content: center; padding: 24px; min-height: 260px; }
.plot-stage img { display: block; max-width: 100%; max-height: 640px; width: auto; height: auto; object-fit: contain; }
.performance-figure figcaption { padding: 18px 22px; border-top: 1px solid var(--project-border); }
.performance-figure h4 { margin: 0; font-size: 16px; font-weight: 600; line-height: 1.4; overflow-wrap: anywhere; }
.performance-figure p { margin: 7px 0 0; color: var(--project-muted); font-size: 13px; line-height: 1.6; overflow-wrap: anywhere; }
.analysis-empty { display: flex; gap: 13px; padding: 20px 22px; margin-top: 18px; border: 1px dashed var(--project-border); border-radius: 12px; }
.analysis-empty > .q-icon { color: var(--project-muted); flex-shrink: 0; }
.analysis-empty h4 { font-size: 14px; font-weight: 600; line-height: 1.4; margin: 0 0 6px; }
.analysis-empty p { margin: 0; color: var(--project-muted); font-size: 13px; line-height: 1.6; }
.plot-dialog { width: min(1100px, 95vw); max-width: 95vw; padding: 22px; color: var(--project-ink); border-radius: 14px; }
.plot-dialog-heading { display: flex; justify-content: space-between; align-items: center; gap: 16px; margin-bottom: 18px; }
.plot-dialog-heading h3 { margin: 0; font-size: 20px; line-height: 1.4; font-weight: 600; overflow-wrap: anywhere; }
.plot-dialog-heading .q-btn { flex-shrink: 0; }
.plot-dialog > img { display: block; max-width: 100%; max-height: 75vh; width: auto; height: auto; object-fit: contain; margin: 0 auto; }
.plot-dialog > p { font-size: 13px; line-height: 1.6; color: var(--project-muted); margin: 16px 0 0; }
@media (max-width: 700px) {
  .performance-heading h2 { font-size: 20px; }
  .objective-heading { padding: 16px; align-items: flex-start; flex-direction: column; gap: 9px; }
  .metrics-table { min-width: 0; table-layout: fixed; }
  .metrics-table th, .metrics-table td { padding: 14px 10px; overflow-wrap: anywhere; }
  .metrics-table thead th:first-child, .metrics-table tbody th { width: 43%; }
  .metrics-table thead th:not(:first-child), .metrics-table tbody td { width: 28.5%; }
  .metrics-table tbody td { font-size: 15px; white-space: normal; }
  .metric-objective { margin: 5px 0 0; display: table; }
  .evaluation-notes { padding: 14px 16px; }
  .analysis-controls { align-items: stretch; flex-direction: column; gap: 8px; }
  .analysis-controls .q-btn { align-self: flex-end; }
  .plot-stage { padding: 14px; min-height: 200px; }
  .performance-figure figcaption { padding: 16px; }
  .analysis-empty { padding: 16px; }
  .plot-dialog { max-width: 100vw; padding: 16px; border-radius: 0; }
}
</style>
