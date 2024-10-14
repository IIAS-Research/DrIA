<script setup lang="ts">
import {
  computed, onMounted, ref, watch, watchEffect, onUnmounted,
} from 'vue';
import { useRouter, useRoute } from 'vue-router';
import {
  IProject,
  ProcessStatus,
} from 'components/models';
import { useSignalR } from 'src/utils/useSignalR';
import TableShowDataSample from 'components/projects/TableShowDataSample.vue';
import { GetProjectWithNestedDataById } from 'src/api/projects.api';
import TrainResultsCards from 'src/components/projects/TrainResultsCards.vue';

const route = useRoute();
const router = useRouter();
const signalR = useSignalR('generateModel');
const { jobId } = route.query;

// eslint-disable-next-line no-undef
let interval: NodeJS.Timeout;

const projectId = computed(() => parseInt(route.params.id as string, 10));

const progress = ref(0);
const startProcessTime = ref<number>();
const approximativeRemainingTime = ref<number>();
const project = ref<IProject>();

async function getProjectById() {
  if (!projectId.value) return undefined;
  const responseProject = await GetProjectWithNestedDataById(projectId.value);
  if (responseProject) project.value = responseProject;
  return undefined;
}

function updateProgressVal() {
  if (!project.value) return;

  const process = project.value.processes.find((p) => p.status === 'RUNNING');
  if (!process) {
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

const isProcessing = computed(() => project.value?.processes.some((p) => p.status === 'RUNNING') ?? false);

function connectWebSocketsIfNeeded() {
  if (!isProcessing.value) return;
  signalR.connect(String(jobId));
  startProcessTime.value = new Date().getTime();
  if (interval) return;
  interval = setInterval(() => {
    updateProgressVal();
  }, 2000);
}

onMounted(async () => {
  await getProjectById();
  connectWebSocketsIfNeeded();
});
onUnmounted(() => {
  signalR.disconnect();
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
  const messageReceived = signalR.receivedMessageRef.value as ProcessStatus;
  if (messageReceived !== 'FINISHED') return;
  signalR.disconnect();
  router.replace({ name: 'project-show', params: { id: projectId.value } });
});

watch(route, async () => {
  await getProjectById();
}, { deep: true });
</script>

<template>
  <q-page padding>
    <q-dialog v-model="signalR.errorDialog.value">
      <q-card class="q-pa-md">
        <div>{{ signalR.receivedMessageRef.value }}</div>
      </q-card>
    </q-dialog>
    <template v-if="project">
      <q-btn
        icon="arrow_back"
        label="Back to home page"
        class="q-mb-xl"
        :to="{ name: 'home' }"
      />
      <div class="text-h5">
        {{ project.title }}
      </div>
      <div
        v-if="project.description"
        class="text-subtitle2 text-grey-8 q-mb-md"
      >
        {{ project.description }}
      </div>

      <div v-if="isProcessing">
        <div
          class="text-h6 text-primary"
        >
          Recherche du meilleur modèle en cours...
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
      </div>

      <TrainResultsCards
        v-if="project.processes?.length"
        :project-id="project.id"
        :processes="project.processes"
        :is-loading="isProcessing"
        class="q-my-md"
      />

      <TableShowDataSample />
    </template>
  </q-page>
</template>
