<script setup lang="ts">
import ModelHeader from 'src/components/models/ModelHeader.vue';
import ModelPerformance from 'src/components/models/ModelPerformance.vue';
import ModelExplanations from 'src/components/models/ModelExplanations.vue';
</script>

<template>
  <q-page padding>
    <ModelHeader />
    <ModelPerformance />
    <ModelExplanations />
  </q-page>
</template>
