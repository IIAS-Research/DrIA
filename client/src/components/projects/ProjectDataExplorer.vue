<script setup lang="ts">
import { computed, onUnmounted, reactive, ref, watch } from 'vue';
import { IProject, IStatistics } from 'src/components/models';
import ProjectDataPreview from 'components/home/ProjectDataPreview.vue';
import ProjectDataSummary from 'components/home/ProjectDataSummary.vue';
import ProjectDataPlots from 'components/home/ProjectDataPlots.vue';
import { getProjectDatasetFile, ProjectDatasetSource } from 'src/api/projects.api';
import { exploreStatistics } from 'src/api/ia-web-service.api';
import { apiError } from 'src/utils/iaml';
import { ProjectColumnAnnotation, ProjectDataset, readProjectCsv } from 'src/utils/project-data';
import { inferPredictionKind, PredictionKind } from 'src/utils/project-study';
import { NativeDataPlotPlan } from 'src/utils/project-native-plots';

const props = defineProps<{ project: IProject }>();
interface DatasetState {
  file?: File;
  dataset?: ProjectDataset;
  loading: boolean;
  error: string;
  statistics?: IStatistics;
  statisticsKey?: string;
  statisticsLoading: boolean;
  statisticsError: string;
}
const createState = (): DatasetState => ({ loading: false, error: '', statisticsLoading: false, statisticsError: '' });
const cache = reactive<Record<ProjectDatasetSource, DatasetState>>({ original: createState(), train: createState(), test: createState() });
const source = ref<ProjectDatasetSource>('original');
const dataView = ref('preview');
const current = computed(() => cache[source.value]);
const dataset = computed(() => current.value.dataset);
const creationContext = computed<Record<string, unknown>>(() => {
  try {
    const context = JSON.parse(props.project.creationContextJson || '{}');
    return context && typeof context === 'object' && !Array.isArray(context) ? context : {};
  } catch { return {}; }
});
const groupColumns = computed(() => props.project.rowDepedencies ? props.project.rowDepedenciesLinked ?? [] : []);
const excludedColumns = computed(() => props.project.datasetHasIdColumn ? props.project.datasetHasIdColumnList ?? [] : []);
const timeColumn = computed(() => props.project.survivalModelDatasetTargetTime || '');
const eventValue = computed(() => props.project.survivalModelEventValue || '');
const timeUnit = computed(() => typeof creationContext.value.timeUnit === 'string' ? creationContext.value.timeUnit : '');
const predictionKind = computed<PredictionKind>(() => {
  const saved = creationContext.value.predictionKind;
  if (props.project.survivalModel || saved === 'survival') return 'survival';
  if (saved === 'category' || saved === 'quantity') return saved;
  // Older projects did not record the target kind. Keep the interpretation
  // anchored to the initial file when exploring smaller train/test partitions.
  const profile = (cache.original.dataset ?? dataset.value)?.profiles[props.project.csvColumnToPredict];
  return profile ? inferPredictionKind(profile) ?? 'category' : 'category';
});
const roles = computed<Record<string, ProjectColumnAnnotation>>(() => {
  const reasons = creationContext.value.exclusionReasons as Record<string, unknown> | undefined;
  return Object.fromEntries((dataset.value?.columns ?? []).map((column) => {
    let annotation: ProjectColumnAnnotation;
    if (column === props.project.csvColumnToPredict) {
      annotation = predictionKind.value === 'survival'
        ? { role: 'event', label: 'Événement', reason: 'Cette colonne indique si l’événement a été observé.' }
        : { role: 'target', label: 'À prédire', reason: 'Le modèle apprend à prédire cette colonne.' };
    } else if (predictionKind.value === 'survival' && column === timeColumn.value) {
      annotation = { role: 'time', label: 'Durée de suivi', reason: timeUnit.value ? `Durée exprimée en ${timeUnit.value}.` : 'Cette colonne contient la durée de suivi.' };
    } else if (groupColumns.value.includes(column)) {
      annotation = { role: 'group', label: 'Regroupement patient', reason: 'Ces observations restent ensemble lors des découpages. Cette colonne ne sert pas à prédire.' };
    } else if (excludedColumns.value.includes(column)) {
      annotation = reasons?.[column] === 'unavailable'
        ? { role: 'unavailable', label: 'Indisponible — exclu', reason: 'Cette information ne sera pas connue au moment de la prédiction.' }
        : { role: 'identifier', label: reasons?.[column] === 'identifier' ? 'Identifiant — exclu' : 'Exclue', reason: reasons?.[column] === 'identifier' ? 'Cet identifiant est exclu de l’apprentissage.' : 'Cette colonne est exclue de l’apprentissage. Le motif n’a pas été enregistré.' };
    } else {
      annotation = { role: 'predictor', label: 'Pour prédire', reason: 'Cette information est disponible pour la prédiction.' };
    }
    return [column, annotation];
  }));
});
const predictorColumns = computed(() => (dataset.value?.columns ?? []).filter((column) => roles.value[column]?.role === 'predictor'));
const statisticsExclusions = computed(() => (dataset.value?.columns ?? []).filter((column) => ['identifier', 'unavailable'].includes(roles.value[column]?.role)));
const sourceOptions = computed(() => [
  { label: 'Fichier initial', value: 'original' },
  { label: 'Apprentissage', value: 'train' },
  ...(props.project.evaluationMode !== 'cross_validation' ? [{ label: 'Test indépendant', value: 'test' }] : []),
]);
const sourceDescription = computed(() => source.value === 'original'
  ? props.project.evaluationMode === 'external' ? 'Le fichier d’apprentissage importé lors de la création du projet.' : 'Le fichier importé lors de la création du projet, avant tout découpage.'
  : source.value === 'train' ? 'Les observations utilisées pour apprendre et comparer les modèles.'
    : 'Les observations réservées à l’évaluation finale du modèle.');
const statisticsBlockedReason = computed(() => {
  const data = dataset.value;
  if (!data?.isComplete || !current.value.file) return 'Les analyses seront disponibles une fois le fichier entièrement lu.';
  const target = data.profiles[props.project.csvColumnToPredict];
  if (!target) return 'La colonne à prédire enregistrée dans le projet est absente de ce fichier.';
  if (target.missingCount) return 'Le résultat à prédire contient des valeurs manquantes. Les analyses DrIA ne peuvent pas être calculées sur ce fichier.';
  if (target.nonFiniteCount) return 'Le résultat à prédire contient des valeurs non finies.';
  if (predictionKind.value === 'survival') {
    let event: unknown;
    try { event = JSON.parse(eventValue.value); } catch { return 'La valeur correspondant à l’événement observé n’a pas été enregistrée.'; }
    if (target.distinctValuesTruncated || target.distinctValues.length > 2) return 'La colonne d’événement doit distinguer l’événement observé et l’événement non observé.';
    if (!target.distinctValues.some((value) => value === event)) return 'La valeur de l’événement observé est absente de ce fichier.';
    const time = data.profiles[timeColumn.value];
    if (!time || timeColumn.value === props.project.csvColumnToPredict) return 'La colonne de durée de suivi enregistrée dans le projet est absente ou incorrecte.';
    if (time.type !== 'numeric' || time.missingCount || time.nonFiniteCount || (time.min ?? -1) < 0) return 'La durée de suivi doit être renseignée, finie et positive ou nulle pour chaque observation.';
  }
  if (!predictorColumns.value.length) return 'Ce fichier ne contient aucune information utilisable pour prédire.';
  if (groupColumns.value.some((column) => !data.profiles[column])) return 'Une colonne de regroupement enregistrée dans le projet est absente de ce fichier.';
  if (groupColumns.value.some((column) => data.profiles[column].missingCount || data.profiles[column].nonFiniteCount)) return 'Une colonne de regroupement contient des valeurs manquantes ou non finies.';
  if (predictorColumns.value.some((column) => data.profiles[column].nonFiniteCount)) return 'Une information retenue pour prédire contient des valeurs non finies.';
  return '';
});
const analysisContext = computed(() => JSON.stringify([
  props.project.id, source.value, dataset.value?.isComplete ?? false,
  props.project.csvColumnToPredict, predictionKind.value, timeColumn.value, eventValue.value,
  groupColumns.value, statisticsExclusions.value,
]));

let datasetController: AbortController | undefined;
let statisticsController: AbortController | undefined;
function cancelStatistics() {
  statisticsController?.abort();
  statisticsController = undefined;
  Object.values(cache).forEach((entry) => { entry.statisticsLoading = false; });
}
async function loadDataset() {
  datasetController?.abort();
  cancelStatistics();
  Object.values(cache).forEach((entry) => { entry.loading = false; });
  const entry = current.value;
  if (entry.dataset?.isComplete) return;
  const controller = new AbortController();
  datasetController = controller;
  entry.loading = true;
  entry.error = '';
  try {
    const file = entry.file ?? await getProjectDatasetFile(props.project.id, source.value, controller.signal);
    if (controller.signal.aborted) return;
    entry.file = file;
    entry.dataset = await readProjectCsv(file, {
      signal: controller.signal,
      onPreview: (preview) => { if (!controller.signal.aborted) entry.dataset = preview; },
    });
  } catch (exception) {
    if (!controller.signal.aborted) { entry.dataset = undefined; entry.error = apiError(exception); }
  } finally {
    if (datasetController === controller) { datasetController = undefined; entry.loading = false; }
  }
}
function statisticsBody(): FormData | undefined {
  if (!current.value.file || statisticsBlockedReason.value) return;
  const body = new FormData();
  body.set('train_dataset', current.value.file);
  body.set('dataset_target', props.project.csvColumnToPredict);
  body.set('configuration', JSON.stringify({ schema_version: 1 }));
  if (predictionKind.value === 'survival') {
    body.set('dataset_target_time', timeColumn.value);
    body.set('event_value', eventValue.value);
  }
  if (groupColumns.value.length) body.set('groups_columns', JSON.stringify(groupColumns.value));
  if (statisticsExclusions.value.length) body.set('excluded_columns', JSON.stringify(statisticsExclusions.value));
  return body;
}
async function loadStatistics(force = false) {
  const body = statisticsBody();
  const entry = current.value;
  const key = analysisContext.value;
  if (!body || entry.statisticsLoading || (!force && entry.statistics && entry.statisticsKey === key)) return;
  const controller = new AbortController();
  statisticsController = controller;
  entry.statisticsLoading = true;
  entry.statisticsError = '';
  try {
    const statistics = await exploreStatistics(body, false, controller.signal);
    if (!controller.signal.aborted && key === analysisContext.value) { entry.statistics = statistics; entry.statisticsKey = key; }
  } catch (exception) {
    if (!controller.signal.aborted && key === analysisContext.value) entry.statisticsError = apiError(exception);
  } finally {
    if (statisticsController === controller) { statisticsController = undefined; entry.statisticsLoading = false; }
  }
}
async function loadNativePlot(plan: NativeDataPlotPlan, signal: AbortSignal): Promise<IStatistics> {
  const body = statisticsBody();
  const data = dataset.value;
  if (!body || !data || !plan.columns.length || plan.columns.some((column) => !predictorColumns.value.includes(column)))
    throw new Error(statisticsBlockedReason.value || 'Cette sélection de données n’est plus disponible.');
  const retained = new Set([...plan.columns, props.project.csvColumnToPredict, ...groupColumns.value,
    ...(predictionKind.value === 'survival' ? [timeColumn.value] : [])]);
  body.set('excluded_columns', JSON.stringify(data.columns.filter((column) => !retained.has(column))));
  body.set('configuration', JSON.stringify({ schema_version: 1, statistics: plan.statistics }));
  return exploreStatistics(body, true, signal);
}
function downloadFile() {
  if (!current.value.file) return;
  const url = URL.createObjectURL(current.value.file);
  const anchor = document.createElement('a');
  anchor.href = url;
  anchor.download = current.value.file.name;
  anchor.click();
  setTimeout(() => URL.revokeObjectURL(url), 1000);
}
watch(() => props.project.id, () => {
  datasetController?.abort();
  cancelStatistics();
  Object.keys(cache).forEach((key) => { cache[key as ProjectDatasetSource] = createState(); });
  source.value = 'original';
});
watch(() => props.project.evaluationMode, (mode) => { if (mode === 'cross_validation' && source.value === 'test') source.value = 'original'; });
watch([() => props.project.id, source], () => { void loadDataset(); }, { immediate: true });
watch(analysisContext, () => {
  cancelStatistics();
  if (current.value.statisticsKey !== analysisContext.value) current.value.statistics = undefined;
  current.value.statisticsError = '';
  void loadStatistics();
}, { immediate: true });
onUnmounted(() => { datasetController?.abort(); cancelStatistics(); });
</script>

<template>
  <section class="project-data-explorer" aria-labelledby="project-data-title">
    <header class="explorer-heading">
      <div><h2 id="project-data-title">Explorer les données</h2><p>Le tableau, le bilan de chaque colonne et les graphiques, avec les rôles enregistrés dans votre projet.</p></div>
      <q-btn outline no-caps color="primary" icon="download" label="Télécharger ce fichier" :disable="!current.file" @click="downloadFile" />
    </header>
    <div class="dataset-selector">
      <q-select v-model="source" outlined dense emit-value map-options label="Jeu de données" :options="sourceOptions" :popup-content-class="'project-workspace-colors'" />
      <p>{{ sourceDescription }}</p>
    </div>
    <div v-if="current.error" class="dataset-error" role="alert"><q-icon name="error_outline" size="21px" /><div><strong>Le fichier n’a pas pu être ouvert</strong><p>{{ current.error }}</p></div><q-btn flat no-caps color="primary" label="Réessayer" @click="loadDataset" /></div>
    <div v-else class="explorer-panel">
      <q-tabs v-model="dataView" no-caps align="left" active-color="primary" indicator-color="primary" class="explorer-tabs" aria-label="Consulter les données du projet">
        <q-tab name="preview" icon="table_chart" label="Vos données" />
        <q-tab name="summary" icon="analytics" label="Bilan des données" />
        <q-tab name="plots" icon="query_stats" label="Graphiques" />
      </q-tabs>
      <div v-if="current.loading && !dataset" class="dataset-loading" role="status"><q-spinner color="primary" size="26px" /><p>Ouverture et analyse du fichier…</p></div>
      <q-tab-panels v-else v-model="dataView" keep-alive class="explorer-panels">
        <q-tab-panel name="preview" class="q-pa-none"><ProjectDataPreview :dataset="dataset" :roles="roles" :group-columns="groupColumns" context="project" /></q-tab-panel>
        <q-tab-panel name="summary" class="q-pa-none">
          <ProjectDataSummary v-if="dataset" :dataset="dataset" :roles="roles" :target-column="project.csvColumnToPredict" :prediction-kind="predictionKind" :filename="current.file?.name" :group-columns="groupColumns" :time-unit="timeUnit" :event-value="eventValue" :statistics="current.statistics" :statistics-loading="current.statisticsLoading" :statistics-error="current.statisticsError" :statistics-unavailable-reason="statisticsBlockedReason" context="project" @retry-statistics="loadStatistics(true)" />
        </q-tab-panel>
        <q-tab-panel name="plots" class="q-pa-none">
          <ProjectDataPlots v-if="dataset" :dataset="dataset" :columns="predictorColumns" :statistics="current.statistics" :statistics-loading="current.statisticsLoading" :statistics-error="current.statisticsError" :prediction-kind="predictionKind" :target-column="project.csvColumnToPredict" :disabled="current.loading" :unavailable-reason="statisticsBlockedReason" :load-native-plot="loadNativePlot" :analysis-context="analysisContext" context="project" @retry-statistics="loadStatistics(true)" />
        </q-tab-panel>
      </q-tab-panels>
    </div>
  </section>
</template>

<style scoped>
.project-data-explorer { min-width: 0; color: var(--project-ink); }
.explorer-heading { display: flex; justify-content: space-between; align-items: flex-start; gap: 24px; margin-bottom: 24px; }
.explorer-heading > div { min-width: 0; }
.explorer-heading h2 { margin: 0; font-size: 22px; font-weight: 600; line-height: 1.4; }
.explorer-heading p { margin: 8px 0 0; max-width: 650px; color: var(--project-muted); font-size: 13px; line-height: 1.7; }
.explorer-heading .q-btn { flex-shrink: 0; font-size: 12px; }
.dataset-selector { display: flex; align-items: center; gap: 20px; margin-bottom: 20px; }
.dataset-selector .q-select { width: 240px; flex-shrink: 0; }
.dataset-selector p { margin: 0; color: var(--project-muted); font-size: 12px; line-height: 1.7; }
.explorer-panel { min-width: 0; }
.explorer-tabs { border-bottom: 1px solid var(--project-border); margin-bottom: 18px; }
.explorer-tabs :deep(.q-tab) { min-height: 56px; padding: 0 22px; }
.explorer-tabs :deep(.q-tab__content) { flex-direction: row; gap: 8px; }
.explorer-tabs :deep(.q-tab__icon) { font-size: 20px; }
.explorer-tabs :deep(.q-tab__label) { font-size: 13px; }
.explorer-panels { background: transparent; overflow: visible; }
.dataset-loading { display: flex; align-items: center; justify-content: center; gap: 12px; padding: 64px 20px; color: var(--project-muted); }
.dataset-loading p { margin: 0; font-size: 13px; }
.dataset-error { display: flex; align-items: flex-start; gap: 12px; border: 1px solid var(--project-border); border-radius: 10px; padding: 20px; background: var(--project-surface); font-size: 13px; }
.dataset-error > div { min-width: 0; flex: 1; overflow-wrap: anywhere; }
.dataset-error p { margin: 7px 0 0; color: var(--project-muted); line-height: 1.7; }
@media (max-width: 599px) {
  .explorer-heading { flex-direction: column; gap: 16px; }
  .dataset-selector { flex-direction: column; align-items: stretch; gap: 10px; }
  .dataset-selector .q-select { width: 100%; }
  .explorer-tabs :deep(.q-tab) { padding: 0 8px; }
  .explorer-tabs :deep(.q-tab__content) { flex-direction: column; gap: 3px; }
  .explorer-tabs :deep(.q-tab__label) { font-size: 11px; }
  .dataset-error { flex-wrap: wrap; }
}
</style>
