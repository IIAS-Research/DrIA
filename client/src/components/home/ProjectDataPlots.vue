<script setup lang="ts">
import { computed, onUnmounted, ref, watch } from 'vue';
import { useQuasar } from 'quasar';
import { IPlot, IStatistics } from '../models';
import { apiError } from 'src/utils/iaml';
import { ProjectDataset } from 'src/utils/project-data';
import { PredictionKind } from 'src/utils/project-study';
import {
  buildAssociationInsight, buildComparisonInsight, buildMissingInsight,
  comparisonColumns, ProjectAnalysisKind, ProjectInsight,
} from 'src/utils/project-plots';
import ProjectInsightChart from './ProjectInsightChart.vue';
import { nativeComparisonColumns, nativeComparisonPlan, nativeMissingPlan, selectNativeDataPlot } from 'src/utils/project-native-plots';
import type { NativeDataPlotPlan } from 'src/utils/project-native-plots';

const props = withDefaults(defineProps<{
  dataset: ProjectDataset;
  columns: string[];
  statistics?: IStatistics;
  statisticsLoading: boolean;
  statisticsError?: string;
  predictionKind: PredictionKind;
  targetColumn: string;
  disabled: boolean;
  unavailableReason: string;
  loadNativePlot: (plan: NativeDataPlotPlan, signal: AbortSignal) => Promise<IStatistics>;
  analysisContext: string;
  context?: 'creation' | 'project';
}>(), {
  context: 'creation',
});
const emit = defineEmits<{ (event: 'retry-statistics'): void }>();
const $q = useQuasar();
const selectedAnalysis = ref<ProjectAnalysisKind>('missing');
const userSelectedAnalysis = ref(false);
const selectedColumn = ref('');
const columnSearch = ref('');
const viewerOpen = ref(false);
const changed = ref(false);
const cache = ref<Record<string, ProjectInsight>>(Object.create(null));
const nativeCache = ref<Record<string, { plot: IPlot; plan: NativeDataPlotPlan; statistics: IStatistics }>>(Object.create(null));
const plotLoading = ref(false);
const plotError = ref('');
const imageFailed = ref(false);
const preferSynthesis = ref(false);
let plotController: AbortController | undefined;
let plotRevision = 0;
const number = new Intl.NumberFormat('fr-FR');

const missingInsight = computed(() => props.dataset.isComplete
  ? buildMissingInsight(props.dataset, props.columns) : null);
const nativeColumns = computed(() => nativeComparisonColumns(
  props.dataset, props.columns, props.targetColumn, props.predictionKind,
));
const comparableColumns = computed(() => [...new Set([
  ...nativeColumns.value,
  ...comparisonColumns(props.statistics, props.dataset, props.columns, props.predictionKind),
])]);
const missingPlan = computed(() => nativeMissingPlan(props.dataset, props.columns, props.targetColumn, props.predictionKind));
const associationInsight = computed(() => buildAssociationInsight(
  props.statistics, props.dataset, props.columns, props.targetColumn, props.predictionKind,
));
const choices = computed(() => [
  { value: 'missing' as const, label: 'Données manquantes', description: 'Quelles informations sont incomplètes ?', icon: 'fact_check', source: missingPlan.value ? 'Graphique original' : missingInsight.value ? 'Vue synthétique' : '' },
  ...(comparableColumns.value.length || (props.statisticsLoading && selectedAnalysis.value === 'comparison') ? [{ value: 'comparison' as const, label: 'Comparer les groupes du résultat', description: 'Comment une information varie-t-elle selon le résultat ?', icon: 'stacked_bar_chart', source: nativeColumns.value.length ? 'Graphiques originaux' : 'Vue synthétique' }] : []),
  ...(associationInsight.value || (props.statisticsLoading && selectedAnalysis.value === 'association') ? [{ value: 'association' as const, label: 'Lien avec le résultat', description: 'Quelles informations sont associées au résultat ?', icon: 'insights', source: 'Vue synthétique' }] : []),
]);
const columnOptions = computed(() => comparableColumns.value.filter((column) => searchable(column).includes(searchable(columnSearch.value))));
const selectionKey = computed(() => JSON.stringify([
  selectedAnalysis.value, selectedAnalysis.value === 'comparison' ? selectedColumn.value : '',
]));
const currentInsight = computed(() => cache.value[selectionKey.value]);
const currentNativePlot = computed(() => !preferSynthesis.value ? nativeCache.value[selectionKey.value] : undefined);
const currentAnalysis = computed(() => currentNativePlot.value
  ? { ...currentNativePlot.value.plan, source: 'Graphique original · service DrIA' }
  : currentInsight.value && { ...currentInsight.value, source: `${currentInsight.value.source} · vue synthétique` });
const nativePlan = computed(() => selectedAnalysis.value === 'comparison'
  ? nativeComparisonPlan(props.dataset, selectedColumn.value, props.targetColumn, props.predictionKind)
  : selectedAnalysis.value === 'missing'
    ? missingPlan.value : null);
const proposedInsight = computed(() => {
  if (selectedAnalysis.value === 'missing') return missingInsight.value;
  if (selectedAnalysis.value === 'association') return associationInsight.value;
  return selectedColumn.value ? buildComparisonInsight(
    nativeCache.value[selectionKey.value]?.statistics ?? props.statistics,
    props.dataset, selectedColumn.value, props.targetColumn, props.predictionKind,
  ) : null;
});
const pendingAnalysis = computed(() => nativePlan.value ?? proposedInsight.value);
const ready = computed(() => props.dataset.isComplete && Boolean(pendingAnalysis.value)
  && !props.disabled && !plotLoading.value
  && (nativePlan.value || selectedAnalysis.value !== 'missing' ? !props.unavailableReason : true));
const nativeImage = computed(() => currentNativePlot.value?.plot.image.startsWith('data:')
  ? currentNativePlot.value.plot.image : `data:image/png;base64,${currentNativePlot.value?.plot.image ?? ''}`);
const comparisonGroups = computed(() => {
  if (selectedAnalysis.value !== 'comparison' || !currentNativePlot.value || !proposedInsight.value) return [];
  const chart = proposedInsight.value.chart;
  return chart.type === 'boxes' ? chart.boxes.filter((box) => box.count !== undefined).map((box) => ({ label: box.label, count: box.count! }))
    : chart.type === 'proportions' ? chart.groups.map((group) => ({ label: group.label, count: group.present })) : [];
});
const retainedMissing = computed(() => props.columns.filter((column) => props.dataset.profiles[column]?.missingCount > 0).length);
const nativeAvailability = computed(() => {
  if (!props.dataset.isComplete || props.unavailableReason || props.predictionKind === 'survival') return '';
  if (nativeColumns.value.length || missingPlan.value) return '';
  return props.predictionKind === 'quantity'
    ? 'Pour un résultat numérique, les comparaisons par catégorie ne s’appliquent pas. Aucun graphique original du service DrIA n’est disponible pour cette sélection ; les analyses proposées sont des vues synthétiques.'
    : 'Aucun graphique original du service DrIA n’est disponible pour cette sélection. Les vues synthétiques restent accessibles lorsque les statistiques le permettent.';
});
const contextualReason = computed(() => {
  if (!props.dataset.isComplete) return 'Attendez la fin de la lecture du fichier pour explorer toutes les observations.';
  if (props.predictionKind === 'survival') return 'Pour la survie, le bilan présente les événements observés et les durées de suivi. Les statistiques disponibles ici ne permettent pas de tracer une courbe de survie.';
  if (props.unavailableReason) return props.unavailableReason;
  const needsStatistics = selectedAnalysis.value === 'association' || (selectedAnalysis.value === 'comparison' && !nativePlan.value);
  if (needsStatistics && props.statisticsLoading) return 'Les statistiques DrIA préparent les comparaisons et les liens avec le résultat…';
  if (needsStatistics && props.statisticsError) return props.statisticsError;
  if (needsStatistics && !props.statistics) return 'Les statistiques DrIA sont nécessaires pour explorer les liens avec le résultat.';
  return '';
});

function chooseAnalysis(analysis: ProjectAnalysisKind) {
  userSelectedAnalysis.value = true;
  selectedAnalysis.value = analysis;
}
function searchable(value: string): string {
  return value.normalize('NFD').replace(/[\u0300-\u036f]/g, '').toLocaleLowerCase('fr');
}
function filterColumns(value: string, update: (callback: () => void) => void) {
  update(() => { columnSearch.value = value; });
}
function cancelPlot() {
  plotRevision += 1;
  plotController?.abort();
  plotController = undefined;
  plotLoading.value = false;
  plotError.value = '';
}
function useSynthesis() {
  if (!proposedInsight.value) return;
  cancelPlot();
  cache.value[selectionKey.value] = proposedInsight.value;
  preferSynthesis.value = true;
  viewerOpen.value = false;
  changed.value = false;
}
function useNativePlot() {
  preferSynthesis.value = false;
  if (!currentNativePlot.value) { delete cache.value[selectionKey.value]; void display(); }
}
async function display() {
  if (!ready.value || !pendingAnalysis.value) return;
  if (!nativePlan.value) {
    if (proposedInsight.value) cache.value[selectionKey.value] = proposedInsight.value;
    changed.value = false;
    return;
  }
  cancelPlot();
  const revision = plotRevision;
  const key = selectionKey.value;
  const plan = nativePlan.value;
  const controller = new AbortController();
  plotController = controller;
  plotLoading.value = true;
  try {
    const result = await props.loadNativePlot(plan, controller.signal);
    if (controller.signal.aborted || revision !== plotRevision || key !== selectionKey.value) return;
    nativeCache.value[key] = { plot: selectNativeDataPlot(result, plan), plan, statistics: result };
    preferSynthesis.value = false;
    delete cache.value[key];
    changed.value = false;
  } catch (error) {
    if (!controller.signal.aborted && revision === plotRevision) plotError.value = apiError(error);
  } finally {
    if (plotController === controller) { plotController = undefined; plotLoading.value = false; }
  }
}

watch([
  () => props.dataset, () => JSON.stringify(props.columns),
  () => props.predictionKind, () => props.targetColumn,
  () => props.analysisContext,
], () => {
  changed.value = changed.value || Object.keys(cache.value).length > 0 || Object.keys(nativeCache.value).length > 0;
  cancelPlot();
  preferSynthesis.value = false;
  cache.value = Object.create(null);
  nativeCache.value = Object.create(null);
  viewerOpen.value = false;
});
watch([() => props.statistics, () => props.unavailableReason], () => {
  // Coverage comes from the full CSV profile and remains valid while IAML
  // refreshes its statistics. Only comparisons and associations need replacing.
  const retained: Record<string, ProjectInsight> = Object.create(null);
  Object.entries(cache.value).forEach(([key, insight]) => {
    if (JSON.parse(key)[0] === 'missing' || nativeCache.value[key]) retained[key] = insight;
    else changed.value = true;
  });
  cache.value = retained;
  if (selectedAnalysis.value !== 'missing') viewerOpen.value = false;
});
watch(choices, (available) => {
  if (!available.some((choice) => choice.value === selectedAnalysis.value)) selectedAnalysis.value = 'missing';
  if (!userSelectedAnalysis.value && !missingInsight.value) {
    selectedAnalysis.value = available.find((choice) => choice.value !== 'missing')?.value ?? 'missing';
  }
}, { immediate: true });
watch(comparableColumns, (columns) => {
  if (!columns.includes(selectedColumn.value)) selectedColumn.value = columns[0] ?? '';
}, { immediate: true });
watch(selectedColumn, () => { columnSearch.value = ''; });
watch(selectionKey, () => { cancelPlot(); preferSynthesis.value = false; viewerOpen.value = false; }, { flush: 'sync' });
watch(currentNativePlot, () => { imageFailed.value = false; });
onUnmounted(cancelPlot);
</script>

<template>
  <section class="data-plots" aria-labelledby="data-insights-title">
    <div class="insights-intro">
      <span class="insights-icon"><q-icon name="query_stats" size="24px" aria-hidden="true" /></span>
      <div>
        <h2 id="data-insights-title">Comprendre vos données</h2>
        <p>{{ context === 'project' ? 'Des vues ciblées pour explorer les données du projet, sur le fichier entier et les informations retenues pour prédire.' : 'Des vues ciblées pour préparer votre étude, sur le fichier entier et les informations retenues pour prédire.' }}</p>
      </div>
    </div>
    <div class="insights-scope">{{ number.format(dataset.rowCount) }} observations · {{ columns.length }} information{{ columns.length > 1 ? 's' : '' }} retenue{{ columns.length > 1 ? 's' : '' }}<template v-if="targetColumn"> · Résultat : <strong>{{ targetColumn }}</strong></template></div>

    <div class="insight-choices" :class="{ 'single-choice': choices.length === 1, 'two-choices': choices.length === 2 }" aria-label="Questions à explorer">
      <q-btn
        v-for="choice in choices" :key="choice.value"
        flat no-caps align="left"
        class="insight-choice" :class="{ selected: selectedAnalysis === choice.value }"
        :aria-label="choice.label" :aria-pressed="selectedAnalysis === choice.value"
        :disable="disabled || !dataset.isComplete || (choice.value === 'association' && statisticsLoading)"
        @click="chooseAnalysis(choice.value)"
      >
        <q-icon :name="choice.icon" size="20px" aria-hidden="true" />
        <span><strong>{{ choice.label }}</strong><small>{{ choice.description }}</small><small v-if="choice.source" class="insight-choice-source">{{ choice.source }}</small></span>
      </q-btn>
    </div>

    <p v-if="nativeAvailability" class="insight-note" role="status">{{ nativeAvailability }}</p>

    <div v-if="contextualReason" class="insight-context" role="status">
      <q-spinner v-if="statisticsLoading && predictionKind !== 'survival'" size="16px" class="q-mr-sm" />{{ contextualReason }}
      <q-btn v-if="statisticsError && !unavailableReason" flat dense no-caps label="Réessayer les statistiques" color="primary" :disable="disabled || statisticsLoading" @click="emit('retry-statistics')" />
    </div>

    <div v-if="selectedAnalysis === 'comparison'" class="comparison-selector">
      <q-select
        v-model="selectedColumn" :options="columnOptions" :disable="disabled"
        outlined dense use-input hide-selected fill-input input-debounce="0"
        label="Information à comparer" color="primary"
        :popup-content-class="context === 'project' ? 'project-workspace-colors' : 'project-creation-colors'" @filter="filterColumns"
      >
        <template #option="scope">
          <q-item v-bind="scope.itemProps"><q-item-section class="insight-option">{{ scope.opt }}</q-item-section></q-item>
        </template>
        <template #no-option><q-item><q-item-section>Aucune information correspondante.</q-item-section></q-item></template>
      </q-select>
      <p v-if="nativePlan">Choisissez une information. Le graphique est adapté à ses valeurs : distribution des nombres ou effectifs des catégories, selon le résultat à prédire.</p>
      <p v-else>Comparez les groupes de <strong>{{ targetColumn }}</strong>. Cette vue synthétique utilise les statistiques DrIA et regroupe les catégories lorsque leur nombre empêche une lecture claire.</p>
    </div>

    <div v-if="selectedAnalysis === 'missing' && dataset.isComplete" class="quality-context">
      <q-icon :name="retainedMissing ? 'data_usage' : 'check_circle'" size="21px" aria-hidden="true" />
      <span v-if="retainedMissing">{{ retainedMissing }} information{{ retainedMissing > 1 ? 's contiennent' : ' contient' }} des valeurs manquantes. La vue d’ensemble permet de repérer les colonnes les plus incomplètes.</span>
      <span v-else>Aucune valeur manquante dans les informations actuellement retenues. Un graphique supplémentaire n’est pas nécessaire.</span>
    </div>

    <div v-if="!currentAnalysis && pendingAnalysis" class="insight-action">
      <p>{{ pendingAnalysis.description }}<span v-if="nativePlan" class="native-calculation-note">Cette image est le graphique original produit par le service DrIA. Il est calculé uniquement à votre demande.</span></p>
      <q-btn outline no-caps color="primary" icon="bar_chart" :label="nativePlan ? 'Calculer le graphique original' : 'Afficher la vue synthétique'" :loading="plotLoading" :disable="!ready" @click="display" />
    </div>
    <p v-if="changed && !currentAnalysis && pendingAnalysis" class="insight-note" role="status">Vos choix ont changé. Affichez de nouveau l’analyse pour utiliser les données actuelles.</p>
    <div v-if="plotError" class="plot-error" role="alert"><q-icon name="error_outline" size="20px" /><p>{{ plotError }}</p><div><q-btn flat dense no-caps color="primary" label="Réessayer" :disable="!ready" @click="display" /><q-btn v-if="proposedInsight" flat dense no-caps color="primary" label="Afficher la vue synthétique" :disable="disabled" @click="useSynthesis" /></div></div>

    <figure v-if="currentAnalysis" class="selected-insight" aria-live="polite" :aria-busy="plotLoading">
      <figcaption class="insight-heading">
        <div><div class="insight-source">{{ currentAnalysis.source }}</div><h3>{{ currentAnalysis.title }}</h3></div>
        <q-btn flat round dense color="primary" icon="zoom_in" aria-label="Agrandir l’analyse" :disable="imageFailed" @click="viewerOpen = true" />
      </figcaption>
      <div class="insight-body">
        <p class="insight-description">{{ currentAnalysis.description }}</p>
        <div v-if="currentNativePlot" class="plot-image">
          <p v-if="imageFailed" class="plot-error" role="alert">L’image du graphique DrIA n’a pas pu être affichée.</p>
          <img v-else :src="nativeImage" :alt="currentAnalysis.title" @error="imageFailed = true" />
        </div>
        <ProjectInsightChart v-else-if="currentInsight" :chart="currentInsight.chart" />
        <p v-if="comparisonGroups.length" class="plot-group-sizes">Valeurs renseignées par groupe : {{ comparisonGroups.map((group) => `${group.label} : ${number.format(group.count)}`).join(' · ') }}.</p>
      </div>
      <div class="insight-reading"><q-icon name="info_outline" size="18px" aria-hidden="true" /><p>{{ currentAnalysis.note }}<span v-if="currentInsight?.omittedCount"> {{ currentInsight.omittedCount }} autre{{ currentInsight.omittedCount > 1 ? 's' : '' }} colonne{{ currentInsight.omittedCount > 1 ? 's' : '' }} reste{{ currentInsight.omittedCount > 1 ? 'nt' : '' }} disponible{{ currentInsight.omittedCount > 1 ? 's' : '' }} dans le bilan chiffré.</span></p></div>
      <div v-if="currentNativePlot || (preferSynthesis && nativePlan)" class="plot-recalculate"><q-btn v-if="currentNativePlot?.plan.nativeTitle === 'Bar plot' && proposedInsight" flat dense no-caps color="primary" label="Comparer les proportions" :disable="disabled || plotLoading" @click="useSynthesis" /><q-btn v-if="preferSynthesis" flat dense no-caps color="primary" label="Voir le graphique original" :disable="disabled || plotLoading" @click="useNativePlot" /><q-btn v-else flat dense no-caps color="primary" icon="refresh" label="Recalculer ce graphique" :loading="plotLoading" :disable="!ready" @click="display" /></div>
    </figure>
    <p v-if="!currentAnalysis && !pendingAnalysis && selectedAnalysis !== 'missing' && !statisticsLoading" class="insight-note">Les statistiques disponibles ne suffisent pas à représenter cette sélection.</p>

    <q-dialog v-model="viewerOpen" :maximized="$q.screen.lt.sm" aria-labelledby="project-insight-title">
      <q-card v-if="currentAnalysis" class="insight-viewer" :class="context === 'project' ? 'project-workspace-colors' : 'project-creation-colors'" tabindex="-1" data-autofocus>
        <q-card-section class="viewer-heading">
          <div><div class="insight-source">{{ currentAnalysis.source }}</div><h4 id="project-insight-title">{{ currentAnalysis.title }}</h4></div>
          <q-btn v-close-popup flat round dense icon="close" aria-label="Fermer l’analyse" />
        </q-card-section>
        <div class="viewer-scroll"><p class="insight-description">{{ currentAnalysis.description }}</p><div v-if="currentNativePlot" class="plot-image"><img :src="nativeImage" :alt="currentAnalysis.title" /></div><ProjectInsightChart v-else-if="currentInsight" :chart="currentInsight.chart" /><p class="insight-note">{{ currentAnalysis.note }}</p></div>
      </q-card>
    </q-dialog>
  </section>
</template>

<style scoped>
.data-plots { min-width: 0; padding: 22px; color: var(--project-ink); background: var(--project-surface); border: 1px solid var(--project-border); border-radius: 8px; }
.insights-intro { display: flex; align-items: flex-start; gap: 12px; }
.insights-icon { display: inline-flex; align-items: center; justify-content: center; flex-shrink: 0; width: 42px; height: 42px; color: var(--project-selection-ink); background: var(--project-tint); border-radius: 8px; }
.insights-intro > div { min-width: 0; }
.insights-intro h2 { margin: 0; font-size: 18px; font-weight: 600; line-height: 1.4; }
.insights-intro p { margin: 6px 0 0; color: var(--project-muted); font-size: 13px; line-height: 1.6; }
.insights-scope { margin: 18px 0; padding-bottom: 16px; border-bottom: 1px solid var(--project-border); color: var(--project-muted); font-size: 12px; line-height: 1.6; overflow-wrap: anywhere; }
.insights-scope strong { color: var(--project-ink); font-weight: 500; }
.insight-choices { display: grid; grid-template-columns: repeat(3, minmax(0, 1fr)); gap: 8px; }
.insight-choices.two-choices { grid-template-columns: repeat(2, minmax(0, 1fr)); }
.insight-choices.single-choice { grid-template-columns: minmax(0, 1fr); }
.insight-choice { min-width: 0; padding: 12px 10px; border: 1px solid var(--project-border); border-radius: 6px; color: var(--project-ink); }
.insight-choice :deep(.q-btn__content) { flex-wrap: nowrap; align-items: flex-start; gap: 8px; }
.insight-choice :deep(.q-icon) { flex-shrink: 0; margin-top: 2px; color: var(--project-muted); }
.insight-choice span { min-width: 0; text-align: left; }
.insight-choice strong { display: block; font-size: 12px; font-weight: 600; line-height: 1.5; }
.insight-choice small { display: block; margin-top: 5px; color: var(--project-muted); font-size: 11px; line-height: 1.5; }
.insight-choice .insight-choice-source { color: var(--project-selection-ink); font-weight: 500; }
.insight-choice.selected { border-color: var(--q-primary); background: var(--project-selection-surface); }
.insight-choice.selected :deep(.q-icon) { color: var(--project-selection-ink); }
.insight-choice:focus-visible { outline: 2px solid var(--q-primary); outline-offset: 3px; }
.insight-context { margin-top: 14px; color: var(--project-muted); font-size: 12px; line-height: 1.6; overflow-wrap: anywhere; }
.comparison-selector { margin-top: 20px; }
.comparison-selector p { margin: 8px 0 0; color: var(--project-muted); font-size: 12px; line-height: 1.6; }
.insight-option { overflow-wrap: anywhere; white-space: normal; }
.quality-context { display: flex; align-items: flex-start; gap: 9px; margin-top: 20px; color: var(--project-muted); font-size: 13px; line-height: 1.6; }
.quality-context .q-icon { flex-shrink: 0; color: var(--q-primary); margin-top: 1px; }
.insight-action { display: flex; align-items: flex-start; justify-content: space-between; gap: 14px; margin-top: 20px; }
.insight-action p { margin: 0; min-width: 0; color: var(--project-muted); font-size: 12px; line-height: 1.6; }
.insight-action .q-btn { flex-shrink: 0; font-size: 12px; }
.insight-note { margin: 12px 0 0; color: var(--project-muted); font-size: 12px; line-height: 1.6; overflow-wrap: anywhere; }
.native-calculation-note { display: block; margin-top: 5px; }
.plot-image { display: flex; justify-content: center; min-width: 0; background: white; }
.plot-image img { display: block; width: auto; height: auto; max-width: 100%; max-height: 560px; object-fit: contain; }
.plot-group-sizes { margin: 14px 0 0; color: var(--project-muted); font-size: 12px; line-height: 1.6; overflow-wrap: anywhere; }
.plot-error { display: flex; align-items: flex-start; gap: 9px; margin-top: 14px; padding: 12px; color: var(--project-error-ink, #a33548); background: var(--project-error-surface, #fff4f6); border: 1px solid var(--project-error-border, #efd2d9); border-radius: 6px; font-size: 12px; line-height: 1.6; }
.plot-error p { flex: 1; margin: 0; min-width: 0; overflow-wrap: anywhere; }
.plot-error .q-icon { flex-shrink: 0; margin-top: 2px; }
.plot-recalculate { display: flex; flex-wrap: wrap; justify-content: flex-end; gap: 8px; padding: 6px 12px; border-top: 1px solid var(--project-border); }
.selected-insight { margin: 20px 0 0; border: 1px solid var(--project-border); border-radius: 8px; overflow: hidden; }
.insight-heading { display: flex; align-items: flex-start; justify-content: space-between; gap: 12px; padding: 14px 16px; background: var(--project-paper); border-bottom: 1px solid var(--project-border); }
.insight-heading > div, .viewer-heading > div { min-width: 0; overflow-wrap: anywhere; }
.insight-heading .q-btn, .viewer-heading .q-btn { flex-shrink: 0; }
.insight-source { color: var(--project-muted); font-size: 11px; line-height: 1.5; }
.insight-heading h3 { margin: 4px 0 0; font-size: 15px; font-weight: 600; line-height: 1.5; }
.insight-body { padding: 16px; }
.insight-description { margin: 0 0 18px; color: var(--project-muted); font-size: 12px; line-height: 1.6; }
.insight-reading { display: flex; align-items: flex-start; gap: 8px; padding: 14px 16px; color: var(--project-muted); background: var(--project-paper); border-top: 1px solid var(--project-border); }
.insight-reading .q-icon { flex-shrink: 0; margin-top: 2px; }
.insight-reading p { margin: 0; font-size: 12px; line-height: 1.6; }
.insight-viewer { display: flex; flex-direction: column; width: min(1080px, calc(100vw - 48px)); max-width: calc(100vw - 48px); max-height: calc(100dvh - 48px); border-radius: 10px; }
.viewer-heading { display: flex; align-items: flex-start; justify-content: space-between; gap: 16px; flex-shrink: 0; }
.viewer-heading h4 { margin: 4px 0 0; color: var(--project-ink); font-size: 20px; font-weight: 600; line-height: 1.4; }
.viewer-scroll { min-height: 0; overflow: auto; padding: 0 20px 20px; }
.viewer-scroll .plot-image img { width: 100%; max-width: 960px; max-height: none; }
@media (max-width: 599px) {
  .data-plots { padding: 18px; }
  .insight-choices, .insight-choices.two-choices { grid-template-columns: minmax(0, 1fr); }
  .insight-choice { padding: 11px 12px; }
  .insight-choice small { margin-top: 2px; }
  .insight-action { flex-direction: column; }
  .insight-action .q-btn { align-self: stretch; }
  .plot-error { flex-wrap: wrap; }
  .plot-error > div { width: 100%; }
  .insight-body, .insight-heading, .insight-reading { padding-left: 12px; padding-right: 12px; }
  .insight-viewer { width: 100%; max-width: 100%; max-height: 100dvh; border-radius: 0; }
}
</style>
