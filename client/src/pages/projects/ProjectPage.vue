<script setup lang="ts">
import { computed, onMounted, ref } from 'vue';
import {
  ICsvProject, IPipeline, IProject,
} from 'components/models';
import { useSignalR } from 'src/utils/useSignalR';
import { useRoute } from 'vue-router';
import ProcessCards from 'components/projects/ProcessCards.vue';
import TableShowDataSample from 'components/projects/TableShowDataSample.vue';
import { GetProjectWithNestedDataById, getProjectDataSampleAsync } from 'src/api/projects.api';

const route = useRoute();

// eslint-disable-next-line no-undef
let interval: NodeJS.Timeout;

const projectId = ref(parseInt(route.params.id as string, 10));
const progress = ref(0);
const startProcessTime = ref<number>();
const approximativeRemainingTime = ref<number>();
const project = ref<IProject>({
  id: 51,
  title: 'fedferv',
  description: 'vdfvdfdfv',
  csvColumnToPredict: 'Price',
  rowDepedencies: false,
  rowDepedenciesLinked: [],
  datasetHasIdColumn: false,
  datasetHasIdColumnList: [],
  createdAt: '',
  updatedAt: '',
  userId: 3,
  processes: [
    {
      id: 32,
      date: new Date(new Date().getTime() - Math.random() * 10000000000), // Random date within the last 3 months
      duration: Math.floor(Math.random() * (60 * 60 * 1000)), // Random duration up to 1 hour in milliseconds
      pipeline: {} as IPipeline, // Assign a random pipeline
      status: 'RUNNING',
      projectId: 51,
      bestTestAccuracy: null,
      bestTrainAccuracy: 898,
      iaRemoteId: 'uifhiufhzeiufhiz',
      maxDuration: 1000 * 60 * 5,
      models: [
        {
          iaRemoteId: 'remote-456',
          id: 1,
          metrics: {
            metricsInfos: {
              balancedAxccuracy: {
                name: 'Noname',
                description: 'fhzeuicbnizebnicvnzebzeu',
              },
              classification_error: {
                name: 'Noname',
                description: 'fhzeuicbnizebnicvnzebzeu',
              },
              f1Score: {
                name: 'Noname',
                description: 'fhzeuicbnizebnicvnzebzeu',
              },
              precision: {
                name: 'Noname',
                description: 'fhzeuicbnizebnicvnzebzeu',
              },
              specificationMulticlass: {
                name: 'Noname',
                description: 'fhzeuicbnizebnicvnzebzeu',
              },
            },
            metricsTrain: {
              balancedAccuracy: 0.85,
              classification_error: 0.15,
              f1Score: 0.82,
              precision: 0.84,
              recall: 0.80,
              specificationMulticlass: 0.78,
            },
            metricsTest: null,
            mainMetric: 'balancedAccuracy',
          },
        }],
    },
  ],
});

const projectFileDataSample = ref<ICsvProject>();

async function getProjectDataSample() {
  if (!projectId.value) return undefined;
  const responseDataSample = await getProjectDataSampleAsync(projectId.value);
  return responseDataSample;
}

async function getProjectById() {
  if (!projectId.value) return undefined;
  const responseProject = await GetProjectWithNestedDataById(projectId.value);
  return responseProject;
}

function updateProgressVal() {
  const maxDuration = project.value.processes.find((p) => p.status === 'RUNNING')?.maxDuration;

  if (!maxDuration) return;
  const currentTime = new Date().getTime();
  const elapsedTime = currentTime - startProcessTime.value!;

  progress.value = (elapsedTime) / maxDuration;
  if (progress.value >= 1) {
    progress.value = 1;
    clearInterval(interval);
  }
  approximativeRemainingTime.value = Math.ceil(maxDuration - elapsedTime);
}

function connectWebSocketsIfNeeded() {
  const { jobId } = route.query;
  if (!jobId) return;
  const signalR = useSignalR('status', String(jobId));
  signalR.connect();
  startProcessTime.value = new Date().getTime();
  if (interval) return;
  interval = setInterval(() => {
    updateProgressVal();
  }, 2000);
}

onMounted(async () => {
  console.log(await getProjectById());
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
</script>

<template>
  <q-page padding>
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
      v-if="project.processes"
      :processes="project.processes"
      class="q-mt-md"
    />
  </q-page>
</template>
