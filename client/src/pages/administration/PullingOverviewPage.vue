<script setup lang="ts">
import { onMounted, ref } from 'vue';
import { getPullingData, stopPullingConnection, IPullingData, IConnectionPullingData } from 'src/api/admin.api'
import { QTableProps, useQuasar } from 'quasar';
import { $ } from 'app/dist/spa/assets/index.4e58b93c';

// interfaces
interface IFormatedPullingData {
  jobWebServiceId: string;
  isPulling: boolean;
  status: string;
  type: string;
}

// consts
const $q = useQuasar();
const columnTable: QTableProps['columns'] = [
  {
    name: 'jobWebServiceId',
    label: 'JobWebServiceId',
    align: 'left',
    field: 'jobWebServiceId',
    sortable: true
  },
  { name: 'isPulling', align: 'left', label: 'IsPulling', field: 'isPulling', sortable: true },
  { name: 'status', align: 'left', label: 'Status', field: 'status', sortable: true },
  { name: 'type', align: 'left', label: 'Type', field: 'type', sortable: true },
  { name: 'actions', align: 'left', label: 'Actions', field: 'actions' },
]

// refs
const pullingDataFormated = ref<IFormatedPullingData[]>([]);

// functions
async function setupPullingData() {
  const data = await getPullingData() as IPullingData;
  pullingDataFormated.value = [];
  Object.entries(data).forEach((entry) => {
    entry[1].forEach((pullingEach: IConnectionPullingData) => {
      pullingDataFormated.value.push({
        jobWebServiceId: pullingEach.jobWebServiceId,
        isPulling: pullingEach.isPulling,
        status: pullingEach.status,
        type: entry[0]
      })
    })
  })
}
async function stopConnectionPulling(jobId: string) {
  await stopPullingConnection(jobId);
  $q.notify({
    type: 'positive',
    message: 'Pulling supprimé'
  })
}
function removeConnectionPulling(jobId: string) {
  stopConnectionPulling(jobId);
  pullingDataFormated.value = pullingDataFormated.value.filter((pullingData) => {
    pullingData.jobWebServiceId !== jobId;
  })
}

// lifecycle
onMounted(async () => {
  await setupPullingData();
});
</script>

<template>
  <q-page padding>
    <div v-if="pullingDataFormated" class="column">
      <q-table :rows="pullingDataFormated" :columns="columnTable"
        row-key="jobWebServiceId" :pagination="{
      rowsPerPage: 100
    }">

        <template v-slot:body="props">
          <q-tr>
            <q-td key="jobWebServiceId">
              {{ props.row.jobWebServiceId }}
            </q-td>
            <q-td key="isPulling">
              {{ props.row.isPulling ? 'Oui' : 'Non' }}
            </q-td>
            <q-td key="status">
              {{ props.row.status }}
            </q-td>
            <q-td key="type">
              {{ props.row.type }}
            </q-td>
            <q-td key="actions">
              <div class="flex row items-center">
                <q-icon class="cursor-pointer" name="delete" size="sm" color="negative"
                  @click="removeConnectionPulling(props.row.jobWebServiceId)">
                  <q-tooltip>Supprimer</q-tooltip>
                </q-icon>
              </div>
            </q-td>
          </q-tr>
        </template>
      </q-table>
    </div>
  </q-page>
</template>
