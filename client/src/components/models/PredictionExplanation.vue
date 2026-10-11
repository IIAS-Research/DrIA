<script setup lang="ts">
import { computed, onUnmounted, ref, watch } from 'vue';
import { getPredictionExplanation, startPredictionExplanation } from 'src/api/models.api';
import type { LocalExplanationOutput } from 'src/api/models.api';

const props = defineProps<{
  modelId: number;
  observationKey: string;
  row: Record<string, string | null>;
  valid: boolean;
}>();
type Explanation = { image: string; output: LocalExplanationOutput; nsamples: number };
type State = 'idle' | 'waiting' | 'calculating' | 'ready' | 'unavailable';
const enabled = ref(true);
const state = ref<State>('idle');
const explanation = ref<Explanation>();
const enlarged = ref(false);
const eligible = computed(() => enabled.value && props.valid && Boolean(props.observationKey));
const statusText = computed(() => state.value === 'waiting' ? 'Actualisation après votre saisie…'
  : state.value === 'calculating' ? 'DrIA calcule les contributions de cette observation…'
    : state.value === 'ready' ? 'Explication à jour avec le formulaire'
      : state.value === 'unavailable' ? 'L’explication n’est pas disponible pour cette observation.'
        : enabled.value ? 'L’explication apparaîtra lorsque la prédiction sera disponible.' : 'L’actualisation automatique est désactivée.');
const outputLabel = computed(() => explanation.value?.output.kind === 'probability'
  ? `Probabilité pour la classe « ${String(explanation.value.output.classLabel)} »`
  : 'Sortie numérique expliquée');
const outputValue = computed(() => explanation.value
  ? new Intl.NumberFormat('fr-FR', explanation.value.output.kind === 'probability'
    ? { style: 'percent', maximumFractionDigits: 1 }
    : { maximumSignificantDigits: 6 }).format(explanation.value.output.value) : '');

// Keep only a few exact observations in this page's memory, never in storage.
const cache = new Map<string, Explanation>();
let debounce: ReturnType<typeof setTimeout> | undefined;
let pollTimer: ReturnType<typeof setTimeout> | undefined;
let releasePoll: (() => void) | undefined;
let controller: AbortController | undefined;
let busy = false;
let pending = false;
let disposed = false;

function pause() {
  return new Promise<void>((resolve) => {
    releasePoll = resolve;
    pollTimer = setTimeout(() => { pollTimer = undefined; releasePoll = undefined; resolve(); }, 1200);
  });
}

function schedule(delay = 900) {
  if (debounce) clearTimeout(debounce);
  debounce = undefined;
  explanation.value = undefined;
  enlarged.value = false;
  pending = false;
  if (!eligible.value || disposed) { state.value = 'idle'; return; }
  const saved = cache.get(props.observationKey);
  if (saved) { explanation.value = saved; state.value = 'ready'; return; }
  state.value = 'waiting';
  debounce = setTimeout(() => {
    debounce = undefined;
    if (!eligible.value || disposed) return;
    // Let an existing SHAP job finish before starting the latest observation.
    // Aborting a request would not cancel IAML's background calculation.
    if (busy) { pending = true; return; }
    void calculate(props.observationKey, props.modelId, { ...props.row });
  }, delay);
}

async function calculate(key: string, modelId: number, row: Record<string, string | null>) {
  busy = true;
  controller = new AbortController();
  const signal = controller.signal;
  const current = () => !disposed && eligible.value && key === props.observationKey && modelId === props.modelId;
  if (current()) state.value = 'calculating';
  try {
    const job = await startPredictionExplanation(modelId, row, signal);
    if (!job.ticket || !job.output || !Number.isFinite(job.output.value)
      || !['probability', 'value'].includes(job.output.kind)
      || (job.output.kind === 'probability' && (job.output.classLabel === undefined || job.output.value < 0 || job.output.value > 1)))
      throw new Error('Unavailable explanation');
    const deadline = Date.now() + 120_000;
    while (!disposed && !signal.aborted && Date.now() < deadline) {
      const result = await getPredictionExplanation(modelId, job.ticket, signal);
      if (result.status === 'FINISHED' || result.status === 'CACHED') {
        // Render the native PNG only; never mistake global importance for local contributions.
        if (!result.plot?.image?.startsWith('iVBORw0KGgo')) throw new Error('Unavailable plot');
        const next: Explanation = { image: `data:image/png;base64,${result.plot.image}`, output: job.output, nsamples: job.nsamples };
        cache.set(key, next);
        if (cache.size > 12) cache.delete(cache.keys().next().value!);
        if (current()) { explanation.value = next; state.value = 'ready'; }
        return;
      }
      if (!['QUEUED', 'COMPUTING'].includes(result.status)) throw new Error('Unavailable explanation');
      await pause();
    }
    if (!disposed) throw new Error('Explanation timed out');
  } catch {
    if (current()) state.value = 'unavailable';
  } finally {
    busy = false;
    controller = undefined;
    if (pending && !disposed) schedule();
  }
}

watch(() => [props.observationKey, props.valid, enabled.value], () => schedule(), { immediate: true, flush: 'sync' });
onUnmounted(() => {
  disposed = true;
  controller?.abort();
  if (debounce) clearTimeout(debounce);
  if (pollTimer) clearTimeout(pollTimer);
  releasePoll?.();
  cache.clear();
});
</script>

<template>
  <section class="prediction-shap" :aria-busy="state === 'waiting' || state === 'calculating'" aria-labelledby="prediction-shap-title">
    <div class="shap-heading">
      <div><p class="shap-eyebrow">Comprendre cette observation</p><h2 id="prediction-shap-title">Quelles variables influencent le résultat ?</h2></div>
      <q-toggle v-model="enabled" label="Explication SHAP" color="primary" size="sm" />
    </div>
    <div class="shap-status" :class="{ 'prediction-shap-error': state === 'unavailable' }" role="status" aria-live="polite">
      <q-spinner v-if="state === 'waiting' || state === 'calculating'" color="primary" size="17px" />
      <q-icon v-else-if="state === 'ready'" name="check_circle_outline" color="primary" size="17px" />
      <span>{{ statusText }}</span>
      <q-btn v-if="state === 'unavailable' && eligible" flat dense no-caps color="primary" label="Réessayer" @click="schedule(0)" />
    </div>
    <div v-if="explanation" class="shap-content">
      <figure class="shap-figure">
        <img class="prediction-shap-image" :src="explanation.image" alt="Graphique SHAP DrIA des contributions positives et négatives pour l’observation du formulaire" />
        <figcaption><span>Graphique calculé par DrIA · SHAP local</span><q-btn flat dense no-caps color="primary" icon="open_in_full" label="Agrandir" @click="enlarged = true" /></figcaption>
      </figure>
      <div class="shap-reading">
        <div class="shap-output"><span>{{ outputLabel }}</span><strong>{{ outputValue }}</strong></div>
        <p v-if="explanation.output.kind === 'probability'" class="shap-class-note">DrIA explique la deuxième classe dans l’ordre fourni par le modèle : elle peut être différente de la catégorie prédite ci-dessus.</p>
        <h3>Lire ce graphique</h3>
        <p>Le point de départ est la sortie moyenne du modèle sur ses données d’entraînement. Chaque variable contribue ensuite au résultat de cette observation.</p>
        <div class="shap-legend"><span><i class="shap-positive" />Augmente la sortie expliquée</span><span><i class="shap-negative" />Diminue la sortie expliquée</span></div>
        <p class="shap-method">Les contributions sont estimées par Kernel SHAP ({{ explanation.nsamples }} échantillons). Elles se recalculent après vos modifications ; le calcul peut prendre quelques secondes.</p>
      </div>
    </div>
    <p v-else class="shap-note">SHAP compare cette observation aux données d’entraînement. La prédiction reste disponible pendant le calcul de son explication.</p>
    <q-dialog v-model="enlarged">
      <q-card class="shap-dialog project-workspace-colors">
        <q-card-section class="shap-dialog-heading"><h2>Contributions pour cette observation</h2><q-btn v-close-popup flat round dense icon="close" aria-label="Fermer le graphique" /></q-card-section>
        <q-card-section v-if="explanation"><p class="shap-dialog-output">{{ outputLabel }} : <strong>{{ outputValue }}</strong></p><img :src="explanation.image" alt="Graphique SHAP DrIA agrandi pour cette observation" /></q-card-section>
      </q-card>
    </q-dialog>
  </section>
</template>

<style scoped>
.prediction-shap { grid-column: 1 / -1; min-width: 0; padding: 24px; border: 1px solid var(--project-border); border-radius: 12px; background: var(--project-surface); }
.shap-heading { display: flex; justify-content: space-between; align-items: center; gap: 16px; }
.shap-eyebrow { margin: 0 0 6px; color: var(--q-primary); font-size: 10px; font-weight: 600; text-transform: uppercase; letter-spacing: .07em; }
.shap-heading h2 { margin: 0; font-size: 19px; font-weight: 600; line-height: 1.5; }
.shap-heading :deep(.q-toggle__label) { font-size: 12px; }
.shap-status { display: flex; align-items: center; flex-wrap: wrap; gap: 8px; margin-top: 18px; color: var(--project-muted); font-size: 12px; line-height: 1.7; }
.shap-note { margin: 12px 0 0; color: var(--project-muted); font-size: 12px; line-height: 1.75; }
.shap-content { display: grid; grid-template-columns: minmax(0, 1.8fr) minmax(240px, 1fr); gap: 28px; align-items: start; margin-top: 24px; }
.shap-figure { min-width: 0; margin: 0; }
.prediction-shap-image { display: block; width: 100%; height: auto; }
.shap-figure figcaption { display: flex; align-items: center; justify-content: space-between; gap: 12px; margin-top: 12px; color: var(--project-muted); font-size: 10px; }
.shap-figure .q-btn { font-size: 11px; }
.shap-reading { min-width: 0; }
.shap-output { padding: 16px; background: var(--project-tint); border-radius: 8px; }
.shap-output span { display: block; font-size: 12px; line-height: 1.6; overflow-wrap: anywhere; }
.shap-output strong { display: block; margin-top: 8px; font-size: 28px; line-height: 1.3; color: var(--q-primary); font-weight: 600; }
.shap-reading h3 { font-size: 13px; font-weight: 600; margin: 24px 0 8px; }
.shap-reading p { font-size: 12px; line-height: 1.75; color: var(--project-muted); margin: 0; }
.shap-reading .shap-class-note { margin-top: 10px; font-size: 11px; }
.shap-legend { display: grid; gap: 10px; margin-top: 16px; color: var(--project-muted); font-size: 11px; }
.shap-legend span { display: flex; align-items: center; gap: 8px; }
.shap-legend i { display: inline-block; width: 12px; height: 5px; border-radius: 3px; flex-shrink: 0; }
.shap-positive { background: #ff0051; }
.shap-negative { background: #008bfb; }
.shap-reading .shap-method { margin-top: 20px; padding-top: 16px; border-top: 1px solid var(--project-border); font-size: 10px; }
.shap-dialog { width: min(1120px, 96vw); max-width: 96vw; color: var(--project-ink); }
.shap-dialog-heading { display: flex; align-items: center; justify-content: space-between; gap: 12px; border-bottom: 1px solid var(--project-border); }
.shap-dialog-heading h2 { margin: 0; font-size: 17px; font-weight: 600; line-height: 1.5; }
.shap-dialog-output { color: var(--project-muted); font-size: 13px; line-height: 1.7; }
.shap-dialog img { display: block; width: 100%; height: auto; }
@media (max-width: 899px) {
  .shap-content { grid-template-columns: minmax(0, 1fr); gap: 20px; }
}
@media (max-width: 599px) {
  .prediction-shap { padding: 20px 16px; }
  .shap-heading { flex-wrap: wrap; gap: 10px; }
  .shap-heading h2 { font-size: 17px; }
  .shap-heading .q-toggle { margin-left: -8px; }
  .shap-figure figcaption { flex-wrap: wrap; }
}
</style>
