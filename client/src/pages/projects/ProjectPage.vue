<script setup lang="ts">
import { ref } from 'vue';

const project = ref({
  $id: '2',
  id: 51,
  title: 'fedferv',
  description: 'vdfvdfdfv',
  csvColumnToPredict: 'Price',
  rowDepedencies: false,
  rowDepedenciesLinked: [
    '[]',
  ],
  datasetHasIdColumn: false,
  datasetHasIdColumnList: [
    '[]',
  ],
  createdAt: '',
  updatedAt: '',
  userId: 3,
  user: null,
  projectFile: null,
  processes: null,
});
</script>

<template>
  <div>Hello from project page</div>
</template>
