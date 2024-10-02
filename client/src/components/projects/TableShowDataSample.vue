<script setup lang="ts">
import { computed } from 'vue';
import { QTableColumn } from 'quasar';
import { ICsvProject } from '../models';

const props = defineProps<{
    projectFileDataSample: ICsvProject;
}>();

const cols = computed(() => props.projectFileDataSample.columns.map((col) => ({
  ...col, field: col.name, required: true, align: 'left',
} as QTableColumn)));

const rows = computed(() => props.projectFileDataSample.rows);
</script>

<template>
  <div>
    <q-table
      title="Input data sample"
      virtual-scroll
      :pagination="{ rowsPerPage: 0 }"
      :rows="rows"
      :columns="cols"
      row-key="name"
      :rows-per-page-options="[0]"
      style="max-height: 1000px;"
      table-header-class="bg-grey-4 text-weight-bold"
    />
  </div>
</template>
