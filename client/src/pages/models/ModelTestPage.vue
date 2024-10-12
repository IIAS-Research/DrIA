<script setup lang="ts">
import { useRoute } from 'vue-router';
import TableShowDataSample from 'components/projects/TableShowDataSample.vue';

const route = useRoute();

const projectId = parseInt(route.params.id as string, 10);
const processId = parseInt(route.params.processId as string, 10);
const modelId = parseInt(route.params.modelId as string, 10);

</script>

<template>
  <q-page padding>
    <q-btn
      icon="arrow_back"
      label="Back to model"
      class="q-mb-lg"
      :to="{ name: 'model-show', params: { id: projectId, processId: processId, modelId: modelId } }"
    />
    <TableShowDataSample editable-rows />
  </q-page>
</template>
