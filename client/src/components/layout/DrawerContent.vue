<script setup lang="ts">
import { onMounted, ref } from "vue";
import { getUserProjects } from "src/api/projects.api";
import { IProject } from "src/components/models";
import { dateFormat } from "src/utils/date";

// emits
const emits = defineEmits<{
  click: [click: Event];
}>();

// refs
const userProjects = ref<IProject[]>([]);

// functions
async function getUserProject() {
  userProjects.value = await getUserProjects();
}

// lifeCycle
onMounted(async () => {
  await getUserProject();
});
</script>

<template>
  <div>
    <div class="column q-pa-sm full-width">
      <div class="flex justify-end q-gutter-x-sm">
        <q-btn color="primary" icon="settings" to="/manage-users">
          <q-tooltip> Administration du site </q-tooltip>
        </q-btn>

        <q-btn
          color="negative"
          icon="logout"
          flat
          @click="(event) => emits('click', event)"
        >
          <q-tooltip> Se déconnecter </q-tooltip>
        </q-btn>
      </div>

      <q-list>
        <q-item
          v-for="project in userProjects"
          :key="project.id"
          clickable
          v-ripple
          :to="{ name: 'projectPage', params: { id: project.id } }"
        >
          <q-item-section>
            <q-item-label>{{ project.title }}</q-item-label>
            <q-item-label caption>{{
              dateFormat(project.updateAt)
            }}</q-item-label>
          </q-item-section>
        </q-item>
      </q-list>
    </div>
  </div>
</template>
