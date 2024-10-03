<script setup lang="ts">
import { computed, ref } from 'vue';
import { formatToDDMMYYYHHmm } from 'src/utils/date';
import { IProcess } from '../models';

const props = defineProps<{
  processes: IProcess[]
}>();

const lastRunningProcess = computed(() => props.processes
  .map((p) => p).sort((a, b) => b.date.getTime() - a.date.getTime())[0]);

const bestModel = computed(() => props.processes.map((p) => p.models[0])
  .sort((a, b) => {
    if (!a.metrics.metricsTest || !b.metrics.metricsTest) {
      return b.metrics.metricsTrain[b.metrics.mainMetric] - a.metrics.metricsTrain[a.metrics.mainMetric];
    }
    return b.metrics.metricsTest[b.metrics.mainMetric] - a.metrics.metricsTest[a.metrics.mainMetric];
  })[0]);
</script>

<template>
  <div>
    <div class="row q-col-gutter-x-sm">
      <div
        class="col-4"
        style="align-self: stretch;"
      >
        <q-card class="full-height">
          <q-card-section class="text-h6">
            Last processing
          </q-card-section>

          <q-card-section>
            <q-list
              bordered
              separator
            >
              <q-item
                clickable
                v-ripple
                class="column"
              >
                <q-item-label>
                  Id: {{ lastRunningProcess.id }}
                </q-item-label>
                <q-item-label>
                  Duration: {{ lastRunningProcess.duration }}
                </q-item-label>
                <q-item-label>
                  Date: {{ formatToDDMMYYYHHmm(lastRunningProcess.date) }}
                </q-item-label>
                <q-item-label>
                  Statut: {{ lastRunningProcess.status }}
                </q-item-label>

                <q-item-label>
                  bestTrainAccuracy: {{ lastRunningProcess.bestTrainAccuracy }}
                </q-item-label>
              </q-item>
            </q-list>
          </q-card-section>
        </q-card>
      </div>

      <div
        class="col-8"
        style="align-self: stretch;"
      >
        <q-card class="bg-teal-4 full-height">
          <q-card-section class="text-h6 text-white">
            Best Model
          </q-card-section>

          <q-card-section class="text-white">
            <q-list
              bordered
              separator
            >
              <q-item
                clickable
                v-ripple
                class="column"
              >
                <q-item-label>
                  <pre>{{ bestModel }}</pre>
                </q-item-label>
              </q-item>
            </q-list>
          </q-card-section>
        </q-card>
      </div>
    </div>

    <q-card class="q-mt-md">
      <q-card-section class="text-h6">
        All processing
      </q-card-section>

      <q-card-section>
        <q-list
          bordered
        >
          <q-expansion-item
            v-for="process in processes"
            :key="process.id"
            expand-separator
          >
            <template #header>
              <div class="full-width">
                <div>
                  Id: {{ process.id }}
                </div>
                <div>
                  Duration: {{ process.duration }}
                </div>
                <div>
                  Date: {{ formatToDDMMYYYHHmm(process.date) }}
                </div>
                <div>
                  Statut: {{ process.status }}
                </div>
                <div>
                  bestTrainAccuracy: {{ process.bestTrainAccuracy }}
                </div>
              </div>
            </template>
            <q-card
              v-for="model in process.models"
              :key="model.id"
            >
              <q-card-section>
                <pre>{{ model }}</pre>
              </q-card-section>
            </q-card>
          </q-expansion-item>
        </q-list>
      </q-card-section>
    </q-card>
  </div>
</template>
