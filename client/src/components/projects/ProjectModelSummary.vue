<script setup lang="ts">
import { computed } from 'vue';
import type { ProjectResult, ProjectScore } from 'src/utils/project-results';
import { formatScore } from 'src/utils/iaml';
import { displayServiceName } from 'src/utils/service-label';

const props = defineProps<{
  result: ProjectResult;
  projectId: number;
  returnView: string;
  validation?: ProjectScore;
  test?: ProjectScore;
  historical?: boolean;
}>();

const modelName = computed(() => displayServiceName(props.result.model.metrics.model_info?.name) || 'Modèle disponible');
const hasTestScore = computed(() => typeof props.test?.value === 'number' && Number.isFinite(props.test.value));
const modelParams = computed(() => ({
  id: props.projectId,
  processId: props.result.process.id,
  modelId: props.result.model.id,
}));
const predictionLink = computed(() => ({
  name: 'model-test',
  params: modelParams.value,
  query: { returnView: props.returnView },
}));
function modelLink(view: 'performance' | 'explanation') {
  return {
    name: 'model-show',
    params: modelParams.value,
    query: { view, returnView: props.returnView },
  };
}
</script>

<template>
  <article class="project-model-summary" aria-label="Résumé du modèle et accès à son analyse">
    <div class="summary-heading">
      <p class="summary-eyebrow">{{ result.selectedByIaml ? 'Modèle retenu par DrIA' : 'Modèle à examiner' }}</p>
      <span v-if="historical" class="summary-history">Résultat antérieur</span>
    </div>

    <div class="summary-result">
      <div class="summary-model">
        <h3>{{ modelName }}</h3>
        <p v-if="!result.selectedByIaml" class="summary-selection">{{ result.selectionLabel }}</p>
      </div>
      <div class="summary-score">
        <span class="summary-source">{{ validation?.label || 'Évaluation interne' }}</span>
        <strong :class="{ 'score-unavailable': validation?.value === null || validation?.value === undefined }">{{ formatScore(validation?.value) }}</strong>
        <span class="summary-metric">{{ validation?.name || 'Critère non précisé' }}</span>
      </div>
    </div>

    <p v-if="hasTestScore" class="summary-test">
      <span>Test indépendant <span class="test-metric">· {{ test?.name }}</span></span>
      <strong>{{ formatScore(test?.value) }}</strong>
    </p>

    <div class="summary-actions">
      <q-btn unelevated no-caps color="primary" icon="science" label="Essayer une prédiction" :to="predictionLink" class="summary-predict" />
      <q-btn flat no-caps color="primary" label="Analyser ce modèle" icon-right="arrow_forward" :to="modelLink('performance')" />
      <q-btn flat no-caps color="primary" icon="psychology" label="Comprendre ses prédictions" :to="modelLink('explanation')" />
    </div>
  </article>
</template>

<style scoped>
.project-model-summary {
  width: 520px;
  max-width: 100%;
  min-width: 0;
  padding: 15px 17px 12px;
  border: 1px solid var(--project-border);
  border-radius: 10px;
  background: var(--project-surface);
  color: var(--project-ink);
  box-shadow: 0 3px 14px var(--project-shadow);
}
.summary-heading { display: flex; align-items: baseline; justify-content: space-between; flex-wrap: wrap; gap: 5px 12px; margin-bottom: 9px; }
.summary-eyebrow { margin: 0; color: var(--q-primary); font-size: 10px; line-height: 1.5; font-weight: 600; letter-spacing: .06em; text-transform: uppercase; }
.summary-history { padding: 2px 6px; border-radius: 4px; background: var(--project-paper); color: var(--project-muted); font-size: 10px; line-height: 1.5; }
.summary-result { display: grid; grid-template-columns: minmax(0, 1.25fr) minmax(0, 1fr); gap: 18px; align-items: center; }
.summary-model { min-width: 0; }
.summary-model h3 { margin: 0; font-size: 18px; font-weight: 600; line-height: 1.4; letter-spacing: -.015em; overflow-wrap: anywhere; }
.summary-selection { margin: 5px 0 0; color: var(--project-muted); font-size: 11px; line-height: 1.5; overflow-wrap: anywhere; }
.summary-score { display: flex; flex-direction: column; align-items: flex-end; min-width: 0; padding-left: 15px; border-left: 1px solid var(--project-border); text-align: right; }
.summary-source { color: var(--project-muted); font-size: 10px; line-height: 1.5; overflow-wrap: anywhere; }
.summary-score strong { max-width: 100%; margin: 2px 0 1px; color: var(--q-primary); font-size: 25px; line-height: 1.25; font-weight: 600; font-variant-numeric: tabular-nums; overflow-wrap: anywhere; }
.summary-score strong.score-unavailable { font-size: 16px; line-height: 1.5; }
.summary-metric { font-size: 11px; font-weight: 500; line-height: 1.5; overflow-wrap: anywhere; }
.summary-test { display: flex; justify-content: space-between; gap: 12px; align-items: baseline; margin: 10px 0 0; padding-top: 9px; border-top: 1px solid var(--project-border); font-size: 11px; line-height: 1.5; }
.summary-test > span { min-width: 0; color: var(--project-muted); overflow-wrap: anywhere; }
.test-metric { color: var(--project-ink); }
.summary-test strong { max-width: 45%; font-weight: 600; text-align: right; font-variant-numeric: tabular-nums; overflow-wrap: anywhere; }
.summary-actions { display: grid; grid-template-columns: minmax(0, 1fr) minmax(0, 1.35fr); gap: 4px 6px; margin-top: 13px; }
.summary-actions .q-btn { min-width: 0; min-height: 32px; padding: 5px 8px; font-size: 11px; line-height: 1.5; border-radius: 6px; }
.summary-actions .summary-predict { grid-column: 1 / -1; min-height: 35px; margin-bottom: 1px; font-size: 12px; }
.summary-actions :deep(.q-btn__content) { min-width: 0; flex-wrap: nowrap; white-space: normal; }
.summary-actions :deep(.q-btn__content > span) { min-width: 0; overflow-wrap: anywhere; }
.summary-actions :deep(.q-icon) { flex-shrink: 0; font-size: 18px; }
@media (max-width: 399px) {
  .project-model-summary { padding: 13px 14px 10px; }
  .summary-result { gap: 11px; }
  .summary-model h3 { font-size: 16px; }
  .summary-score { padding-left: 11px; }
  .summary-score strong { font-size: 23px; }
  .summary-actions { grid-template-columns: minmax(0, 1fr); }
  .summary-actions .summary-predict { grid-column: 1; }
}
</style>
