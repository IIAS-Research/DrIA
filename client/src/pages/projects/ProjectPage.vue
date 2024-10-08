<script setup lang="ts">
import {
  computed, onMounted, ref, watch, watchEffect,
} from 'vue';
import { useRouter, useRoute } from 'vue-router';
import {
  ICsvProject, IProject,
  ProcessStatus,
} from 'components/models';
import { useSignalR } from 'src/utils/useSignalR';
import ProcessCards from 'components/projects/ProcessCards.vue';
import TableShowDataSample from 'components/projects/TableShowDataSample.vue';
import { GetProjectWithNestedDataById, getProjectDataSampleAsync } from 'src/api/projects.api';

const route = useRoute();
const router = useRouter();

// eslint-disable-next-line no-undef
let interval: NodeJS.Timeout;

const projectId = computed(() => parseInt(route.params.id as string, 10));

const signalRRef = ref();
const progress = ref(0);
const startProcessTime = ref<number>();
const approximativeRemainingTime = ref<number>();
const project = ref<IProject>();

const projectFileDataSample = ref<ICsvProject>();

async function getProjectDataSample() {
  if (!projectId.value) return undefined;
  const responseDataSample = await getProjectDataSampleAsync(projectId.value);
  return responseDataSample;
}

async function getProjectById() {
  if (!projectId.value) return undefined;
  const responseProject = await GetProjectWithNestedDataById(projectId.value);
  console.log('response project => ', responseProject);
  if (responseProject) project.value = responseProject;
  return undefined;
}

function updateProgressVal() {
  if (!project.value) return;

  const process = project.value.processes.find((p) => p.status === 'RUNNING');
  if (!process) {
    console.log('No running process found, clearing interval.');
    clearInterval(interval);
    return;
  }

  const maxDuration = (process.maxDuration ?? 0) * 1000; // Convert from seconds to milliseconds

  if (!maxDuration || !startProcessTime.value) return;

  // Get current time and calculate elapsed time
  const currentTime = new Date().getTime();
  const elapsedTime = currentTime - startProcessTime.value;

  // Calculate progress (ensure elapsedTime and maxDuration are in milliseconds)
  progress.value = elapsedTime / maxDuration;

  // If progress exceeds 100%, cap it at 100%
  if (progress.value >= 1) {
    progress.value = 1;
    clearInterval(interval); // Stop updating when progress reaches 100%
  }

  // Calculate remaining time in milliseconds, ensure it's not negative
  const remainingTime = maxDuration - elapsedTime;
  approximativeRemainingTime.value = Math.max(0, Math.ceil(remainingTime / 1000)); // Convert to seconds and prevent negative values
}

function connectWebSocketsIfNeeded() {
  const { jobId } = route.query;
  if (!jobId) return;
  console.log('here connect web sockets');
  const signalR = useSignalR('status', String(jobId));
  signalRRef.value = signalR;
  signalR.connect();
  startProcessTime.value = new Date().getTime();
  if (interval) return;
  interval = setInterval(() => {
    console.log('Interval triggered');
    updateProgressVal();
  }, 2000);
}

onMounted(async () => {
  await getProjectById();
  projectFileDataSample.value = await getProjectDataSample();
  connectWebSocketsIfNeeded();
});

const remainingFormatedTime = computed(() => {
  if (!approximativeRemainingTime.value) return '';
  const totalMilliseconds = approximativeRemainingTime.value;

  const totalSeconds = Math.floor(totalMilliseconds / 1000);

  const hours = Math.floor(totalSeconds / 3600) >= 0 ? Math.floor(totalSeconds / 3600) : 0;
  const minutes = Math.floor((totalSeconds % 3600) / 60) >= 0 ? Math.floor((totalSeconds % 3600) / 60) : 0;

  let timeString = '';
  if (hours > 0) {
    timeString += `${hours} heure${hours !== 1 ? 's' : ''} `;
  }
  timeString += `${minutes} minute${minutes !== 1 ? 's' : ''} restants`;

  return timeString.trim();
});

watchEffect(async () => {
  console.log('it worked =>  ', signalRRef.value?.receivedMessageRef);
  const messageReceived = signalRRef.value?.receivedMessageRef as ProcessStatus;
  if (messageReceived !== 'FINISHED') return;
  console.log('job finished !');
  router.replace({ name: 'project-show', params: { id: projectId.value } });
});

watch(route, async () => {
  await getProjectById();
  console.log('icicicicic');
}, { deep: true });
</script>

<template>
  <q-page padding>
    <template v-if="project">
      <div class="text-h5">
        {{ project.title }}
      </div>
      <div
        v-if="project.description"
        class="text-subtitle2 text-grey-8"
      >
        {{ project.description }}
      </div>

      <div
        v-if="project.processes.some(p => p.status === 'RUNNING')"
        class="text-h6 text-primary q-mt-md"
      >
        Traitement des process en cours
      </div>
      <q-linear-progress
        :value="progress"
        rounded
        stripe
        :color="progress === 1 ? 'positive' : 'primary'"
        size="30px"
        class="q-mb-md"
      >
        <div class="absolute-full flex flex-center">
          <q-badge
            color="white"
            text-color="primary"
            :label="progress === 1 ? '100 % Terminé' :
              `${Math.floor(progress * 100)} % (Environ ${remainingFormatedTime})`"
          />
        </div>
      </q-linear-progress>

      <TableShowDataSample :project-file-data-sample="projectFileDataSample" />

      <ProcessCards
        v-if="project.processes?.length"
        :processes="project.processes"
        class="q-mt-md"
      />
    </template>
  </q-page>
</template>
