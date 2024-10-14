<script setup lang="ts">
import { getDefaultPipeline } from 'src/api/ia-web-service.api';
import { getProcessPipeline, runNewProcessWithNewPipeline } from 'src/api/processes.api';
import { IPipeline, IRunPipelineForkDto } from 'src/components/models';
import PipelineProject from 'src/components/pipeline/PipelineProject.vue';
import { onMounted, ref } from 'vue';
import { useRoute, useRouter } from 'vue-router';

const route = useRoute();
const router = useRouter();
const projectId = parseInt(route.params.id as string, 10);
const processId = parseInt(route.params.processId as string, 10);

const pipelineComponentRef = ref<InstanceType<typeof PipelineProject>>();
const pipeline = ref<IPipeline>();
const runNewProcessWithNewPipelineDto = ref<IRunPipelineForkDto>();

async function fetchProcessPipeline() {
  if (!projectId || !processId) {
    console.log('pas les infos donc on prend la default pipeline');
    pipeline.value = await getDefaultPipeline();
  } else {
    console.log('on peut fetch');
    pipeline.value = await getProcessPipeline(projectId, processId);
  }
}

async function runForkedPipeline() {
  if (!pipelineComponentRef.value || !pipelineComponentRef.value.pipelineRefLocalComponent) return;
  runNewProcessWithNewPipelineDto.value = {
    projectId,
    pipeline: { ...pipelineComponentRef.value.pipelineRefLocalComponent },
  };
  const runForkPipelineResponse = await runNewProcessWithNewPipeline(runNewProcessWithNewPipelineDto.value);
  if (runForkPipelineResponse) {
    router.push(
      { name: 'project-show', params: { id: projectId }, query: { jobId: runForkPipelineResponse.id } },
    );
  }
}

onMounted(async () => {
  await fetchProcessPipeline();
});
</script>

<template>
  <q-page padding>
    <div class="text-h6">
      Édition des paramètres de l'IA pour un nouvel entraînement
    </div>

    <PipelineProject
      ref="pipelineComponentRef"
      v-if="pipeline"
      :pipeline="pipeline"
    />

    <div class="flex justify-end q-mt-md">
      <q-btn
        label="Lancer l'entrainement"
        color="primary"
        :loading="!pipeline"
        @click="runForkedPipeline"
      />
    </div>
  </q-page>
</template>
