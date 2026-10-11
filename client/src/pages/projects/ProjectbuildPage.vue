<script setup lang="ts">
import AppPresentation from 'components/home/AppPresentation.vue';
import TrainAIPrompt from 'components/home/TrainAIPrompt.vue';
import { useQuasar } from 'quasar';
import { api } from 'src/boot/axios';
import { ICreateProjectResponseDTO, IProjectForm } from 'src/components/models';
import { ref } from 'vue';
import { useRouter } from 'vue-router';
import { apiError } from 'src/utils/iaml';

const $q = useQuasar();
const router = useRouter();

const isLoading = ref(false);
const datasetReady = ref(false);

async function submitProject(projectForm: IProjectForm, file: File) {
  isLoading.value = true;
  const formData = new FormData();
  formData.append('title', projectForm.title);
  formData.append('description', projectForm.description);
  formData.append('csvColumnToPredict', projectForm.csvColumnToPredict);
  formData.append('rowDepedencies', String(projectForm.rowDepedencies));
  formData.append('rowDepedenciesLinkedString', JSON.stringify(projectForm.rowDepedenciesLinked));
  formData.append('datasetHasIdColumn', String(projectForm.datasetHasIdColumn));
  formData.append('datasetHasIdColumnListString', JSON.stringify(projectForm.datasetHasIdColumnList));
  formData.append('pipeline', JSON.stringify(projectForm.pipeline));
  formData.append('survivalModel', String(projectForm.survivalModel));
  formData.append('file', file);
  formData.append('evaluationMode', projectForm.evaluationMode ?? 'split');
  if (projectForm.evaluationMode === 'external' && projectForm.testFile)
    formData.append('testFile', projectForm.testFile);
  if (projectForm.creationContextJson)
    formData.append('creationContextJson', projectForm.creationContextJson);
  if (projectForm.survivalModeldatasetTargetTime) {
    formData.append('survivalModeldatasetTargetTime', projectForm.survivalModeldatasetTargetTime);
  }
  if (projectForm.survivalModel && projectForm.survivalModelEventValue !== undefined) {
    formData.append('survivalModelEventValue', projectForm.survivalModelEventValue);
  }
  try {
    const responsePostProject: ICreateProjectResponseDTO = (await api.post('Projects', formData))?.data;
    if (responsePostProject) {
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
      message: apiError(error),
    });
  } finally {
    isLoading.value = false;
  }
}
</script>

<template>
  <q-page
    padding
    class="column project-creation-page"
    :class="{ 'has-dataset': datasetReady }"
  >
    <div class="flex column flex-center presentation">
      <AppPresentation />
    </div>

    <div class="creation-content">
      <TrainAIPrompt :submitting="isLoading" @dataset-ready="datasetReady = $event" @submit-project="submitProject" />
    </div>

    <img
      class="partner-mark"
      :class="{ 'fixed-bottom-right': !datasetReady }"
      src="/images/iias_miniature.png"
      alt="IIAS"
      height="50px"
    >
  </q-page>
</template>

<style scoped>
.project-creation-page {
  justify-content: center;
  gap: 30px;
  padding: 40px 24px;
  min-height: calc(100svh - 72px) !important;
  min-width: 0;
  /* Compensate for the shared layout's footer space on this entry screen. */
  margin-bottom: -200px;
}
.presentation {
  max-width: 100%;
  flex: 0 0 auto;
  align-self: center;
}
.presentation :deep(.main_title) {
  font-size: 48px !important;
  margin-bottom: 0;
}
.presentation :deep(img) {
  height: 46px;
}
.creation-content {
  width: 100%;
  min-width: 0;
}
.partner-mark {
  right: 20px;
  bottom: 16px;
}
.has-dataset {
  justify-content: flex-start;
  gap: 24px;
  padding: 24px;
  min-height: auto !important;
  margin-bottom: 0;
}
.has-dataset .presentation :deep(.main_title) {
  font-size: 32px !important;
}
.has-dataset .presentation :deep(img) {
  height: 32px;
}
.has-dataset .partner-mark {
  align-self: flex-end;
  height: 36px;
  margin-top: 8px;
}
@media (max-width: 599px) {
  .project-creation-page {
    gap: 24px;
    padding: 28px 16px 72px;
  }
  .presentation {
    overflow: hidden;
  }
  .presentation :deep(.main_title) {
    font-size: 40px !important;
  }
  .presentation :deep(img) {
    height: 38px;
  }
  .has-dataset {
    gap: 20px;
    padding: 20px 16px;
  }
}
</style>
