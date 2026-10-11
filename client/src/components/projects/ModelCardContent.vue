<script setup lang="ts">
import { formatToDDMMYYYHHmm } from 'src/utils/date';
import { IModel, IProcess } from '../models';
import { computed } from 'vue';
import { formatScore, metricKey } from 'src/utils/iaml';
import { displayServiceName } from 'src/utils/service-label';

const props = defineProps<{
  model: IModel;
  projectId: number;
  process: IProcess;
  baseline: boolean;
}>();

// The objective may be an alias and can be minimized; missing scores remain unavailable.
const sortedMetrics = computed(() => {
  const scores = props.model.metrics.metrics_test ?? props.model.metrics.metrics_train ?? {};
  return Object.entries(scores)
    .sort((a, b) => Number(b[0] === metricKey(props.model.metrics)) - Number(a[0] === metricKey(props.model.metrics)));
});
const scoreSource = computed(() => props.model.metrics.metrics_test
  ? 'Jeu de test indépendant'
  : props.model.metrics.score_source === 'in_sample' ? 'Données d’entraînement' : 'Validation croisée');
function metricDirection(key: string): string {
  const greater = key === metricKey(props.model.metrics)
    ? props.model.metrics.objective?.greater_is_better ?? props.model.metrics.metrics_info?.[key]?.greater_is_better
    : props.model.metrics.metrics_info?.[key]?.greater_is_better;
  return greater === undefined ? '' : greater ? 'Maximiser' : 'Minimiser';
}
</script>

<template>
  <div
    v-if="model"
  >
    <div
      v-if="!baseline"
      class="model-header"
    >
      <div
        class="model-name"
      >
        <q-icon
          name="smart_toy"
          class="q-mb-xs"
        />
        {{ displayServiceName(model.metrics.model_info?.name) }}
      </div>

      <div
        class="model-date"
      >
        <q-icon name="date_range" />
        {{ formatToDDMMYYYHHmm(model.date) }}
      </div>

      <div
        class="model-duration"
      >
        <q-icon name="timer" />
        {{ process.maxDuration }}s
      </div>
    </div>
    <div
      v-else
      class="model-header"
    >
      <div
        class="model-baseline-description"
      >
        <q-icon
          name="help"
          class="q-mb-xs"
        />
        Results of a model without Dr IA optimizations
      </div>
    </div>
    <div
      class="q-mt-md"
    >
      <q-item
        v-for="item in sortedMetrics"
        :key="item[0]"
        clickable
        class="row custom-q-item"
      >
        <q-item-section
          side
          v-if="!baseline"
        >
          <q-icon
            name="info"
            color="white"
          >
            <q-tooltip
              class="text-body2"
              anchor="center right"
              self="center left"
            >
              {{
                model.metrics.metrics_info?.[item[0]]?.desc ?? ''
              }}
              {{ metricDirection(item[0]) }} · {{ scoreSource }}
            </q-tooltip>
          </q-icon>
        </q-item-section>
        <q-item-section>
          <div
            class="metric-line"
            :class="{ 'metric-line--baseline': baseline }"
          >
            <div class="text-weight-medium metric-label">
              {{
                displayServiceName(model.metrics.metrics_info?.[item[0]]?.name ?? item[0])
              }}
            </div>
            <div class="text-weight-medium metric-value">
              {{ formatScore(item[1]) }}
            </div>
          </div>
        </q-item-section>
      </q-item>
    </div>
  </div>

  <q-item v-else>
    <q-item-label class="text-caption text-italic">
      Aucun models Dr IA actuellement disponible
    </q-item-label>
  </q-item>
</template>

<style scoped>
.model-header {
  display: flex;
  flex-wrap: wrap;
  align-items: baseline;
  gap: 4px 8px;
  min-width: 0;
}

.model-name,
.model-date {
  flex: 5 1 140px;
  min-width: 0;
  overflow-wrap: anywhere;
}

.model-date,
.model-duration,
.metric-value {
  font-variant-numeric: tabular-nums;
}

.model-duration {
  flex: 2 1 64px;
  min-width: 0;
  overflow-wrap: anywhere;
}

.model-baseline-description {
  min-width: 0;
  overflow-wrap: anywhere;
}

.custom-q-item {
  min-height: 28px;
  height: auto;
  padding-top: 2px;
  padding-bottom: 2px;
}

.metric-line {
  display: grid;
  grid-template-columns: minmax(0, 1fr) fit-content(60%);
  align-items: baseline;
  gap: 8px;
  min-width: 0;
}

.metric-label,
.metric-value {
  min-width: 0;
  overflow-wrap: anywhere;
}

.metric-value {
  text-align: right;
}

.metric-line--baseline {
  grid-template-columns: fit-content(60%) minmax(0, 1fr);
}

.metric-line--baseline .metric-label {
  grid-column: 2;
  grid-row: 1;
  text-align: right;
}

.metric-line--baseline .metric-value {
  grid-column: 1;
  grid-row: 1;
  text-align: left;
}
</style>
