<script setup lang="ts">
import AppPresentation from 'components/home/AppPresentation.vue';
import TrainAIPrompt from 'components/home/TrainAIPrompt.vue';
import { useQuasar } from 'quasar';
import { api } from 'src/boot/axios';
import { IProjectForm } from 'src/components/models';
import { ref } from 'vue';

const $q = useQuasar();

const isLoading = ref(false);

async function submitProject(projectForm: IProjectForm, file: File) {
  isLoading.value = true;
  const formData = new FormData();
  Object.entries(projectForm).forEach(([key, value]) => {
    let formatedValue : string | Blob = String(value);
    if (Array.isArray(value) || typeof value === 'object') {
      formatedValue = JSON.stringify(value);
    }
    formData.append(key, formatedValue);
  });
  formData.append('file', file);
  try {
    const responsePostProject = await api.post('Projects', formData, {
      headers: {
        'Content-Type': 'multipart/form-data',
      },
    });
    // response give the id for the WS
  } catch (error) {
    console.error(error);
    $q.notify({
      type: 'negative',
      message: 'Une erreur est survenue',
    });
  } finally {
    isLoading.value = false;
  }

  // ws connect
}
</script>

<template>
  <q-page
    padding
    class="column q-pt-xl q-mt-xl"
  >
    <div class="flex column flex-center">
      <img
        class="fixed-bottom-right"
        src="/images/iias_miniature.png"
        height="50px"
        style="right: 20px;"
      >
      <AppPresentation />
    </div>

    <TrainAIPrompt @submit-project="submitProject" />
  </q-page>
</template>
