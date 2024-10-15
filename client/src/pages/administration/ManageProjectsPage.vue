<script setup lang="ts">
import { QTable, QTableColumn, useQuasar } from "quasar";
import { IProject } from "src/components/models";
import { onMounted, ref } from "vue";
import { deleteProject, getProjects } from "src/api/projects.api";

const $q = useQuasar();
const columns: QTableColumn[] = [
  {
    name: "id",
    label: "ID",
    field: "id",
    align: "left",
  },
  {
    name: "title",
    label: "Titre",
    field: "title",
    align: "left",
  },
  {
    name: "userId",
    label: "UserId",
    field: "userId",
    align: "left",
  },
  {
    name: "actions",
    label: "Actions",
    field: "",
    align: "left",
  },
];

// refs
const projects = ref<IProject[]>([]);
const tableProjectRef = ref<InstanceType<typeof QTable>>();
const pagination = ref({
  sortBy: "desc",
  descending: false,
  page: 1,
  rowsPerPage: 30,
  rowsNumber: 0,
});

// functions
async function removeProject(projectId: IProject["id"]) {
  await deleteProject(projectId);
  projects.value = projects.value.filter(
    (projectFilter) => projectFilter.id !== projectId
  );
  $q.notify({
    type: 'positive',
    message: 'Projet supprimé'
  })
}
async function getResultsQTable(propsArg: any) {
  const { page, rowsPerPage } = propsArg.pagination;
  const fetchCount =
    rowsPerPage === 0 ? pagination.value.rowsNumber : rowsPerPage;
  const startRow = (page - 1) * rowsPerPage;
  try {
    const returnedData = await getProjects(fetchCount, startRow);
    pagination.value.page = page;
    pagination.value.rowsPerPage = rowsPerPage;
    projects.value = returnedData;
  } catch (error) {
    console.error(error);
    $q.notify({
      type: "negative",
      message: "Une erreur est survenue",
    });
  }
}

// lifecycle
onMounted(async () => {
  tableProjectRef.value?.requestServerInteraction();
});
</script>

<template>
  <q-page padding>
    <q-table
      v-if="projects"
      title="Projects"
      :rows="projects"
      :columns="columns"
      ref="tableProjectRef"
      row-key="id"
      flat
      bordered
      v-model:pagination="pagination"
      @request="getResultsQTable"
    >
      <template v-slot:body="props">
        <q-tr>
          <q-td key="id">
            {{ props.row.id }}
          </q-td>
          <q-td key="title">
            {{ props.row.title }}
          </q-td>
          <q-td key="userId">
            {{ props.row.userId }}
          </q-td>
          <q-td key="actions">
            <div class="flex row items-center">
              <q-icon
                class="cursor-pointer"
                name="delete"
                size="sm"
                color="negative"
                @click="removeProject(props.row.id)"
              >
                <q-tooltip>Supprimer</q-tooltip>
              </q-icon>
            </div>
          </q-td>
        </q-tr>
      </template>
    </q-table>
  </q-page>
</template>
