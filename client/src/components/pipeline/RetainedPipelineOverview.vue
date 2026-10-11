<script setup lang="ts">
import axios from 'axios';
import { computed, onUnmounted, ref, watch } from 'vue';
import { api } from 'src/boot/axios';
import { apiError } from 'src/utils/iaml';
import { displayServiceName } from 'src/utils/service-label';
import type { IModel, IRecipe } from '../models';
import PipelineOverview from './PipelineOverview.vue';

const props = defineProps<{ model: IModel; selectionLabel?: string }>();
const emit = defineEmits<{
  (event: 'audit-loaded', value: Record<string, unknown>): void;
  (event: 'report-loaded', value: Record<string, unknown>): void;
  (event: 'loading-change', loading: boolean): void;
}>();
const pipeline = ref<IRecipe>();
const loading = ref(false);
const loadError = ref('');
const omittedSteps = ref(0);
const modelName = computed(() => displayServiceName(props.model.metrics.model_info?.name) || `Model # ${props.model.id}`);
let controller: AbortController | undefined;
let requestVersion = 0;

function object(value: unknown): value is Record<string, unknown> {
  return value !== null && typeof value === 'object' && !Array.isArray(value);
}
function text(value: unknown): string {
  return typeof value === 'string' ? value.trim() : '';
}
function isOmitted(step: Record<string, unknown>): boolean {
  return text(step.class) === 'VoidStep'
    || text(step.node_id).endsWith(':absence')
    || text(step.variant_id).endsWith(':absence');
}
function stepDescription(step: Record<string, unknown>): string {
  const roles: Record<string, string> = {
    predictor: 'Model used for prediction.',
    transformer: 'Data preparation selected for this model.',
    resampler: 'Resampling used during training.',
  };
  const role = roles[text(step.role)] ?? 'Step recorded in this model’s pipeline.';
  const description = text(step.description);
  return description ? `${role} ${description}` : role;
}
function retainedPipeline(payload: unknown): { node: IRecipe; omitted: number } | undefined {
  if (!object(payload) || !object(payload.audit) || !Array.isArray(payload.audit.steps)) return undefined;
  const steps = payload.audit.steps;
  // Keep the recorded order, and never infer actual steps from the study's search space.
  if (!steps.length || !steps.every(object)) return undefined;
  const activeSteps = steps.filter((step) => !isOmitted(step));
  if (!activeSteps.length || activeSteps.some((step) => !text(step.alias) && !text(step.name) && !text(step.class))) return undefined;
  return {
    omitted: steps.length - activeSteps.length,
    node: {
      type: 'sequence', alias: 'Selected pipeline',
      description: 'These are the selected model’s steps, in execution order. Any resampling applies only during training.',
      children: activeSteps.map((step) => ({
        type: 'use',
        alias: text(step.alias) || text(step.name) || text(step.class),
        component: text(step.class) || undefined,
        role: text(step.role),
        description: stepDescription(step),
        params: object(step.configuration)
          ? Object.fromEntries(Object.entries(step.configuration).map(([key, value]) => [key, { type: 'const', value }]))
          : {},
      })),
    },
  };
}
function canUseReportFallback(error: unknown): boolean {
  return !axios.isCancel(error)
    && !(axios.isAxiosError(error) && [401, 403].includes(error.response?.status ?? 0));
}
function errorMessage(error: unknown): string {
  const message = apiError(error);
  if (axios.isAxiosError(error) && [401, 403].includes(error.response?.status ?? 0))
    return 'This pipeline is not accessible with your current session.';
  if (/introuvable|not found/i.test(message)) return 'Details for this model are no longer available.';
  return 'DrIA cannot load the selected pipeline right now.';
}
async function loadPipeline(): Promise<void> {
  controller?.abort();
  controller = new AbortController();
  const signal = controller.signal;
  const version = ++requestVersion;
  const modelId = props.model.id;
  pipeline.value = undefined;
  omittedSteps.value = 0;
  loadError.value = '';
  loading.value = true;
  emit('loading-change', true);
  let auditError: unknown;
  try {
    let retained: ReturnType<typeof retainedPipeline>;
    try {
      const response = await api.get(`Models/${modelId}/audit`, { signal });
      if (version !== requestVersion) return;
      if (object(response.data)) emit('audit-loaded', response.data);
      retained = retainedPipeline(response.data);
    } catch (error) {
      if (version !== requestVersion || signal.aborted) return;
      if (!canUseReportFallback(error)) throw error;
      auditError = error;
    }
    if (!retained) {
      try {
        const response = await api.get(`Models/${modelId}/report`, { params: { format: 'json' }, signal });
        if (version !== requestVersion) return;
        if (object(response.data)) emit('report-loaded', response.data);
        retained = retainedPipeline(response.data);
      } catch (error) {
        if (version !== requestVersion || signal.aborted) return;
        // An unavailable report is a missing-detail state when the audit itself was readable.
        if (auditError || !canUseReportFallback(error)) throw error;
      }
    }
    if (version !== requestVersion) return;
    pipeline.value = retained?.node;
    omittedSteps.value = retained?.omitted ?? 0;
  } catch (error) {
    if (version === requestVersion && !signal.aborted) loadError.value = errorMessage(error);
  } finally {
    if (version === requestVersion) {
      loading.value = false;
      emit('loading-change', false);
    }
  }
}
watch([() => props.model.id, () => props.model.metrics.id], () => { void loadPipeline(); }, { immediate: true });
onUnmounted(() => { requestVersion += 1; controller?.abort(); });
</script>

<template>
  <section class="retained-overview" aria-label="Selected model pipeline">
    <header class="retained-overview__heading">
      <div>
        <span class="retained-overview__eyebrow">The pipeline used by this model</span>
        <h3>{{ modelName }}</h3>
        <p v-if="selectionLabel" class="retained-overview__selection">{{ selectionLabel }}</p>
        <p>View the steps and settings used by this model.</p>
      </div>
    </header>
    <div v-if="loading" class="retained-overview__state" role="status" aria-live="polite">
      <q-spinner color="primary" size="24px" />
      <p>Loading selected pipeline…</p>
    </div>
    <div v-else-if="loadError" class="retained-overview__state" role="status" aria-live="polite">
      <q-icon name="info_outline" size="24px" />
      <div><p>{{ loadError }}</p><q-btn flat dense no-caps color="primary" label="Retry" icon="refresh" @click="loadPipeline" /></div>
    </div>
    <template v-else-if="pipeline">
      <PipelineOverview :node="pipeline" context="retained" />
      <p v-if="omittedSteps" class="retained-overview__omitted">
        {{ omittedSteps }} optional step{{ omittedSteps === 1 ? ' was' : 's were' }} not used by this model.
      </p>
    </template>
    <div v-else class="retained-overview__state" role="status">
      <q-icon name="route" size="24px" />
      <div>
        <p>The steps used by this model are unavailable.</p>
        <small>The pipeline will appear when DrIA can provide its details.</small>
        <q-btn flat dense no-caps color="primary" label="Retry" icon="refresh" @click="loadPipeline" />
      </div>
    </div>
  </section>
</template>

<style scoped>
.retained-overview { min-width: 0; color: var(--project-ink); }
.retained-overview__heading { margin-bottom: 22px; }
.retained-overview__eyebrow { display: block; color: var(--q-primary); font-size: 10px; font-weight: 600; margin-bottom: 7px; }
.retained-overview__heading h3 { margin: 0; font-size: 18px; font-weight: 600; line-height: 1.5; overflow-wrap: anywhere; }
.retained-overview__heading p { margin: 8px 0 0; color: var(--project-muted); font-size: 12px; line-height: 1.8; }
.retained-overview__heading .retained-overview__selection { color: var(--project-ink); }
.retained-overview__state { display: flex; align-items: flex-start; gap: 14px; padding: 24px; border: 1px solid var(--project-border); border-radius: 12px; background: var(--project-paper); }
.retained-overview__state > .q-icon { color: var(--project-muted); flex-shrink: 0; }
.retained-overview__state > .q-spinner { flex-shrink: 0; }
.retained-overview__state > div { min-width: 0; }
.retained-overview__state p { margin: 0; font-size: 13px; line-height: 1.8; overflow-wrap: anywhere; }
.retained-overview__state small { display: block; color: var(--project-muted); font-size: 12px; line-height: 1.8; margin-top: 6px; }
.retained-overview__state .q-btn { margin: 10px 0 0 -7px; }
.retained-overview__omitted { margin: 14px 0 0; color: var(--project-muted); font-size: 12px; line-height: 1.8; }
@media (max-width: 599px) { .retained-overview__heading h3 { font-size: 16px; } .retained-overview__state { padding: 18px; } }
</style>
