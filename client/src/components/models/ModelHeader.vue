<script setup lang="ts">
import { computed } from 'vue';
import { formatToDDMMYYYHHmm } from 'src/utils/date';
import { IModel } from '../models';
import { displayServiceName } from 'src/utils/service-label';

const props = defineProps<{
  model: IModel;
  routeParams: { projectId: number; processId: number; modelId: number };
  returnView?: 'overview' | 'training' | 'data' | 'configuration';
}>();
const created = computed(() => props.model.date && Number.isFinite(Date.parse(props.model.date))
  ? formatToDDMMYYYHHmm(props.model.date) : undefined);
const evaluationSource = computed(() => {
  if (props.model.metrics.metrics_test && Object.values(props.model.metrics.metrics_test).some((score) => typeof score === 'number' && Number.isFinite(score)))
    return 'Évalué sur un jeu de test indépendant';
  if (props.model.metrics.score_source === 'cross_validation') return 'Évalué par validation croisée';
  if (props.model.metrics.score_source === 'in_sample') return 'Évalué sur les données d’entraînement';
  return 'Consultez la source des scores dans les résultats';
});
</script>

<template>
  <header class="model-header">
    <div class="model-identity">
      <span class="model-eyebrow">Modèle entraîné</span>
      <h1>{{ displayServiceName(model.metrics.model_info?.name) || `Modèle nº ${model.id}` }}</h1>
      <p v-if="model.metrics.model_info?.description" class="model-description">{{ model.metrics.model_info.description }}</p>
      <div class="model-metadata">
        <span v-if="created"><q-icon name="event" size="16px" />{{ created }}</span>
        <span><q-icon name="fact_check" size="16px" />{{ evaluationSource }}</span>
      </div>
    </div>
    <div class="model-primary-action">
      <q-btn
        unelevated
        no-caps
        color="primary"
        icon="science"
        label="Essayer une prédiction"
        :to="{
          name: 'model-test',
          params: { id: routeParams.projectId, processId: routeParams.processId, modelId: routeParams.modelId },
          query: { returnView: returnView ?? 'training' },
        }"
      />
      <p>Essayer une prédiction sur des observations.</p>
    </div>
  </header>
</template>

<style scoped>
.model-header { display: flex; justify-content: space-between; align-items: flex-start; gap: 28px; min-width: 0; }
.model-identity { flex: 1 1 auto; min-width: 0; }
.model-eyebrow { display: block; font-size: 11px; font-weight: 700; letter-spacing: .09em; text-transform: uppercase; color: var(--q-primary); }
h1 { margin: 8px 0 10px; font-size: clamp(25px, 2.8vw, 35px); line-height: 1.2; font-weight: 600; letter-spacing: -.025em; overflow-wrap: anywhere; }
.model-description { margin: 0; max-width: 780px; font-size: 14px; line-height: 1.65; color: var(--project-muted); overflow-wrap: anywhere; }
.model-metadata { display: flex; align-items: center; flex-wrap: wrap; gap: 10px 20px; margin-top: 18px; color: var(--project-muted); font-size: 12px; }
.model-metadata span { display: flex; align-items: center; gap: 7px; }
.model-metadata .q-icon { flex-shrink: 0; color: var(--q-primary); }
.model-primary-action { flex: 0 1 240px; padding-top: 14px; min-width: 0; }
.model-primary-action .q-btn { min-height: 44px; max-width: 100%; border-radius: 9px; }
.model-primary-action p { margin: 8px 0 0; font-size: 12px; line-height: 1.5; color: var(--project-muted); }
@media (max-width: 700px) {
  .model-header { flex-direction: column; gap: 18px; }
  .model-primary-action { flex: 0 0 auto; padding-top: 0; width: 100%; }
  .model-primary-action .q-btn { width: 100%; }
  .model-primary-action p { text-align: center; }
  .model-metadata { margin-top: 14px; gap: 8px; flex-direction: column; align-items: flex-start; }
}
</style>
