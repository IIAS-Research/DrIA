<script setup lang="ts">
import { computed, onMounted, ref } from 'vue';
import { QTableColumn } from 'quasar';
import { useRoute } from 'vue-router';
import { getProjectDataSampleAsync } from 'src/api/projects.api';
import { predictValueUsingAiModel } from 'src/api/models.api';
import { IColumn, ICsvProject, IRow } from '../models';

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
} as IColumn & QTableColumn)));

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

function addRowToTable() {
  if (!projectFileDataSample.value) return;
  const rowToAdd = cols.value?.map((col) => col).reduce((acc, col) => {
    if (col.isColToPredict) return acc;
    acc[col.name] = 0;
    return acc;
  }, {} as IRow);

  if (!rowToAdd) return;
  projectFileDataSample.value.rows = [...projectFileDataSample.value.rows, rowToAdd];
}

onMounted(async () => {
  projectFileDataSample.value = await getProjectDataSample();
});
</script>

<template>
  <div>
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
            v-model="row[ col.name ]"
            dense
            :disable="col.isColToPredict"
            borderless
          />
        </q-td>
      </template>

      <template
        v-if="editableRows"
        #bottom
      >
        <q-btn
          label="Ajouter un rang"
          color="primary"
          @click="addRowToTable"
        />
      </template>
    </q-table>
  </div>
</template>
