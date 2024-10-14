<script setup lang="ts">
import { ref } from 'vue';
import { formatToDDMMYYYHHmm } from 'src/utils/date';
import { restartProcessAsync } from 'src/api/processes.api';
import { useRouter } from 'vue-router';
import { IProcess } from '../models';

const router = useRouter();

const propsComponent = defineProps<{
  process?: IProcess
  isLoading: boolean;
}>();

const mainMetricName = ref<string | undefined>(
  propsComponent.process?.models[0]?.metrics.metrics_info[propsComponent.process?.models[0]?.metrics.main_metrics].name,
);
const mainMetricValue = ref<number | undefined>(
  propsComponent.process?.models[0]?.metrics.metrics_test
    ? propsComponent.process?.models[0]?.metrics.metrics_test[propsComponent.process?.models[0]?.metrics.main_metrics]
    : undefined,
);
const dialog = ref(false);
const maxDuration = ref(45);

async function restartProcess() {
  if (!propsComponent.process) return;
  console.log('propsComponent.process.id => ', propsComponent.process.id);
  const response = await restartProcessAsync(propsComponent.process.id, maxDuration.value);
  if (!response) return;
  dialog.value = false;
  router.push(
    { name: 'project-show', params: { id: response.projectId }, query: { jobId: response.id } },
  );
}
</script>

<template>
  <q-dialog
    v-model="dialog"
  >
    <q-card style="min-width: 450px;">
      <q-card-section>
        Paramètre de la relance du process
      </q-card-section>

      <q-card-section>
        <q-badge color="secondary">
          maxDuration: {{ maxDuration }} sec
        </q-badge>
        <q-slider
          v-model="maxDuration"
          :min="45"
          :max="3600"
          :inner-min="10"
          :inner-max="4000"
          label
          :label-value="maxDuration + 'sec'"
          class="q-px-md"
        />
      </q-card-section>

      <q-card-actions align="right">
        <q-btn
          color="primary"
          label="valider"
          @click="restartProcess()"
        >
          <q-tooltip>Lancer l'entrainement</q-tooltip>
        </q-btn>
      </q-card-actions>
    </q-card>
  </q-dialog>

  <q-item
    v-if="process"
    class="row full-width"
  >
    <div class="col-2">
      <q-icon name="date_range" />
      {{ formatToDDMMYYYHHmm(process?.date) }}
    </div>
    <div class="col-2">
      <q-icon name="timer" />
      {{ process?.maxDuration }}s
    </div>
    <div class="col-5">
      <q-icon name="rocket_launch" />
      Best {{ mainMetricName }} : {{ mainMetricValue }}
    </div>
    <div class="col-2">
      <q-chip
        v-if="process"
        square
        :color="{
          'FINISHED': 'green',
          'CANCELED': 'red',
          'CRASHED': 'red',
          'QUEUED': 'blue',
          'RUNNING': 'orange',
          'TIMEOUT': 'grey'
        }[process.status]"
        text-color="white"
        :icon="{
          'FINISHED': 'check',
          'CANCELED': 'close',
          'CRASHED': 'error',
          'QUEUED': 'hourglass_top',
          'RUNNING': 'rocket_launch',
          'TIMEOUT': 'hourglass_disabled'

        }[process.status]"
        :label="process?.status"
      />
    </div>

    <q-space />

    <q-btn
      @click="dialog = true"
      icon="refresh"
      color="primary"
      :loading="isLoading"
    >
      <q-tooltip>Relancer le process</q-tooltip>
    </q-btn>
  </q-item>

  <q-item v-else>
    <div class="text-caption text-italic">
      Aucun process actuellement disponible
    </div>
  </q-item>
</template>
