<script setup lang="ts">
import { ref, toRef } from "vue";
import { getModeleExplanationById } from "src/api/models.api";
import { IModel } from "../models";

// props
const propsComponent = defineProps<{
  model: IModel;
}>();

// refs
const isLoadingExplanation = ref(false);
const textExplanation = toRef(propsComponent.model.textExplanation);

// functions
async function getExplanation() {
  isLoadingExplanation.value = true;
  textExplanation.value =
    (await getModeleExplanationById(propsComponent.model.id)) ?? [];
  isLoadingExplanation.value = false;
}
</script>

<template>
  <div class="flex column">
    <div class="flex row items-start">
      <div class="flex column col-6 q-pr-xl q-mr-xl">
        <h4 class="no-margin q-py-lg text-weight-medium">
          Explication des traitements
        </h4>
        <div v-if="!propsComponent.model.textExplanation" class="flex">
          <q-btn
            @click="getExplanation"
            :loading="isLoadingExplanation"
            label="Voir l'explication"
            color="yellow-8"
          />
        </div>
        <div v-else class="flex column q-gutter-y-md">
          <div v-for="(text, index) in textExplanation" :key="index">
            <div v-html="text" />
          </div>
        </div>
      </div>

      <div class="flex column col-4 q-pl-xl">
        <h4 class="no-margin q-py-lg text-weight-medium">Bibliographie</h4>
        <div>
          {{ propsComponent.model.bibliography?.bibliography }}
        </div>
      </div>
    </div>
  </div>
</template>
