<script setup lang="ts">
import { ref } from 'vue';
import { formatToDDMMYYYHHmm } from 'src/utils/date';
import { IProcess } from '../models';

const propsComponent = defineProps<{
  process?: IProcess
}>();

const mainMetricName = ref<string | undefined>(
  propsComponent.process?.models[0]?.metrics.metrics_info[propsComponent.process?.models[0]?.metrics.main_metrics].name
);
const mainMetricValue = ref<number | undefined>(
  propsComponent.process?.models[0]?.metrics.metrics_test ? propsComponent.process?.models[0]?.metrics.metrics_test[propsComponent.process?.models[0]?.metrics.main_metrics] : undefined
);
</script>

<template>
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
      Best {{  mainMetricName }} : {{ mainMetricValue }}
    </div>
    <div class="col-2">
      <q-chip
        v-if="process"
        square
        :color="{'FINISHED': 'green', 'CANCELED': 'red', 'CRASHED': 'red', 'QUEUED': 'blue', 'RUNNING': 'orange', 'TIMEOUT': 'grey'}[process.status]"
        text-color="white"
        :icon="{'FINISHED': 'check', 'CANCELED': 'close', 'CRASHED': 'error', 'QUEUED': 'hourglass_top', 'RUNNING': 'rocket_launch', 'TIMEOUT': 'hourglass_disabled'}[process.status]"
        :label="process?.status"
      />
    </div>
  </q-item>

  <q-item v-else>
    <div class="text-caption text-italic">
      Aucun process actuellement disponible
    </div>
  </q-item>
</template>
