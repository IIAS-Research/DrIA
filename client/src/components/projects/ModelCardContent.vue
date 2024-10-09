<script setup lang="ts">
import { formatToDDMMYYYHHmm } from 'src/utils/date';
import { IModel } from '../models';

defineProps<{
  model?: IModel
}>();
</script>

<template>
  <q-item
    v-if="model"
    class="column"
    :to="{ name: 'model-show', params: { id: model.id } }"
  >
    <q-item-label>
      <b>Date :</b> {{ formatToDDMMYYYHHmm(model.date) }}
    </q-item-label>

    <q-item-label>
      <b>Nom du model :</b> {{ model.metrics.model }}
    </q-item-label>
    <q-item-label>
      <b>Valeur du model :</b> {{ model.metrics.main_metrics }} = {{ model.metrics.metrics_test ? model.metrics.metrics_test[model.metrics.main_metrics] : model.metrics.metrics_train[model.metrics.main_metrics] }}
    </q-item-label>
  </q-item>

  <q-item v-else>
    <q-item-label class="text-caption text-italic">
      Aucun models actuellement disponible
    </q-item-label>
  </q-item>
</template>
