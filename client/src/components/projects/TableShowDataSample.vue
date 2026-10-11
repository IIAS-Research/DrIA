<script setup lang="ts">
import { computed, onMounted, ref } from 'vue';
import { QTableColumn } from 'quasar';
import { useRoute } from 'vue-router';
import { getProjectDataSampleAsync } from 'src/api/projects.api';
import { predictValueUsingAiModel } from 'src/api/models.api';
import { api } from 'src/boot/axios';
import { notifyApiError } from 'src/utils/iaml';
import { IColumn, ICsvProject, IProject, IRow } from '../models';

const route = useRoute();
const props = defineProps<{ editableRows?: boolean }>();
const projectId = parseInt(route.params.id as string, 10);
const modelId = parseInt(route.params.modelId as string, 10);
const projectFileDataSample = ref<ICsvProject>();
const loading = ref(true);
const sampleError = ref('');
const targetColumnName = ref<string>();
const predicting = ref(false);
const numericColumns = ref<Set<string>>(new Set());
const booleanColumns = ref<Set<string>>(new Set());

const cols = computed(() => projectFileDataSample.value?.columns.map((col) => ({
  ...col, field: col.name, required: true, align: 'left',
} as IColumn & QTableColumn)));
const rows = computed(() => projectFileDataSample.value?.rows);
const inputColumns = computed(() => projectFileDataSample.value?.columns
  .filter((column) => !column.isColToPredict && !column.isGroupColumn && !column.isIdColumn) ?? []);
const targetColumn = computed(() => projectFileDataSample.value?.columns
  .find((column) => column.label === targetColumnName.value));

function toScalar(value: any, column: IColumn): any {
  if (value === '' || value === null || value === undefined) return null;
  if (booleanColumns.value.has(column.name) && typeof value === 'string' && /^(true|false)$/i.test(value))
    return value.toLowerCase() === 'true';
  if (numericColumns.value.has(column.name) && typeof value === 'string' && value.trim() && Number.isFinite(Number(value)))
    return Number(value);
  return value;
}

async function predict() {
  if (!projectFileDataSample.value || !targetColumn.value || predicting.value) return;
  predicting.value = true;
  try {
    const json_row = projectFileDataSample.value.rows.map((row) => Object.fromEntries(inputColumns.value
      .map((column) => [column.label, toScalar(row[column.name], column)])));
    const response = await predictValueUsingAiModel(modelId, { json_row, method: 'predict' });
    if (!Array.isArray(response.prediction) || response.prediction.length !== projectFileDataSample.value.rows.length)
      throw new Error('Le nombre de prédictions ne correspond pas aux observations.');
    const outputColumn = targetColumn.value.name;
    projectFileDataSample.value.rows = projectFileDataSample.value.rows.map((row, index) => ({
      ...row, [outputColumn]: response.prediction[index],
    }));
  } catch (error) {
    notifyApiError(error);
  } finally {
    predicting.value = false;
  }
}

function addRowToTable() {
  if (!projectFileDataSample.value) return;
  const rowToAdd = inputColumns.value.reduce((row, column) => {
    row[column.name] = 'Edit here';
    return row;
  }, {} as IRow);
  projectFileDataSample.value.rows = [...projectFileDataSample.value.rows, rowToAdd];
}

function rowKey(row: IRow): number {
  return projectFileDataSample.value?.rows.indexOf(row) ?? -1;
}

async function loadSample() {
  if (!projectId) return;
  loading.value = true;
  sampleError.value = '';
  try {
    const [sample, project] = await Promise.all([
      getProjectDataSampleAsync(projectId),
      props.editableRows ? api.get<IProject>(`Projects/${projectId}`).then((response) => response.data) : Promise.resolve(undefined),
    ]);
    projectFileDataSample.value = sample;
    if (!sample) {
      sampleError.value = 'L’aperçu des données n’a pas pu être chargé.';
      return;
    }
    targetColumnName.value = project?.csvColumnToPredict;
    const values = (column: IColumn) => sample?.rows.map((row) => row[column.name])
      .filter((value) => value !== null && value !== undefined && value !== '') ?? [];
    numericColumns.value = new Set((sample?.columns ?? []).filter((column) => values(column).length > 0
      && values(column).every((value) => typeof value === 'number' || (typeof value === 'string' && value.trim() && Number.isFinite(Number(value)))))
      .map((column) => column.name));
    booleanColumns.value = new Set((sample?.columns ?? []).filter((column) => values(column).length > 0
      && values(column).every((value) => typeof value === 'boolean' || (typeof value === 'string' && /^(true|false)$/i.test(value))))
      .map((column) => column.name));
  } catch (error) {
    sampleError.value = 'L’aperçu des données n’a pas pu être chargé.';
    notifyApiError(error);
  } finally {
    loading.value = false;
  }
}
onMounted(loadSample);
</script>

<template>
  <div class="project-sample">
    <div v-if="sampleError" class="sample-error" role="alert"><span>{{ sampleError }}</span><q-btn flat no-caps label="Réessayer" color="primary" :disable="loading" @click="loadSample" /></div>
    <q-table
      :title="editableRows ? 'Observations à essayer' : 'Échantillon des données'"
      virtual-scroll
      :pagination="{ rowsPerPage: 0 }"
      :rows="rows ?? []"
      :columns="cols"
      :row-key="rowKey"
      :rows-per-page-options="[0]"
      class="sample-table"
      :loading="loading"
      flat bordered
      no-data-label="Aucune observation disponible"
      table-header-class="sample-table-header"
    >
      <template #header-cell="scope">
        <q-th :props="scope"><span class="sample-column-name">{{ scope.col.label }}</span><small v-if="scope.col.isColToPredict" class="sample-role target-role">Résultat</small><small v-else-if="scope.col.isGroupColumn" class="sample-role">Regroupement</small><small v-else-if="scope.col.isIdColumn" class="sample-role">Exclue</small></q-th>
      </template>
      <template
        v-if="editableRows"
        #top-right
      >
        <q-btn
          label="Calculer les prédictions"
          color="primary"
          @click="predict"
          :loading="predicting"
          :disable="!targetColumn"
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
            :disable="col.isColToPredict || col.isGroupColumn || col.isIdColumn"
            borderless
          />
        </q-td>
      </template>

      <template
        v-if="editableRows"
        #bottom
      >
        <q-btn
          label="Ajouter une observation"
          color="primary"
          @click="addRowToTable"
        />
      </template>
    </q-table>
  </div>
</template>

<style scoped>
.project-sample { min-width: 0; }
.sample-table { max-width: 100%; max-height: 480px; border-color: var(--project-border, #dcded5); color: var(--project-ink, #203b3c); border-radius: 8px; }
.sample-table :deep(.q-table__top) { padding: 12px 16px; background: var(--project-paper, #f8f7f2); border-bottom: 1px solid var(--project-border, #dcded5); }
.sample-table :deep(.q-table__title) { font-size: 13px; font-weight: 500; }
.sample-table :deep(.q-table__middle) { overflow: auto; }
.sample-table :deep(th) { position: sticky; top: 0; z-index: 1; background: var(--project-paper, #f8f7f2); padding: 10px 16px; font-size: 12px; font-weight: 500; text-align: left; border-color: var(--project-border, #dcded5); }
.sample-table :deep(td) { font-size: 12px; padding: 10px 16px; border-color: var(--project-border, #dcded5); max-width: 300px; overflow: hidden; text-overflow: ellipsis; }
.sample-column-name { display: block; max-width: 240px; white-space: normal; overflow-wrap: anywhere; }
.sample-role { display: inline-block; padding: 2px 5px; margin-top: 4px; border: 1px solid var(--project-border, #dcded5); color: var(--project-muted, #626b68); font-size: 9px; border-radius: 3px; }
.target-role { color: var(--q-primary); background: var(--project-tint, #edf5f2); border-color: #c8dbd5; }
.sample-error { display: flex; align-items: center; flex-wrap: wrap; justify-content: space-between; gap: 10px; padding: 12px 0; color: #815247; font-size: 12px; }
@media (max-width: 599px) {
  .sample-table :deep(.q-table__top) { padding: 10px 12px; flex-wrap: wrap; gap: 10px; }
  .sample-table :deep(.q-table__top) .q-btn { font-size: 12px; }
}
</style>
