<script setup lang="ts">
import { useUserStore } from 'stores/user-store';
import ParticlesBackground from 'components/home/ParticlesBackground.vue';
import AppPresentation from 'components/home/AppPresentation.vue';
import TrainAIPrompt from 'components/home/TrainAIPrompt.vue';
import Login from 'components/auth/LogIn.vue';

const userStore = useUserStore();

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

    <TrainAIPrompt v-if="userStore.isUserConnected" />
    <Login
      v-else
      class="q-mt-md"
    />
  </q-page>
</template>
