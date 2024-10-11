<script setup lang="ts">
import { computed, onMounted, ref } from 'vue';
import { QTableColumn } from 'quasar';
import { useRoute } from 'vue-router';
import { getProjectDataSampleAsync } from 'src/api/projects.api';
import { predictValueUsingAiModel } from 'src/api/models.api';
import { ICsvProject } from '../models';

const route = useRoute();
defineProps<{
    editableRows?: boolean;
}>();

const projectId = parseInt(route.params.id as string, 10);
const modelId = parseInt(route.params.modelId as string, 10);

const projectFileDataSample = ref<ICsvProject>();

async function getProjectDataSample() {
  if (!projectId) return undefined;
  const responseDataSample = await getProjectDataSampleAsync(projectId);
  return responseDataSample;
}

const cols = computed(() => projectFileDataSample.value?.columns.map((col) => ({
  ...col, field: col.name, required: true, align: 'left',
} as QTableColumn)));

const rows = computed(() => projectFileDataSample.value?.rows);

const editedDataTable = computed(() => {
  if (!rows.value || !cols.value || !projectFileDataSample.value) return null;
  return {
    columns: [...projectFileDataSample.value.columns],
    rows: rows.value,
  };
});

async function predict() {
  if (!editedDataTable.value) return;
  const responsePrediction = await predictValueUsingAiModel(modelId, editedDataTable.value);
  if (!responsePrediction) return;
  projectFileDataSample.value = responsePrediction;
}

onMounted(async () => {
  projectFileDataSample.value = await getProjectDataSample();
});
</script>

<template>
  <div>
    <pre> {{ editedDataTable }}</pre>
    <q-table
      title="Input data sample"
      virtual-scroll
      :pagination="{ rowsPerPage: 0 }"
      :rows="rows ?? []"
      :columns="cols"
      row-key="name"
      :rows-per-page-options="[0]"
      style="max-height: 1000px;"
      :loading="!projectFileDataSample"
      table-header-class="bg-grey-4 text-weight-bold"
    >
      <template
        v-if="editableRows"
        #top-right
      >
        <q-btn
          label="tester"
          color="primary"
          glossy
          @click="predict"
        />
      </template>
      <template
        v-if="editableRows"
        #body-cell="{ row, col }"
      >
        <q-td
          :props="{ row, col }"
        >
          <q-input
            v-model.number="row[ col.name ]"
            type="number"
            dense
            borderless
          />
        </q-td>
      </template>
    </q-table>
  </div>
</template>
