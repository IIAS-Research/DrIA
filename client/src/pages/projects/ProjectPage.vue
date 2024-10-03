<script setup lang="ts">
import { computed, onMounted, ref } from 'vue';
import {
  ICsvProject, IPipeline, IProcess, IProject,
} from 'components/models';
import { useSignalR } from 'src/utils/useSignalR';
import { useRoute } from 'vue-router';
import ProcessCards from 'components/projects/ProcessCards.vue';
import TableShowDataSample from 'components/projects/TableShowDataSample.vue';

const route = useRoute();

// eslint-disable-next-line no-undef
let interval: NodeJS.Timeout;

const progress = ref(0);
const startProcessTime = ref<number>();
const approximativeRemainingTime = ref<number>();
const project = ref<IProject & { maxDuration: number}>({
  id: 51,
  title: 'fedferv',
  description: 'vdfvdfdfv',
  csvColumnToPredict: 'Price',
  rowDepedencies: false,
  rowDepedenciesLinked: [
    '[]',
  ],
  datasetHasIdColumn: false,
  datasetHasIdColumnList: [
    '[]',
  ],
  createdAt: '',
  updatedAt: '',
  userId: 3,
  maxDuration: 1000 * 60 * 5, // TODO: Fetch real max time duration
});
const projectFileDataSample = ref<ICsvProject>({
  columns: [
    { name: 'name', label: 'Dessert (100g serving)' },
    { name: 'calories', label: 'Calories' },
    { name: 'fat', label: 'Fat (g)' },
    { name: 'carbs', label: 'Carbs (g)' },
    { name: 'protein', label: 'Protein (g)' },
    { name: 'sodium', label: 'Sodium (mg)' },
    { name: 'calcium', label: 'Calcium (%)' },
    { name: 'iron', label: 'Iron (%)' },
  ],
  rows: [
    {
      name: 'Frozen Yogurt',
      calories: 159,
      fat: 6.0,
      carbs: 24,
      protein: 4.0,
      sodium: 87,
      calcium: '14%',
      iron: '1%',
    },
    {
      name: 'Ice cream sandwich',
      calories: 237,
      fat: 9.0,
      carbs: 37,
      protein: 4.3,
      sodium: 129,
      calcium: '8%',
      iron: '1%',
    },
    {
      name: 'Eclair',
      calories: 262,
      fat: 16.0,
      carbs: 23,
      protein: 6.0,
      sodium: 337,
      calcium: '6%',
      iron: '7%',
    },
    {
      name: 'Cupcake',
      calories: 305,
      fat: 3.7,
      carbs: 67,
      protein: 4.3,
      sodium: 413,
      calcium: '3%',
      iron: '8%',
    },
    {
      name: 'Gingerbread',
      calories: 356,
      fat: 16.0,
      carbs: 49,
      protein: 3.9,
      sodium: 327,
      calcium: '7%',
      iron: '16%',
    },
    {
      name: 'Jelly bean',
      calories: 375,
      fat: 0.0,
      carbs: 94,
      protein: 0.0,
      sodium: 50,
      calcium: '0%',
      iron: '0%',
    },
    {
      name: 'Lollipop',
      calories: 392,
      fat: 0.2,
      carbs: 98,
      protein: 0,
      sodium: 38,
      calcium: '0%',
      iron: '2%',
    },
    {
      name: 'Honeycomb',
      calories: 408,
      fat: 3.2,
      carbs: 87,
      protein: 6.5,
      sodium: 562,
      calcium: '0%',
      iron: '45%',
    },
    {
      name: 'Donut',
      calories: 452,
      fat: 25.0,
      carbs: 51,
      protein: 4.9,
      sodium: 326,
      calcium: '2%',
      iron: '22%',
    },
    {
      name: 'KitKat',
      calories: 518,
      fat: 26.0,
      carbs: 65,
      protein: 7,
      sodium: 54,
      calcium: '12%',
      iron: '6%',
    }],
});
const projectProcesses = ref<IProcess[]>([
  {
    id: 32,
    date: new Date(new Date().getTime() - Math.random() * 10000000000), // Random date within the last 3 months
    Duration: Math.floor(Math.random() * (60 * 60 * 1000)), // Random duration up to 1 hour in milliseconds
    Pipeline: {} as IPipeline, // Assign a random pipeline
    status: 'RUNNING',
    ProjectId: 51,
  },
]);

function updateProgressVal() {
  const currentTime = new Date().getTime();
  const elapsedTime = currentTime - startProcessTime.value!;
  const { maxDuration } = project.value;

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

onMounted(() => {
  connectWebSocketsIfNeeded();
});

const remainingFormatedTime = computed(() => {
  if (!approximativeRemainingTime.value) return '';
  const totalMilliseconds = approximativeRemainingTime.value;

  const totalSeconds = Math.floor(totalMilliseconds / 1000);

  const hours = Math.floor(totalSeconds / 3600);
  const minutes = Math.floor((totalSeconds % 3600) / 60);

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
      v-if="projectProcesses.some(p => p.status === 'RUNNING')"
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
          :label="`${Math.floor(progress * 100)} % (Environ ${remainingFormatedTime})`"
        />
      </div>
    </q-linear-progress>

    <TableShowDataSample :project-file-data-sample="projectFileDataSample" />

    <ProcessCards
      v-if="projectProcesses"
      :processes="projectProcesses"
      class="q-mt-md"
    />
  </q-page>
</template>
