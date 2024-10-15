<script setup lang="ts">
import { formatToDDMMYYYHHmm } from 'src/utils/date';
import { IModel, IProcess } from '../models';

defineProps<{
  model: IModel;
  projectId: number;
  process: IProcess;
  baseline: boolean;
}>();
</script>

<template>
  <div
    v-if="model"
  >
    <div
      v-if="!baseline"
      class="row q-col-gutter-x-sm full-width"
    >
      <div
        class="col-5"
      >
        <q-icon
          name="smart_toy"
          class="q-mb-xs"
        />
        {{ model.metrics.model_info.name }}
      </div>

      <div
        class="col-5"
      >
        <q-icon name="date_range" />
        {{ formatToDDMMYYYHHmm(model.date) }}
      </div>

      <div
        class="col-2"
      >
        <q-icon name="timer" />
        {{ process.maxDuration }}s
      </div>
    </div>
    <div
      v-else
      class="row q-col-gutter-x-sm full-width"
    >
      <div
        class="col-12"
      >
        <q-icon
          name="help"
          class="q-mb-xs"
        />
        Results of a model without Dr IA optimizations
      </div>
    </div>
    <div
      class="q-mt-md"
    >
      <q-item
        v-for="item in Object.entries(
          model.metrics.metrics_test ?? model.metrics.metrics_train
        )"
        :key="item[0]"
        clickable
        class="row"
      >
        <q-item-section
          side
          v-if="!baseline"
        >
          <q-icon
            name="info"
            color="white"
          >
            <q-tooltip
              class="text-body2"
              anchor="center right"
              self="center left"
            >
              {{
                model.metrics.metrics_info[item[0]].desc
              }}
            </q-tooltip>
          </q-icon>
        </q-item-section>
        <q-item-section>
          <div
            class="flex row items-center justify-between"
            :class="{reverse: baseline}"
          >
            <div class="text-weight-medium">
              {{
                model.metrics.metrics_info[item[0]].name
              }}
            </div>
            <div class="text-weight-medium">
              {{ item[1] }}
            </div>
          </div>
        </q-item-section>
      </q-item>
    </div>
  </div>

  <q-item v-else>
    <q-item-label class="text-caption text-italic">
      Aucun models Dr IA actuellement disponible
    </q-item-label>
  </q-item>
</template>
