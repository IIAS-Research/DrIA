<script setup lang="ts">
import { ref } from 'vue';
import { formatToDDMMYYYHHmm } from 'src/utils/date';
import { IModel, IProcess } from '../models';

const propsComponent = defineProps<{
  model: IModel;
  projectId: number;
  processId: IProcess;
}>();

const mainMetricName = ref<string | undefined>(
  propsComponent.model.metrics.metrics_info[propsComponent.model.metrics.main_metrics].name
);
const mainMetricValue = ref<number | undefined>(
  propsComponent.model.metrics.metrics_test ? propsComponent.model.metrics.metrics_test[propsComponent.model.metrics.main_metrics] : undefined
);

</script>

<template>
  <div
    v-if="model"
  >
    <div
      class="row q-col-gutter-x-sm full-width"
    >
      <div class="col-1" />
      <div
        class="col-5"
      >
        <q-icon name="smart_toy" />
        {{ model.metrics.model_info.name }}
      </div>

      <div
        class="col-4"
      >
        <q-icon name="rocket_launch" />
        {{  mainMetricName }} : {{ mainMetricValue }}
      </div>

      <div
        class="col-2"
      >
        <q-btn
          icon="visibility"
          color="primary"
          :to="{ name: 'model-show', params: { id: projectId, processId, modelId: model.id } }"
        />
      </div>
    </div>
  </div>
  <q-item v-else>
    <q-item-label class="text-caption text-italic">
      Aucun models Dr IA actuellement disponible
    </q-item-label>
  </q-item>
</template>
