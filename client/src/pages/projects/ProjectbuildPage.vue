<script setup lang="ts">
import AppPresentation from 'components/home/AppPresentation.vue';
import TrainAIPrompt from 'components/home/TrainAIPrompt.vue';
import { useQuasar } from 'quasar';
import { api } from 'src/boot/axios';
import { ICreateProjectResponseDTO, IProjectForm } from 'src/components/models';
import { ref } from 'vue';
import { useRouter } from 'vue-router';

const $q = useQuasar();
const router = useRouter();

const isLoading = ref(false);

async function submitProject(projectForm: IProjectForm, file: File) {
  isLoading.value = true;
  const formData = new FormData();
  formData.append('title', projectForm.title);
  formData.append('description', projectForm.description);
  formData.append('csvColumnToPredict', projectForm.csvColumnToPredict);
  formData.append('rowDepedencies', String(projectForm.rowDepedencies));
  formData.append('rowDepedenciesLinkedString', projectForm.rowDepedenciesLinked.toString());
  formData.append('datasetHasIdColumn', String(projectForm.datasetHasIdColumn));
  formData.append('datasetHasIdColumnListString', projectForm.datasetHasIdColumnList.toString());
  formData.append('pipeline', JSON.stringify(projectForm.pipeline));
  formData.append('file', file);
  try {
    const responsePostProject: ICreateProjectResponseDTO = (await api.post('Projects', formData, {
      headers: {
        'Content-Type': 'multipart/form-data',
      },
    }))?.data;
    if (responsePostProject) {
      console.log(responsePostProject);
      router.push({
        name: 'project-show',
        params: { id: responsePostProject.projectId },
        query: { jobId: responsePostProject.id },
      });
    }
  } catch (error) {
    console.error(error);
    $q.notify({
      type: 'negative',
      message: 'Une erreur est survenue',
    });
  } finally {
    isLoading.value = false;
  }
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
