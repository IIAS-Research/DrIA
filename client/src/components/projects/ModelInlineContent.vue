<script setup lang="ts">
import { useRouter } from 'vue-router';
import { computed } from 'vue';
import { formatScore, metricKey, metricScore } from 'src/utils/iaml';
import { IModel } from '../models';
import { displayServiceName } from 'src/utils/service-label';

const router = useRouter();

const propsComponent = defineProps<{
  model: IModel;
  projectId: number;
  processId: number;
}>();

const mainMetricName = computed(() => displayServiceName(propsComponent.model.metrics.metrics_info?.[metricKey(propsComponent.model.metrics)]?.name ?? metricKey(propsComponent.model.metrics)));
const mainMetricValue = computed(() => metricScore(propsComponent.model.metrics));
const mainMetricDirection = computed(() => {
  const metrics = propsComponent.model.metrics;
  const greater = metrics.objective?.greater_is_better ?? metrics.metrics_info?.[metricKey(metrics)]?.greater_is_better;
  return greater === undefined ? '' : greater ? 'Maximize' : 'Minimize';
});

function forkPipeline() {
  if (!propsComponent.processId) return;
  router.push({ name: 'pipeline-fork', params: { id: propsComponent.projectId, processId: propsComponent.processId } });
}

</script>

<template>
  <div
    v-if="model"
  >
    <div
      class="model-inline"
    >
      <div
        class="model-inline-name"
      >
        <q-icon name="smart_toy" />
        {{ displayServiceName(model.metrics.model_info?.name) }}
      </div>

      <div
        class="model-inline-score"
      >
        <q-icon name="rocket_launch">
          <q-tooltip>{{ mainMetricDirection }}</q-tooltip>
        </q-icon>
        {{ mainMetricName }} : <span class="metric-value">{{ formatScore(mainMetricValue) }}</span>
      </div>

      <div
        class="model-inline-actions"
      >
        <q-btn
          icon="fork_right"
          color="red-4"
          title="Fork pipeline"
          @click="forkPipeline"
        >
          <q-tooltip>Fork</q-tooltip>
        </q-btn>
        <q-btn
          icon="visibility"
          color="primary"
          :to="{ name: 'model-show', params: { id: projectId, processId, modelId: model.id } }"
        >
          <q-tooltip>See</q-tooltip>
        </q-btn>
      </div>
    </div>
  </div>
  <q-item v-else>
    <q-item-label class="text-caption text-italic">
      No DrIA models currently available
    </q-item-label>
  </q-item>
</template>

<style scoped>
.model-inline {
  display: grid;
  grid-template-columns: minmax(0, 5fr) minmax(0, 4fr) auto;
  align-items: center;
  gap: 8px;
  padding-left: 8.3333%;
  min-width: 0;
}

.model-inline-name,
.model-inline-score {
  min-width: 0;
  overflow-wrap: anywhere;
}

.metric-value {
  font-variant-numeric: tabular-nums;
}

.model-inline-actions {
  display: flex;
  flex-wrap: wrap;
  justify-content: flex-end;
  gap: 16px;
  min-width: 0;
  max-width: 100%;
}

@media (max-width: 599px) {
  .model-inline {
    grid-template-columns: minmax(0, 1fr);
    padding-left: 0;
  }
}
</style>
