<script setup lang="ts">
// particles
import { ref } from 'vue';
import type { Engine } from 'tsparticles-engine';
import { loadFull } from 'tsparticles';
import { particlesJSConfig } from 'src/utils/particles';

const particlesOptions = ref(particlesJSConfig);

// Function to initialize particles
async function particlesInit(engine: Engine) {
  await loadFull(engine);
}

</script>

<template>
  <Particles
    id="tsparticles"
    :options="particlesOptions"
    :particles-init="particlesInit"
  />
</template>
