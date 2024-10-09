<script setup lang="ts">
import { formatToDDMMYYYHHmm } from 'src/utils/date';
import { IProcess } from '../models';

defineProps<{
  process?: IProcess
}>();
</script>

<template>
  <q-item
    v-if="process"
    class="column full-width"
  >
    <q-item-label>
      <b>Duration :</b> {{ process?.maxDuration }}s
    </q-item-label>
    <q-item-label>
      <b>Date :</b> {{ formatToDDMMYYYHHmm(process?.date) }}
    </q-item-label>
    <q-item-label>
      <b>Statut :</b> {{ process?.status }}
    </q-item-label>

    <q-item-label>
      <b>bestTrainAccuracy :</b> {{ process?.bestTrainAccuracy }}
    </q-item-label>

    <q-item-label>
      <b>Nom du meilleur model :</b> {{ process?.models[0]?.metrics?.model }}
    </q-item-label>
    <q-item-label>
      <b>Valeur du meilleur model :</b> {{ process?.models[0]?.metrics?.main_metrics }} = {{ process?.models[0]?.metrics?.metrics_test ? process?.models[0]?.metrics?.metrics_test[process?.models[0]?.metrics?.main_metrics] : process?.models[0]?.metrics?.metrics_train[process?.models[0]?.metrics?.main_metrics] }}
    </q-item-label>
  </q-item>

  <q-item v-else>
    <q-item-label class="text-caption text-italic">
      Aucun process actuellement disponible
    </q-item-label>
  </q-item>
</template>
