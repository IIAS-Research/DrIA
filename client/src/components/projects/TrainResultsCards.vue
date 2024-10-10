<script setup lang="ts">
import { computed } from 'vue';
import ModelCardContent from 'components/projects/ModelCardContent.vue';
import { useRouter } from 'vue-router';
import { IModel, IProcess } from '../models';
import ProcessCardContent from './ProcessCardContent.vue';

const router = useRouter();

const props = defineProps<{
  processes: IProcess[]
  projectId: number;
}>();

const lastRunningProcess = computed(() => {
  if (!props.processes) return null;
  return props.processes
    .map((p) => p).sort((a, b) => new Date(b.date).getTime() - new Date(a.date).getTime())[0];
});

const bestModel = computed(() => props.processes.map((p) => p.models[0])
  .sort((a, b) => {
    if (!a.metrics.metrics_test || !b.metrics.metrics_test) {
      return b.metrics.metrics_train[b.metrics.main_metrics] - a.metrics.metrics_train[a.metrics.main_metrics];
    }
    return b.metrics.metrics_test[b.metrics.main_metrics] - a.metrics.metrics_test[a.metrics.main_metrics];
  })[0]);

function getParentProcessOfModel(model: IModel) {
  return props.processes.filter((p) => p.models.find((m) => JSON.stringify(m) === JSON.stringify(model)))[0];
}

function forkPipeline() {
  console.log('Forking pipeline');
  const processId = getParentProcessOfModel(bestModel.value)?.id;
  if (!processId) return;
  router.push({ name: 'pipeline-fork', params: { projectId: props.projectId, processId } });
}
</script>

<template>
  <div>
    <div class="row q-col-gutter-x-sm">
      <div
        class="col-6"
        style="align-self: stretch;"
      >
        <q-card class="full-height">
          <q-card-section>
            <div class="text-h6">
              Last processing
            </div>

            <ProcessCardContent
              v-if="lastRunningProcess"
              :process="lastRunningProcess"
            />
          </q-card-section>
        </q-card>
      </div>

      <div
        class="col-6"
        style="align-self: stretch;"
      >
        <q-card class="bg-teal-4 full-height">
          <q-card-section>
            <div class="text-h6 text-white">
              Best Model (de tous les processes confondus)
            </div>

            <ModelCardContent
              :model="bestModel"
              display-best-model-name
              class="text-white"
            />
          </q-card-section>

          <q-card-actions
            align="right"
          >
            <q-btn
              label="Fork pipeline"
              color="primary"
              @click="forkPipeline"
            />
          </q-card-actions>
        </q-card>
      </div>
    </div>

    <q-card
      v-if="processes?.length"
      class="q-mt-md"
    >
      <q-card-section class="text-h6">
        All processing
      </q-card-section>

      <q-list
        bordered
        separator
      >
        <q-expansion-item
          v-for="process in processes"
          :key="process.id"
          expand-separator
          header-class="bg-blue-2"
        >
          <template #header>
            <ProcessCardContent :process="process" />
          </template>

          <template
            v-for="(model, index) in process.models"
            :key="model.id"
          >
            <q-card-section :class="index === 0 ? 'bg-green-2' : 'bg-blue-1'">
              <div class="q-pl-md text-subtitle2 text-italic">
                Model n° {{ index + 1 }} {{ index === 0 ? '(meilleur model de ce process)' : '' }}
              </div>
              <ModelCardContent :model="model" />
            </q-card-section>
            <q-separator />
          </template>
        </q-expansion-item>
      </q-list>
    </q-card>
  </div>
</template>
