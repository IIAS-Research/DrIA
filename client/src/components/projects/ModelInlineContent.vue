<script setup lang="ts">
import { useRouter } from 'vue-router';
import { ref } from 'vue';
import { IModel } from '../models';

const router = useRouter();

const propsComponent = defineProps<{
  model: IModel;
  projectId: number;
  processId: number;
}>();

const mainMetricName = ref<string | undefined>(
  propsComponent.model.metrics.metrics_info[propsComponent.model.metrics.main_metrics].name
);
const mainMetricValue = ref<number | undefined>(
  propsComponent.model.metrics.metrics_test ? propsComponent.model.metrics.metrics_test[propsComponent.model.metrics.main_metrics] : undefined
);

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
          icon="fork_right"
          color="red-4"
          class="q-mr-md"
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
      Aucun models Dr IA actuellement disponible
    </q-item-label>
  </q-item>
</template>
