<script setup lang="ts">
import { formatToDDMMYYYHHmm } from 'src/utils/date';
import { IModel } from '../models';

// props
const propsComponent = defineProps<{
  model: IModel;
  routeParams: {
    projectId: number;
    processId: number;
    modelId: number;
  }
}>();
</script>

<template>
  <div class="flex-center column">
    <div class="flex row items-start justify-center full-width">
      <div class="flex column col-6 q-pr-xl q-mr-xl">
        <h4 class="no-margin q-pb-lg text-weight-medium">
          {{ propsComponent.model.metrics.model_info.name }}
        </h4>
        <q-card class="q-pa-md">
          {{ propsComponent.model.metrics.model_info.description }}
        </q-card>
      </div>

      <div class="flex column q-pl-xl q-ml-xl col-2">
        <div class="flex column q-gutter-y-md">
          <q-item
            clickable
            class="bg-secondary text-white shadow-3 q-pa-md"
            style="width: 300px"
          >
            <q-item-section>
              <div class="flex row items-center justify-between">
                <div class="text-weight-bolder text-body1">
                  Date :
                </div>
                <div class="text-weight-bolder text-body1">
                  {{ formatToDDMMYYYHHmm(propsComponent.model.date) }}
                </div>
              </div>
            </q-item-section>
          </q-item>

          <q-item
            clickable
            class="bg-secondary text-white shadow-3 q-pa-md"
            style="width: 300px"
          >
            <q-item-section>
              <div class="flex row items-center justify-between">
                <div class="text-weight-bolder text-body1">
                  {{ propsComponent.model.metrics.metrics_info[propsComponent.model.metrics.main_metrics].name }}
                </div>
                <div class="text-weight-bolder text-body1">
                  {{
                    propsComponent.model.metrics.metrics_test ? 
                    propsComponent.model.metrics.metrics_test[propsComponent.model.metrics.main_metrics] : "?"
                  }}
                </div>
              </div>
            </q-item-section>
          </q-item>
        </div>

        <q-btn
          color="blue-8"
          label="Tester le modèle"
          class="q-mt-md"
          :to="{ name: 'model-test', params: { 
            id: routeParams.projectId, processId: routeParams.processId, modelId: routeParams.modelId } }"
        />

        <q-btn
          @click="console.log('mes couilles')"
          color="yellow-9"
          label="Créer une application"
          class="q-mt-md"
        />
      </div>
    </div>
  </div>
</template>

<style scoped>
.model-info-card {
  word-break: break-word;
  line-height: 1.4em;
  padding: 16px 20px;
}
.model-info-card-left {
  color: #000000;
  background: #dfdfdf;
  border: 2px solid #000000;
}
.model-info-card-right {
  color: #000000;
  background: #ffffff;
  border-top: 2px solid #000000;
  border-right: 2px solid #000000;
  border-bottom: 2px solid #000000;
}
</style>
