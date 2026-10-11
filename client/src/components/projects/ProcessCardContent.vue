<script setup lang="ts">
import { computed } from 'vue';
import { displayServiceText, formatScore, metricKey, metricScore } from 'src/utils/iaml';
import { formatToDDMMYYYHHmm } from 'src/utils/date';
import { IProcess } from '../models';

const propsComponent = defineProps<{
  process?: IProcess
}>();

const firstModel = computed(() => propsComponent.process?.models?.[0]);
const mainMetricName = computed(() => {
  const metrics = firstModel.value?.metrics;
  return metrics ? metrics.metrics_info?.[metricKey(metrics)]?.name ?? metricKey(metrics)
    : propsComponent.process?.statusSnapshot?.objective?.key;
});
const mainMetricValue = computed(() => firstModel.value
  ? metricScore(firstModel.value.metrics)
  : propsComponent.process?.statusSnapshot?.best_test_metric ?? propsComponent.process?.statusSnapshot?.best_train_metric ?? null);
const mainMetricDirection = computed(() => {
  const metrics = firstModel.value?.metrics;
  const greater = metrics ? metrics.objective?.greater_is_better ?? metrics.metrics_info?.[metricKey(metrics)]?.greater_is_better
    : propsComponent.process?.statusSnapshot?.objective?.greater_is_better;
  return greater === undefined ? '' : greater ? 'Maximiser' : 'Minimiser';
});
const processError = computed(() => propsComponent.process?.error ?? propsComponent.process?.statusSnapshot?.error);
const processMessages = computed(() => [...new Set([
  processError.value,
  propsComponent.process?.cacheError ?? propsComponent.process?.statusSnapshot?.cache_error,
].filter((message): message is string => typeof message === 'string' && message.length > 0))].map(displayServiceText).join('\n\n'));
</script>

<template>
  <q-item
    v-if="process"
    class="process-header full-width"
  >
    <div class="process-date">
      <q-icon name="date_range" />
      {{ formatToDDMMYYYHHmm(process?.date) }}
    </div>
    <div class="process-duration">
      <q-icon name="timer" />
      {{ process?.maxDuration }}s
    </div>
    <div class="process-score">
      <q-icon name="rocket_launch">
        <q-tooltip>{{ mainMetricDirection }}</q-tooltip>
      </q-icon>
      Best {{ mainMetricName }} : <span class="metric-value">{{ formatScore(mainMetricValue) }}</span>
    </div>
    <div class="process-status">
      <q-chip
        v-if="process"
        square
        :color="{
          'FINISHED': 'green',
          'CACHED': 'green',
          'CANCELED': 'red',
          'CRASHED': 'red',
          'QUEUED': 'blue',
          'RUNNING': 'orange',
          'TIMEOUT': 'grey'
        }[process.status]"
        text-color="white"
        :icon="{
          'FINISHED': 'check',
          'CACHED': 'check',
          'CANCELED': 'close',
          'CRASHED': 'error',
          'QUEUED': 'hourglass_top',
          'RUNNING': 'rocket_launch',
          'TIMEOUT': 'hourglass_disabled'

        }[process.status]"
        :label="process?.status"
      >
        <q-tooltip v-if="processMessages" class="process-tooltip">{{ processMessages }}</q-tooltip>
      </q-chip>
    </div>

    <!-- <q-space />

    <q-btn
      @click="dialog = true"
      icon="refresh"
      color="primary"
    >
      <q-tooltip>Relancer le process</q-tooltip>
    </q-btn> -->
  </q-item>

  <q-item v-else>
    <div class="text-caption text-italic">
      Aucun process actuellement disponible
    </div>
  </q-item>
</template>

<style scoped>
.process-header {
  display: grid;
  grid-template-columns: minmax(0, 2fr) minmax(0, 2fr) minmax(0, 5fr) auto;
  align-items: center;
  gap: 8px;
  min-width: 0;
}

.process-date,
.process-duration,
.process-score,
.process-status {
  min-width: 0;
  overflow-wrap: anywhere;
}

.process-date,
.process-duration,
.metric-value {
  font-variant-numeric: tabular-nums;
}

.process-status .q-chip {
  max-width: 100%;
  margin: 0;
}

.process-tooltip {
  max-width: min(420px, calc(100vw - 32px));
  white-space: pre-wrap;
  overflow-wrap: anywhere;
}

@media (max-width: 599px) {
  .process-header {
    grid-template-columns: minmax(0, 1fr) auto;
  }
}
</style>
