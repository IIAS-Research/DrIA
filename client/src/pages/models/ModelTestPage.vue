<script setup lang="ts">
import 'src/css/project-creation.scss';
import { computed, onUnmounted, ref, watch } from 'vue';
import { useRoute } from 'vue-router';
import type { ICsvProject, IModel, IProject } from 'src/components/models';
import { api } from 'src/boot/axios';
import { predictValueUsingAiModel } from 'src/api/models.api';
import PredictionExplanation from 'src/components/models/PredictionExplanation.vue';
import { apiError } from 'src/utils/iaml';
import { displayServiceName } from 'src/utils/service-label';
import { displayFollowUpUnit } from 'src/utils/project-data';
import { describePredictionFailure, parseSinglePrediction, parseSurvivalCurve, predictionFields, survivalAt } from 'src/utils/model-prediction';
import type { PredictionFailure, PredictionScalar, SurvivalCurve } from 'src/utils/model-prediction';

type PredictionState = 'idle' | 'waiting' | 'calculating' | 'ready' | 'invalid' | 'error';
const route = useRoute();
const projectId = computed(() => Number(route.params.id));
const processId = computed(() => Number(route.params.processId));
const modelId = computed(() => Number(route.params.modelId));
const projectViews = ['overview', 'training', 'data', 'configuration'] as const;
type ProjectView = typeof projectViews[number];
const returnView = computed<ProjectView>(() => projectViews.includes(route.query.returnView as ProjectView)
  ? route.query.returnView as ProjectView : 'training');
const modelLocation = computed(() => ({
  name: 'model-show',
  params: { id: projectId.value, processId: processId.value, modelId: modelId.value },
  query: { view: 'performance', returnView: returnView.value },
}));
const project = ref<IProject>();
const model = ref<IModel>();
const sample = ref<ICsvProject>();
const loading = ref(true);
const loadError = ref('');
const draft = ref<Record<string, string | null>>({});
const selectedObservation = ref(-1);
const fieldSearch = ref('');
const state = ref<PredictionState>('idle');
const prediction = ref<PredictionScalar>(null);
const predictionFailure = ref<PredictionFailure>();
const curve = ref<SurvivalCurve>();
const selectedTime = ref<number>();
const rawSurvivalOutput = ref(false);
const fields = computed(() => project.value && sample.value ? predictionFields(sample.value, project.value) : []);
const shownFields = computed(() => fields.value.filter((field) =>
  field.label.toLocaleLowerCase('en-US').includes((fieldSearch.value || '').trim().toLocaleLowerCase('en-US'))));
const observationOptions = computed(() => [
  ...(sample.value?.rows ?? []).map((_, index) => ({ label: `Example ${index + 1}`, value: index })),
  { label: 'New observation', value: -1 },
]);
const isSurvival = computed(() => Boolean(project.value?.survivalModel));
const context = computed<Record<string, unknown>>(() => {
  try {
    const value = JSON.parse(project.value?.creationContextJson || '{}');
    return value && typeof value === 'object' && !Array.isArray(value) ? value : {};
  } catch { return {}; }
});
const timeUnit = computed(() => displayFollowUpUnit(context.value.timeUnit));
const method = computed(() => isSurvival.value && !rawSurvivalOutput.value ? 'predict_survival_function' : 'predict');
const row = computed(() => Object.fromEntries(fields.value.map((field) => {
  const value = draft.value[field.name];
  // IAML applies its fitted schema; preserve CSV strings and codes such as "001".
  return [field.name, value === null || value === undefined || value === '' ? null : value];
})));
const missingCount = computed(() => Object.values(row.value).filter((value) => value === null).length);
const requestKey = computed(() => !loading.value && !loadError.value && fields.value.length
  ? JSON.stringify({ modelId: modelId.value, row: row.value, method: method.value }) : '');
const numberFormat = new Intl.NumberFormat('en-US', { maximumSignificantDigits: 6 });
const percentFormat = new Intl.NumberFormat('en-US', { style: 'percent', maximumFractionDigits: 1 });
const formatNumber = (value: number) => numberFormat.format(value);
const survivalProbability = computed(() => curve.value && selectedTime.value !== undefined
  ? survivalAt(curve.value, selectedTime.value) : undefined);
const displayPrediction = computed(() => {
  if (curve.value) return survivalProbability.value === undefined ? 'Unavailable' : percentFormat.format(survivalProbability.value);
  if (prediction.value === null || prediction.value === '') return 'Unavailable';
  return typeof prediction.value === 'number' ? formatNumber(prediction.value) : String(prediction.value);
});
const resultLabel = computed(() => isSurvival.value
  ? rawSurvivalOutput.value ? 'Raw model output' : 'Estimated probability of remaining event-free'
  : project.value?.csvColumnToPredict || 'Predicted result');
const statusText = computed(() => state.value === 'waiting' ? 'Updating after your input…'
  : state.value === 'calculating' ? 'Computing prediction…'
    : state.value === 'ready' ? 'Matches the current form values'
      : state.value === 'invalid' ? 'Waiting for valid values'
        : state.value === 'error' ? 'Prediction unavailable'
        : fields.value.length ? 'Enter an observation to see its prediction' : 'No features available');
const timeIndex = computed({
  get: () => Math.max(0, curve.value?.times.indexOf(selectedTime.value!) ?? 0),
  set: (index: number) => { selectedTime.value = curve.value?.times[index]; },
});
const survivalPath = computed(() => {
  if (!curve.value) return '';
  const { times, values } = curve.value;
  const span = times[times.length - 1] - times[0];
  const x = (time: number) => span ? 8 + (time - times[0]) / span * 284 : 150;
  const y = (value: number) => 8 + (1 - value) * 94;
  return `M ${x(times[0])} ${y(values[0])}` + times.slice(1).map((time, index) =>
    ` H ${x(time)} V ${y(values[index + 1])}`).join('');
});
const horizonX = computed(() => {
  if (!curve.value || selectedTime.value === undefined) return 0;
  const { times } = curve.value;
  const span = times[times.length - 1] - times[0];
  return span ? 8 + (selectedTime.value - times[0]) / span * 284 : 150;
});

let loadRevision = 0;
let predictionRevision = 0;
let loadController: AbortController | undefined;
let predictionController: AbortController | undefined;
let debounce: ReturnType<typeof setTimeout> | undefined;
let disposed = false;

function invalidatePrediction() {
  predictionRevision += 1;
  if (debounce) clearTimeout(debounce);
  debounce = undefined;
  predictionController?.abort();
  predictionController = undefined;
  prediction.value = null;
  curve.value = undefined;
  predictionFailure.value = undefined;
}

function useObservation(index: number) {
  selectedObservation.value = index;
  const source = index >= 0 ? sample.value?.rows[index] : undefined;
  draft.value = Object.fromEntries(fields.value.map((field) => {
    const value = source?.[field.name];
    return [field.name, value === null || value === undefined || value === '' ? null : String(value)];
  }));
}

async function load() {
  const revision = ++loadRevision;
  loadController?.abort();
  const controller = new AbortController();
  loadController = controller;
  loading.value = true;
  invalidatePrediction();
  state.value = 'idle';
  project.value = undefined;
  sample.value = undefined;
  model.value = undefined;
  draft.value = {};
  loadError.value = '';
  fieldSearch.value = '';
  rawSurvivalOutput.value = false;
  selectedTime.value = undefined;
  try {
    const [projectResponse, sampleResponse, modelResponse] = await Promise.all([
      api.get<IProject>(`Projects/${projectId.value}`, { signal: controller.signal }),
      api.get<ICsvProject>(`Projects/${projectId.value}/data-sample`, { signal: controller.signal }),
      api.get<IModel>(`Models/${modelId.value}`, { signal: controller.signal }),
    ]);
    if (disposed || revision !== loadRevision) return;
    if (!Array.isArray(sampleResponse.data?.columns) || !Array.isArray(sampleResponse.data?.rows))
      throw new Error('The features for this study could not be loaded.');
    project.value = projectResponse.data;
    sample.value = sampleResponse.data;
    model.value = modelResponse.data;
    useObservation(sample.value.rows.length ? 0 : -1);
  } catch (error) {
    if (!controller.signal.aborted && !disposed && revision === loadRevision) loadError.value = apiError(error);
  } finally {
    if (!disposed && revision === loadRevision) loading.value = false;
  }
}

async function calculate(key = requestKey.value) {
  if (!key || disposed) return;
  if (debounce) clearTimeout(debounce);
  debounce = undefined;
  const revision = ++predictionRevision;
  predictionController?.abort();
  const controller = new AbortController();
  predictionController = controller;
  const request = JSON.parse(key) as { modelId: number; row: Record<string, string | null>; method: string };
  state.value = 'calculating';
  predictionFailure.value = undefined;
  try {
    const response = await predictValueUsingAiModel(request.modelId, {
      json_row: [request.row], method: request.method,
    }, controller.signal);
    if (disposed || controller.signal.aborted || revision !== predictionRevision || key !== requestKey.value) return;
    if (request.method === 'predict_survival_function') {
      const next = parseSurvivalCurve(response);
      curve.value = next;
      if (selectedTime.value === undefined || !next.times.includes(selectedTime.value))
        selectedTime.value = next.times[Math.floor((next.times.length - 1) / 2)];
    } else prediction.value = parseSinglePrediction(response);
    state.value = 'ready';
  } catch (error) {
    if (disposed || controller.signal.aborted || revision !== predictionRevision || key !== requestKey.value) return;
    prediction.value = null;
    curve.value = undefined;
    predictionFailure.value = describePredictionFailure(error);
    state.value = predictionFailure.value.kind === 'invalid' ? 'invalid' : 'error';
  } finally {
    if (predictionController === controller) predictionController = undefined;
  }
}

watch(requestKey, (key) => {
  invalidatePrediction();
  state.value = key ? 'waiting' : 'idle';
  if (key) debounce = setTimeout(() => { void calculate(key); }, 450);
}, { flush: 'sync' });
watch([projectId, modelId, processId], load, { immediate: true });
onUnmounted(() => {
  disposed = true;
  loadRevision += 1;
  loadController?.abort();
  invalidatePrediction();
});
</script>

<template>
  <q-page class="prediction-page project-workspace-colors">
    <div class="prediction-workspace">
      <q-btn flat dense no-caps icon="arrow_back" label="Back to model results" :to="modelLocation" class="back-link" />
      <header class="prediction-heading">
        <p class="section-eyebrow">Try your model</p>
        <h1>What does it predict for this observation?</h1>
        <p>Change the values: the prediction updates automatically after a short pause in typing.</p>
        <span v-if="model" class="prediction-context">{{ displayServiceName(model.metrics.model_info?.name) || `Model #${modelId}` }}</span>
      </header>
      <div v-if="loading" class="prediction-loading" role="status"><q-spinner color="primary" size="28px" /><p>Preparing the form…</p></div>
      <div v-else-if="loadError" class="prediction-load-error" role="alert"><q-icon name="error_outline" size="22px" /><div><strong>Unable to prepare this prediction</strong><p>{{ loadError }}</p></div><q-btn flat no-caps label="Try again" color="primary" @click="load" /></div>
      <div v-else class="prediction-content">
        <div class="prediction-layout">
          <section class="prediction-form" aria-labelledby="observation-title">
            <div class="form-heading"><div><h2 id="observation-title">Your observation</h2><p>{{ fields.length }} feature{{ fields.length !== 1 ? 's' : '' }} used by the model.</p></div><q-btn v-if="fields.length" flat dense no-caps icon="restart_alt" label="Reset" color="primary" @click="useObservation(selectedObservation)" /></div>
            <q-select :model-value="selectedObservation" :options="observationOptions" emit-value map-options outlined dense label="Starting point" class="observation-select" popup-content-class="project-workspace-colors" @update:model-value="useObservation" />
            <p class="example-note">{{ selectedObservation >= 0 ? 'An example from your study preview. You can change each value.' : 'Enter the details of the observation you want to try.' }}</p>
            <q-input v-if="fields.length > 8" v-model="fieldSearch" outlined dense clearable label="Search for a feature" class="field-search"><template #prepend><q-icon name="search" size="19px" /></template></q-input>
            <div class="prediction-fields">
              <div v-for="(field, index) in shownFields" :key="field.name" class="prediction-field" :data-column="field.name">
                <q-input v-model="draft[field.name]" outlined clearable :label="field.label" :inputmode="field.kind === 'numeric' ? 'decimal' : 'text'" :list="field.suggestions.length ? `prediction-examples-${index}` : undefined" :placeholder="field.suggestions[0] ? `e.g. ${field.suggestions[0]}` : undefined" hide-bottom-space autocomplete="off" />
                <datalist v-if="field.suggestions.length" :id="`prediction-examples-${index}`"><option v-for="example in field.suggestions" :key="example" :value="example" /></datalist>
              </div>
            </div>
            <p v-if="fields.length && !shownFields.length" class="form-empty">No features match your search.</p>
            <p v-else-if="!fields.length" class="form-empty">No input features are available in this preview.</p>
            <p class="missing-note">An empty field is sent as a missing value. Whether it is accepted depends on the trained model.<span v-if="missingCount">{{ ` ${missingCount} field${missingCount !== 1 ? 's' : ''} currently empty.` }}</span></p>
          </section>
          <aside class="prediction-result" :aria-busy="state === 'waiting' || state === 'calculating'" aria-labelledby="prediction-result-title">
            <div class="result-heading"><q-icon name="insights" size="20px" aria-hidden="true" /><h2 id="prediction-result-title">Model prediction</h2><span class="live-tag"><span />Live</span></div>
            <div class="prediction-status" :class="`prediction-status--${state}`" role="status" aria-live="polite"><q-spinner v-if="state === 'waiting' || state === 'calculating'" size="16px" color="primary" /><q-icon v-else-if="state === 'ready'" name="check_circle_outline" size="16px" /><span>{{ statusText }}</span></div>
            <template v-if="state === 'ready'">
              <p class="result-label">{{ resultLabel }}</p>
              <div class="prediction-value" :class="{ 'prediction-value--empty': displayPrediction === 'Unavailable' }" role="status" aria-live="polite">{{ displayPrediction }}</div>
              <template v-if="curve && selectedTime !== undefined">
                <p class="survival-horizon">At time <strong>{{ formatNumber(selectedTime) }}{{ timeUnit ? ` ${timeUnit}` : '' }}</strong><span v-if="!timeUnit">{{ ' (file units)' }}</span></p>
                <div v-if="curve.times.length > 1" class="survival-controls"><label>Choose a time horizon on the model curve</label><q-slider v-model="timeIndex" :min="0" :max="curve.times.length - 1" :step="1" color="primary" aria-label="Survival prediction time horizon" /><div class="survival-axis"><span>{{ formatNumber(curve.times[0]) }}</span><span>{{ formatNumber(curve.times[curve.times.length - 1]) }}{{ timeUnit ? ` ${timeUnit}` : '' }}</span></div></div>
                <svg class="survival-curve" viewBox="0 0 300 110" role="img" aria-label="Estimated probability of remaining event-free at the times returned by the model"><line x1="8" y1="102" x2="292" y2="102" class="curve-axis" /><line :x1="horizonX" y1="4" :x2="horizonX" y2="102" class="curve-horizon" /><path :d="survivalPath" class="curve-path" /></svg>
              </template>
              <p v-else-if="prediction === null || prediction === ''" class="result-explanation">The service did not return a value for this observation.</p>
              <p v-else-if="isSurvival" class="result-explanation">The meaning of this output depends on the selected estimator. Review its method to interpret it.</p>
              <p v-else class="result-explanation">{{ context.predictionKind === 'quantity' ? 'Estimated quantity for this observation, in the units used by your data.' : context.predictionKind === 'category' ? 'Predicted category for this observation.' : 'Value returned by the model for this observation.' }}</p>
            </template>
            <div v-else-if="state === 'invalid'" class="prediction-invalid" role="status" aria-live="polite"><strong>{{ predictionFailure?.message }}</strong><p>Check the entered values to display the prediction.</p></div>
            <div v-else-if="state === 'error'" class="prediction-error" role="alert"><q-icon name="info_outline" size="21px" /><p>{{ predictionFailure?.message }}</p><q-btn flat dense no-caps label="Try again" color="primary" @click="calculate()" /></div>
            <div v-else class="prediction-placeholder"><span>—</span><p>{{ state === 'idle' ? 'The prediction will appear here.' : 'The result updates with your new values.' }}</p></div>
            <q-btn v-if="isSurvival && predictionFailure?.kind === 'unsupported' && !rawSurvivalOutput" flat no-caps dense color="primary" label="Try the raw model output" class="survival-fallback" @click="rawSurvivalOutput = true" />
            <q-btn v-else-if="isSurvival && rawSurvivalOutput" flat no-caps dense color="primary" label="Return to the survival curve" class="survival-fallback" @click="rawSurvivalOutput = false" />
            <div class="result-footer"><q-icon name="tune" size="17px" /><p>Only input features are sent to the model. These predictions do not change your data or the training run.</p></div>
          </aside>
        </div>
        <PredictionExplanation
          v-if="!isSurvival"
          :key="modelId"
          :model-id="modelId"
          :observation-key="requestKey"
          :row="row"
          :valid="state === 'ready' && prediction !== null && prediction !== ''"
        />
      </div>
    </div>
  </q-page>
</template>

<style scoped>
.prediction-page { padding: 28px 32px 48px; color: var(--project-ink); }
.prediction-workspace { max-width: 1220px; min-width: 0; margin: 0 auto; }
.back-link { color: var(--q-primary); margin-bottom: 24px; }
.section-eyebrow { margin: 0 0 8px; color: var(--q-primary); font-size: 11px; font-weight: 600; letter-spacing: .075em; text-transform: uppercase; }
.prediction-heading h1 { margin: 0 0 12px; font-size: clamp(25px, 2.8vw, 34px); line-height: 1.3; font-weight: 600; letter-spacing: -.025em; }
.prediction-heading > p:not(.section-eyebrow) { margin: 0; color: var(--project-muted); font-size: 14px; line-height: 1.7; }
.prediction-context { display: block; font-size: 12px; color: var(--project-muted); margin-top: 10px; overflow-wrap: anywhere; }
.prediction-loading { display: grid; justify-items: center; gap: 12px; padding: 70px 20px; color: var(--project-muted); }
.prediction-load-error { display: flex; gap: 12px; align-items: flex-start; margin-top: 24px; padding: 16px; color: var(--project-error-ink); border: 1px solid var(--project-error-border); background: var(--project-error-surface); border-radius: 10px; }
.prediction-load-error > div { flex: 1; min-width: 0; }
.prediction-load-error p { margin: 6px 0 0; line-height: 1.7; overflow-wrap: anywhere; }
.prediction-content { display: grid; gap: 24px; margin-top: 28px; }
.prediction-layout { display: grid; grid-template-columns: minmax(0, 1.55fr) minmax(320px, 1fr); align-items: start; gap: 24px; }
.prediction-form, .prediction-result { min-width: 0; border: 1px solid var(--project-border); background: var(--project-surface); border-radius: 12px; }
.prediction-form { padding: 24px; }
.form-heading { display: flex; justify-content: space-between; align-items: flex-start; gap: 12px; margin-bottom: 20px; }
.form-heading h2 { font-size: 19px; font-weight: 600; line-height: 1.5; margin: 0; }
.form-heading p { font-size: 12px; color: var(--project-muted); line-height: 1.6; margin: 4px 0 0; }
.form-heading .q-btn { font-size: 11px; }
.example-note { margin: 10px 0 24px; font-size: 12px; color: var(--project-muted); line-height: 1.7; }
.field-search { margin-bottom: 18px; }
.prediction-fields { display: grid; grid-template-columns: repeat(2, minmax(0, 1fr)); gap: 18px; }
.prediction-field { min-width: 0; }
.prediction-field :deep(.q-field__label) { font-size: 13px; max-width: calc(100% - 16px); text-overflow: ellipsis; overflow: hidden; }
.prediction-field :deep(.q-field__native) { font-size: 14px; }
.missing-note { margin: 24px 0 0; padding-top: 18px; border-top: 1px solid var(--project-border); font-size: 11px; color: var(--project-muted); line-height: 1.75; }
.form-empty { color: var(--project-muted); font-size: 13px; padding: 12px 0; }
.prediction-result { position: sticky; top: 24px; overflow: hidden; padding: 24px; box-shadow: 0 5px 20px var(--project-shadow); border-top: 3px solid var(--project-brand); }
.result-heading { display: flex; align-items: center; gap: 9px; flex-wrap: wrap; }
.result-heading > .q-icon { color: var(--q-primary); }
.result-heading h2 { font-size: 15px; font-weight: 600; line-height: 1.6; margin: 0; }
.live-tag { display: flex; align-items: center; gap: 5px; margin-left: auto; padding: 3px 6px; font-size: 10px; color: var(--q-primary); background: var(--project-tint); border-radius: 4px; }
.live-tag > span { width: 5px; height: 5px; border-radius: 50%; background: currentColor; }
.prediction-status { display: flex; align-items: flex-start; gap: 7px; color: var(--project-muted); font-size: 11px; line-height: 1.7; margin-top: 15px; }
.prediction-status .q-icon, .prediction-status .q-spinner { flex-shrink: 0; margin-top: 1px; }
.prediction-status--ready { color: var(--q-primary); }
.result-label { margin: 28px 0 8px; font-size: 13px; line-height: 1.7; overflow-wrap: anywhere; }
.prediction-value { font-size: clamp(35px, 4.1vw, 58px); line-height: 1.2; font-weight: 600; letter-spacing: -.025em; color: var(--q-primary); overflow-wrap: anywhere; font-variant-numeric: tabular-nums; }
.prediction-value--empty { font-size: 25px; }
.result-explanation, .survival-horizon { margin: 14px 0 0; color: var(--project-muted); font-size: 12px; line-height: 1.75; }
.survival-horizon strong { color: var(--project-ink); font-weight: 500; }
.survival-controls { margin-top: 24px; }
.survival-controls label { display: block; font-size: 11px; color: var(--project-muted); margin-bottom: 6px; }
.survival-axis { display: flex; justify-content: space-between; gap: 12px; font-size: 10px; color: var(--project-muted); }
.survival-curve { width: 100%; height: auto; margin-top: 16px; }
.curve-axis { stroke: var(--project-border); stroke-width: 1; }
.curve-horizon { stroke: var(--project-muted); stroke-width: 1; stroke-dasharray: 3 3; }
.curve-path { fill: none; stroke: var(--q-primary); stroke-width: 2; stroke-linejoin: round; }
.prediction-placeholder { padding: 28px 0 22px; color: var(--project-muted); }
.prediction-placeholder > span { font-size: 52px; line-height: 1.3; color: var(--project-border); }
.prediction-placeholder p { margin: 8px 0 0; font-size: 12px; line-height: 1.7; }
.prediction-invalid { padding: 28px 0 22px; color: var(--project-muted); }
.prediction-invalid strong { font-size: 19px; line-height: 1.5; font-weight: 500; }
.prediction-invalid p { margin: 8px 0 0; font-size: 12px; line-height: 1.7; }
.prediction-error { display: flex; flex-wrap: wrap; align-items: flex-start; gap: 10px; padding: 16px; margin-top: 24px; color: var(--project-muted); background: var(--project-paper); border-radius: 8px; }
.prediction-error .q-icon { flex-shrink: 0; }
.prediction-error p { flex: 1; min-width: 0; margin: 0; font-size: 12px; line-height: 1.7; overflow-wrap: anywhere; }
.prediction-error .q-btn { margin-left: 30px; font-size: 11px; }
.survival-fallback { margin-top: 16px; font-size: 11px; max-width: 100%; }
.survival-fallback :deep(.q-btn__content) { white-space: normal; text-align: left; }
.result-footer { display: flex; align-items: flex-start; gap: 8px; margin-top: 26px; padding-top: 18px; border-top: 1px solid var(--project-border); color: var(--project-muted); }
.result-footer .q-icon { flex-shrink: 0; margin-top: 1px; }
.result-footer p { font-size: 11px; line-height: 1.75; margin: 0; }
@media (max-width: 899px) {
  .prediction-content { gap: 20px; }
  .prediction-layout { grid-template-columns: minmax(0, 1fr); gap: 20px; }
  .prediction-result { grid-row: 1; position: static; top: auto; }
  .prediction-fields { grid-template-columns: repeat(2, minmax(0, 1fr)); }
  .result-label { margin-top: 18px; }
}
@media (max-width: 599px) {
  .prediction-page { padding: 18px 16px 36px; }
  .back-link { margin-bottom: 20px; max-width: 100%; }
  .back-link :deep(.q-btn__content) { white-space: normal; text-align: left; }
  .prediction-form, .prediction-result { padding: 20px 16px; }
  .prediction-fields { grid-template-columns: minmax(0, 1fr); gap: 16px; }
  .form-heading { gap: 8px; }
  .form-heading h2 { font-size: 17px; }
  .form-heading .q-btn { font-size: 10px; }
  .prediction-value { font-size: 44px; }
  .prediction-value--empty { font-size: 23px; }
  .prediction-load-error { flex-wrap: wrap; }
  .prediction-load-error .q-btn { margin-left: 32px; }
}
</style>
