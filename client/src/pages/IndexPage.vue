<script setup lang="ts">
import ParticlesBackground from 'components/home/ParticlesBackground.vue';
import AppPresentation from 'components/home/AppPresentation.vue';
import TrainAIPrompt from 'components/home/TrainAIPrompt.vue';

</script>

<template>
  <q-page class="column flex-center">
    <img
      class="fixed-bottom-right"
      src="/images/iias_miniature.png"
      height="50px"
      style="right: 20px;"
    >
    <ParticlesBackground />
    <AppPresentation />

    <TrainAIPrompt />
  </q-page>
</template>
