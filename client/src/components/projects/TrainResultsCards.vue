<script setup lang="ts">
import { computed } from 'vue';
import ModelCardContent from 'components/projects/ModelCardContent.vue';
import ModelInlineContent from 'components/projects/ModelInlineContent.vue';
import { useRouter } from 'vue-router';
import { IModel, IProcess } from '../models';
import ProcessCardContent from './ProcessCardContent.vue';

const router = useRouter();

const props = defineProps<{
  processes: IProcess[];
  projectId: number;
  isLoading: boolean;
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
  const processId = getParentProcessOfModel(bestModel.value)?.id;
  if (!processId) return;
  router.push({ name: 'pipeline-fork', params: { id: props.projectId, processId } });
}

function seeBest() {
  const processId = getParentProcessOfModel(bestModel.value)?.id;
  if (!processId) return;
  router.push({ name: 'model-show', params: { id: props.projectId, processId, modelId: bestModel.value.id } });
}
</script>

<template>
  <div>
    <div class="row q-col-gutter-x-sm">
      <div
        class="col-6"
        style="align-self: stretch;"
      >
        <q-card class="bg-teal-4 full-height">
          <q-card-section>
            <div class="text-h6 text-white">
              Best Dr IA Model
            </div>

            <ModelCardContent
              v-if="bestModel"
              :process="getParentProcessOfModel(bestModel)"
              :project-id="projectId"
              :model="bestModel"
              :baseline="false"
              display-best-model-name
              class="text-white"
            />
          </q-card-section>

          <q-card-actions
            align="right"
          >
            <q-btn
              :loading="isLoading"
              icon="fork_right"
              label="Fork pipeline"
              color="red-4"
              @click="forkPipeline"
            />
            <q-btn
              :loading="isLoading"
              icon="visibility"
              label="See model details"
              color="primary"
              @click="seeBest"
            />
          </q-card-actions>
        </q-card>
      </div>
      <div
        class="col-6"
        style="align-self: stretch;"
      >
        <q-card class="bg-grey-4 full-height">
          <q-card-section>
            <div class="text-h6 text-grey-8">
              Baseline model
            </div>

            <ModelCardContent
              v-if="bestModel"
              :baseline="true"
              :model="bestModel"
              :project-id="projectId"
              :process="(lastRunningProcess as IProcess)"
              display-best-model-name
              class="text-grey-8"
            />
          </q-card-section>
        </q-card>
      </div>
    </div>

    <q-card
      v-if="processes?.length"
      class="q-mt-md"
    >
      <q-card-section class="text-h6">
        All trainings
      </q-card-section>

      <q-list
        bordered
        separator
      >
        <q-expansion-item
          v-for="process in processes.slice().reverse()"
          :key="process.id"
          expand-separator
          header-class="bg-blue-2"
        >
          <template #header>
            <ProcessCardContent
              :process="process"
              :is-loading="isLoading"
            />
          </template>

          <template
            v-for="(model, index) in process.models"
            :key="model.id"
          >
            <q-card-section :class="index === 0 ? 'bg-green-2' : 'bg-blue-1'">
              <ModelInlineContent
                :process-id="process.id"
                :project-id="projectId"
                :model="model"
              />
            </q-card-section>
            <q-separator />
          </template>
        </q-expansion-item>
      </q-list>
    </q-card>
  </div>
</template>
