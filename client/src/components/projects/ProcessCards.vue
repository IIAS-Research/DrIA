<script setup lang="ts">
import { computed, ref } from 'vue';
import { formatToDDMMYYYHHmm } from 'src/utils/date';
import { IProcess } from '../models';

const props = defineProps<{
  processes: IProcess[]
}>();

const lastRunningProcess = computed(() => props.processes
  .map((p) => p).sort((a, b) => b.date.getTime() - a.date.getTime())[0]);
</script>

<template>
  <div>
    <div class="row q-col-gutter-x-sm">
      <div class="col-4">
        <q-card>
          <q-card-section class="text-h6">
            Last processing
          </q-card-section>

          <q-card-section>
            le {{ formatToDDMMYYYHHmm(lastRunningProcess.date) }}
          </q-card-section>
        </q-card>
      </div>

      <div class="col-8">
        <q-card class="bg-teal-4">
          <q-card-section class="text-h6 text-white">
            Best processing
          </q-card-section>

          <q-card-section class="text-white">
            <!-- TODO: Retreive best processing and display it here -->
            le {{ formatToDDMMYYYHHmm(lastRunningProcess.date) }}
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
          separator
        >
          <q-item
            v-for="process in processes"
            :key="process.id"
            clickable
            v-ripple
            class="column"
          >
            <q-item-label>
              {{ formatToDDMMYYYHHmm(process.date) }}
            </q-item-label>
            <q-item-label>
              Duration: {{ process.Duration }}
            </q-item-label>
            <q-item-label>
              Statut: {{ process.status }}
            </q-item-label>
          </q-item>
        </q-list>
      </q-card-section>
    </q-card>
  </div>
</template>
