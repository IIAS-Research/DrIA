<script setup lang="ts">
import { formatToDDMMYYYHHmm } from 'src/utils/date';
import { IModel } from '../models';

// props
const propsComponent = defineProps<{
  model: IModel;
}>();
</script>

<template>
  <div class="flex column">
    <div class="flex row items-center full-width">
      <div class="flex column col-6 q-pr-xl">
        <h4 class="no-margin q-py-lg text-weight-medium">
          {{ propsComponent.model.metrics.model }}
        </h4>
        <div>
          Lorem Ipsum is simply dummy text of the printing and typesetting
          industry. Lorem Ipsum has been the industry's standard dummy text ever
          since the 1500s, when an unknown printer took a galley of type and
          scrambled it to make a type specimen book. It has survived not only
          five centuries, but also the leap into electronic typesetting,
          remaining essentially unchanged.
        </div>
      </div>

      <div class="flex column q-pl-xl">
        <div class="flex row items-center">
          <div
            class="model-info-card model-info-card-left column justify-center text-center"
            style="width: 110px;"
          >
            Date
          </div>
          <div
            class="model-info-card model-info-card-right column justify-center text-center"
            style="width: 200px;"
          >
            {{ formatToDDMMYYYHHmm(propsComponent.model.date) }}
          </div>
        </div>

        <div class="flex row items-center q-pt-md">
          <div
            class="model-info-card model-info-card-left column justify-center text-center"
            style="width: 110px;"
          >
            Précision
          </div>
          <div
            class="model-info-card model-info-card-right column justify-center text-center"
            style="width: 200px;"
          >
            0.87
          </div>
        </div>

        <q-btn
          @click="console.log('ma bite')"
          color="blue-8"
          label="Tester le modèle"
          class="q-mt-md"
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
