<script setup lang="ts">
import { ref, onMounted } from 'vue';
import { IModel } from 'src/components/models';
import { getModeleById } from 'src/api/models.api';
import { useRoute } from 'vue-router';
// components
import ModelHeader from 'src/components/models/ModelHeader.vue';
import ModelPerformance from 'src/components/models/ModelPerformance.vue';
import ModelExplanations from 'src/components/models/ModelExplanations.vue';
import ModelExplains from 'src/components/models/ModelExplains.vue';

// consts
const route = useRoute();
const modelId = route.params?.modelId.toString() ?? null;
const routeParams = {
  projectId: parseInt(route.params.id?.toString(), 10),
  processId: parseInt(route.params.processId?.toString(), 10),
  modelId: parseInt(modelId, 10),
};

// refs
const model = ref<IModel>();
const isLoading = ref(false);

// functions
async function getModelById(id: number) {
  isLoading.value = true;
  model.value = await getModeleById(id);
  isLoading.value = false;
}

// lifeCycle
onMounted(async () => {
  if (modelId == null) {
    return;
  }
  await getModelById(Number(modelId));
});
</script>

<template>
  <q-page class="q-pa-xl q-pt-md">
    <div
      v-if="model !== undefined"
      class="flex justify-center column full-width q-gutter-y-xl"
    >
      <ModelHeader
        :model="model"
        :route-params="routeParams"
      />
      <ModelPerformance :model="model" />
      <ModelExplanations :model="model" />
      <ModelExplains :model="model" />
    </div>

    <div
      v-show="isLoading"
      class="flex flex-center"
    >
      <q-spinner
        size="md"
        color="primary"
      />
    </div>
  </q-page>
</template>
