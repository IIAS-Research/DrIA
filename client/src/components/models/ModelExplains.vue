<script setup lang="ts">
import axios from "axios";
import { useQuasar } from "quasar";
import { computed, onMounted, onUnmounted, ref, watch } from "vue";
import { useRoute } from "vue-router";
import { api } from "src/boot/axios";
import { getExplanationStatus, getExplanationResults, getExplanationPlots, startModeleExplanationById, predictValueUsingAiModel } from "src/api/models.api";
import { apiError, displayServiceText, isTemporaryApiError } from "src/utils/iaml";
import { isSessionExpired } from "src/utils/session";
import { IExplanation, IModel, IPlot } from "../models";
import FeatureImportanceRanking from "./FeatureImportanceRanking.vue";

const propsComponent = defineProps<{ model: IModel }>();
const emit = defineEmits<{ (event: "explanationUpdated", explanation: IExplanation): void }>();
const $q = useQuasar();
const route = useRoute();
const currentExplainIndex = ref(0);
const plotViewerOpen = ref(false);
const imageFailed = ref(false);
const isLoadingPlots = ref(false);
const isLoadingExplanation = ref(false);
const explanation = ref<IExplanation>();
const status = ref("");
const explainedClass = ref<unknown>();
let timer: ReturnType<typeof setTimeout> | undefined;
let disposed = false;
let plotsLoaded = false;
let checkedClasses = false;
let lastError = "";

const plots = computed(() => explanation.value?.plots ?? {});
const plotList = computed(() => Object.values(plots.value));
const currentExplain = computed(() => plotList.value[currentExplainIndex.value]);
const plotOptions = computed(() => plotList.value.map((plot, index) => ({
  label: displayServiceText(plot.title) || `Graphique ${index + 1}`, value: index,
})));
const nativeImage = computed(() => {
  const image = currentExplain.value?.image;
  return image ? image.startsWith("data:") ? image : `data:image/png;base64,${image}` : "";
});
const features = computed(() => explanation.value?.features ?? []);
const hasExplanation = computed(() => features.value.length > 0 || Object.keys(plots.value).length > 0);

function changeExplain(index: number) {
  currentExplainIndex.value = index;
}
watch(currentExplain, () => { imageFailed.value = false; plotViewerOpen.value = false; });
function statusName(value: IExplanation["status"]): string {
  return typeof value === "string" ? value : value?.current ?? "";
}
function schedule() {
  if (disposed) return;
  if (timer) clearTimeout(timer);
  timer = setTimeout(refresh, 2500);
}
function notifyError(error: unknown) {
  const message = apiError(error);
  if (!isSessionExpired(error) && message !== lastError) $q.notify({ type: "negative", message });
  lastError = message;
}
function handleRefreshError(error: unknown) {
  notifyError(error);
  if (isTemporaryApiError(error) && status.value) {
    isLoadingExplanation.value = true;
    schedule();
  } else isLoadingExplanation.value = false;
}
function receiveExplanation(result: IExplanation) {
  const shap = Object.values(result.results ?? {}).find(analysis => analysis.kind === "shap");
  const next = {
    ...result,
    features: shap?.features ? Object.entries(shap.features).map(([name, value]) => ({ name, value })) : result.features ?? [],
    // New plots in results are performance analyses; the SHAP figures are loaded
    // from the dedicated JSON route for the native SHAP figures.
    plots: result.results ? (plotsLoaded ? plots.value : {}) : result.plots ?? {},
  };
  explanation.value = next;
  emit("explanationUpdated", next);
}
async function identifyExplainedClass() {
  if (checkedClasses) return;
  checkedClasses = true;
  try {
    const sample = (await api.get(`Projects/${route.params.id}/data-sample`)).data;
    const columns = sample.columns.filter((column: any) => !column.isColToPredict && !column.isGroupColumn && !column.isIdColumn);
    const first = sample.rows[0];
    if (!first) return;
    const row = Object.fromEntries(columns.map((column: any) => {
      let value = first[column.name];
      if (value === "") value = null;
      else if (typeof value === "string" && ["true", "false"].includes(value.toLowerCase())) value = value.toLowerCase() === "true";
      else if (typeof value === "string" && value.trim() && Number.isFinite(Number(value))) value = Number(value);
      return [column.label, value];
    }));
    const result = await predictValueUsingAiModel(propsComponent.model.id, { json_row: [row], method: "predict_proba" });
    explainedClass.value = result.classes?.[1];
  } catch { /* Some fitted models do not expose class probabilities. */ }
}
async function loadPlots() {
  if (plotsLoaded) return;
  const shap = Object.entries(explanation.value?.results ?? {}).find(([, analysis]) => analysis.kind === "shap");
  if (!shap && !features.value.length) return;
  isLoadingPlots.value = true;
  try {
    let images: Record<string, IPlot>;
    try { images = await getExplanationPlots(propsComponent.model.id, { key: shap?.[0] }); }
    catch (error) {
      if (!axios.isAxiosError(error) || error.response?.status !== 400) throw error;
      // Some figures cannot render every model output. Keep the available bar plot.
      images = await getExplanationPlots(propsComponent.model.id, { key: shap?.[0], plots: "bar" });
    }
    if (disposed) return;
    if (explanation.value) explanation.value.plots = images;
    plotsLoaded = true;
    changeExplain(0);
  } catch (error) {
    if (isTemporaryApiError(error)) throw error;
    plotsLoaded = true;
    notifyError(error);
  } finally { isLoadingPlots.value = false; }
  await identifyExplainedClass();
}
async function refresh() {
  if (disposed) return;
  try {
    const current = await getExplanationStatus(propsComponent.model.id);
    const result = await getExplanationResults(propsComponent.model.id);
    if (disposed) return;
    status.value = statusName(result.status) || statusName(current.status);
    receiveExplanation(result);
    lastError = "";
    if (current.error || result.error) notifyError(new Error(current.error || result.error));
    if (["QUEUED", "COMPUTING"].includes(status.value)) {
      isLoadingExplanation.value = true;
      schedule();
    } else {
      isLoadingExplanation.value = false;
      if (["FINISHED", "CACHED"].includes(status.value)) {
        await loadPlots();
        const reports = [result.report, result.configured_report].flat().filter(Boolean);
        if (reports.some(report => report.status === "error")) {
          $q.notify({ type: "warning", message: "Certaines explications sont indisponibles ; les résultats disponibles sont affichés." });
        }
        if (result.cache_error) $q.notify({ type: "warning", message: displayServiceText(result.cache_error) });
      }
    }
  } catch (error) { handleRefreshError(error); }
}
async function getExplanation() {
  if (timer) clearTimeout(timer);
  isLoadingExplanation.value = true;
  plotsLoaded = false;
  checkedClasses = false;
  explainedClass.value = undefined;
  try {
    const created = await startModeleExplanationById(propsComponent.model.id);
    if (!created?.id) throw new Error("L’explication n’a pas pu être lancée.");
    status.value = statusName(created.status);
    await refresh();
  } catch (error) {
    isLoadingExplanation.value = false;
    notifyError(error);
  }
}
onMounted(async () => {
  if (propsComponent.model.explanation) {
    try {
      const stored = JSON.parse(propsComponent.model.explanation) as IExplanation;
      receiveExplanation(stored);
      status.value = statusName(stored.status);
    } catch { /* Old explanations can be recalculated with the original button. */ }
  }
  try {
    const current = await getExplanationStatus(propsComponent.model.id);
    if (disposed) return;
    status.value = statusName(current.status);
    await refresh();
  } catch (error) {
    if (!axios.isAxiosError(error) || error.response?.status !== 404) handleRefreshError(error);
  }
});
onUnmounted(() => {
  disposed = true;
  if (timer) clearTimeout(timer);
});
</script>

<template>
  <div class="model-explains">
    <div v-if="!hasExplanation" class="explanation-empty">
      <span class="empty-explanation-icon"><q-icon name="bar_chart" size="30px" /></span>
      <div><h3>Importance des variables</h3><p>{{ isLoadingExplanation ? 'DrIA calcule l’importance des variables et les graphiques de cette analyse.' : 'Découvrez quelles variables contribuent le plus aux prédictions de ce modèle.' }}</p></div>
      <q-btn unelevated no-caps color="primary" icon="analytics" :label="isLoadingExplanation ? 'Analyse en cours' : 'Calculer l’importance des variables'" :loading="isLoadingExplanation" @click="getExplanation" />
    </div>
    <template v-else>
      <div v-if="isLoadingExplanation" class="explanation-progress" role="status"><q-spinner color="primary" size="20px" /><span>L’analyse se poursuit. Les résultats disponibles sont affichés ci-dessous.</span></div>
      <div class="explanation-layout" :class="{ 'single-panel': !features.length || (!plotList.length && !isLoadingPlots) }">
        <FeatureImportanceRanking v-if="features.length" :features="features" :explained-class="explainedClass" />
        <section v-if="plotList.length || isLoadingPlots" class="explanation-figures" aria-labelledby="explanation-figures-title">
          <header class="figures-heading"><div><span class="analysis-eyebrow">Explorer l’analyse</span><h3 id="explanation-figures-title">Graphiques de l’explication</h3><p>Consultez les graphiques associés à cette analyse.</p></div><q-btn v-if="nativeImage && !imageFailed" flat round dense color="primary" icon="open_in_full" aria-label="Agrandir le graphique" @click="plotViewerOpen = true" /></header>
          <div v-if="isLoadingPlots && !plotList.length" class="figures-loading" role="status"><q-spinner color="primary" size="26px" /><p>Chargement des graphiques…</p></div>
          <template v-else>
            <div class="figure-selection">
              <q-select v-if="plotList.length > 1" :model-value="currentExplainIndex" :options="plotOptions" emit-value map-options outlined dense label="Graphique de l’explication" popup-content-class="project-workspace-colors" @update:model-value="changeExplain" />
              <h4 v-else>{{ displayServiceText(currentExplain?.title) }}</h4>
            </div>
            <figure class="native-figure">
              <div v-if="imageFailed || !nativeImage" class="figure-unavailable" role="status"><q-icon name="image_not_supported" size="28px" /><p>Ce graphique ne peut pas être affiché pour le moment.</p></div>
              <img v-else :src="nativeImage" :alt="displayServiceText(currentExplain?.title) || 'Graphique de l’explication du modèle'" @error="imageFailed = true" />
              <figcaption v-if="currentExplain?.description">{{ displayServiceText(currentExplain.description) }}</figcaption>
            </figure>
            <footer class="figure-navigation"><span>Graphique {{ currentExplainIndex + 1 }} sur {{ plotList.length }}</span><div v-if="plotList.length > 1"><q-btn flat round dense icon="chevron_left" color="primary" aria-label="Graphique précédent" :disable="currentExplainIndex === 0" @click="changeExplain(currentExplainIndex - 1)" /><q-btn flat round dense icon="chevron_right" color="primary" aria-label="Graphique suivant" :disable="currentExplainIndex === plotList.length - 1" @click="changeExplain(currentExplainIndex + 1)" /></div></footer>
          </template>
        </section>
      </div>
    </template>
    <q-dialog v-model="plotViewerOpen">
      <q-card class="figure-viewer project-workspace-colors"><q-card-section class="viewer-heading"><h3>{{ displayServiceText(currentExplain?.title) }}</h3><q-btn v-close-popup flat round dense icon="close" aria-label="Fermer le graphique" /></q-card-section><div class="viewer-image"><img :src="nativeImage" :alt="displayServiceText(currentExplain?.title) || 'Graphique de l’explication du modèle'" /><p v-if="currentExplain?.description">{{ displayServiceText(currentExplain.description) }}</p></div></q-card>
    </q-dialog>
  </div>
</template>

<style scoped>
.model-explains { min-width: 0; color: var(--project-ink); }
.explanation-layout { display: grid; grid-template-columns: minmax(0, .9fr) minmax(0, 1.1fr); gap: 28px; align-items: start; }
.explanation-layout.single-panel { grid-template-columns: minmax(0, 1fr); }
.explanation-figures { min-width: 0; padding: 24px; border: 1px solid var(--project-border); border-radius: 14px; background: var(--project-surface); }
.figures-heading { display: flex; align-items: flex-start; justify-content: space-between; gap: 12px; margin-bottom: 20px; }
.figures-heading > div { min-width: 0; }
.analysis-eyebrow { display: block; color: var(--q-primary); font-size: 11px; font-weight: 700; letter-spacing: .08em; text-transform: uppercase; }
.figures-heading h3 { margin: 6px 0 7px; font-size: 19px; line-height: 1.4; font-weight: 600; }
.figures-heading p { margin: 0; color: var(--project-muted); font-size: 12px; line-height: 1.7; }
.figure-selection { margin-bottom: 16px; }
.figure-selection h4 { margin: 0; font-size: 14px; line-height: 1.5; font-weight: 600; }
.figure-selection :deep(.q-field__native) { font-size: 12px; }
.native-figure { margin: 0; min-width: 0; }
.native-figure > img { display: block; width: 100%; max-height: 620px; object-fit: contain; }
.native-figure figcaption { margin-top: 16px; padding: 14px 16px; border-radius: 8px; background: var(--project-paper); color: var(--project-muted); font-size: 12px; line-height: 1.75; overflow-wrap: anywhere; }
.figure-navigation { display: flex; align-items: center; justify-content: space-between; gap: 12px; border-top: 1px solid var(--project-border); margin-top: 18px; padding-top: 10px; }
.figure-navigation > span { font-size: 11px; color: var(--project-muted); }
.figure-navigation > div { display: flex; gap: 4px; }
.figures-loading, .figure-unavailable { min-height: 240px; display: flex; flex-direction: column; justify-content: center; align-items: center; gap: 14px; color: var(--project-muted); background: var(--project-paper); border-radius: 10px; text-align: center; padding: 20px; }
.figures-loading p, .figure-unavailable p { font-size: 12px; margin: 0; line-height: 1.7; }
.explanation-progress { display: flex; align-items: center; gap: 10px; padding-bottom: 20px; color: var(--project-muted); font-size: 12px; }
.explanation-empty { display: grid; justify-items: center; gap: 22px; padding: 40px 20px; text-align: center; }
.empty-explanation-icon { display: grid; place-items: center; width: 60px; height: 60px; border-radius: 14px; background: var(--project-tint); color: var(--q-primary); }
.explanation-empty h3 { margin: 0; font-size: 20px; font-weight: 600; line-height: 1.5; }
.explanation-empty p { margin: 8px 0 0; color: var(--project-muted); font-size: 13px; line-height: 1.7; max-width: 500px; }
.explanation-empty .q-btn { font-size: 12px; }
.figure-viewer { width: min(1120px, calc(100vw - 40px)); max-width: calc(100vw - 40px); max-height: calc(100dvh - 40px); border-radius: 12px; color: var(--project-ink); }
.viewer-heading { display: flex; align-items: center; justify-content: space-between; gap: 16px; border-bottom: 1px solid var(--project-border); padding: 16px 20px; }
.viewer-heading h3 { font-size: 16px; font-weight: 600; margin: 0; line-height: 1.5; overflow-wrap: anywhere; }
.viewer-image { padding: 20px; overflow: auto; }
.viewer-image img { display: block; max-width: 100%; height: auto; margin: 0 auto; }
.viewer-image p { margin: 18px 0 0; color: var(--project-muted); font-size: 12px; line-height: 1.7; overflow-wrap: anywhere; }
@media (min-width: 1001px) {
  .explanation-layout:not(.single-panel) .explanation-figures {
    position: sticky;
    top: 24px;
    max-height: calc(100dvh - 48px);
    overflow-y: auto;
  }
}
@media (max-width: 1000px) {
  .explanation-layout { grid-template-columns: minmax(0, 1fr); gap: 28px; }
}
@media (max-width: 599px) { .explanation-figures { padding: 20px 16px; } .explanation-empty { padding: 26px 0; } .figures-heading h3 { font-size: 18px; } .figure-viewer { max-width: calc(100vw - 24px); } }
</style>
