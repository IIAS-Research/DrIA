<script setup lang="ts">
import {
  onMounted, ref, watchEffect,
} from 'vue';
import { getUserProjects } from 'src/api/projects.api';
import { IProject } from 'src/components/models';
import { formatToDDMMYYYHHmm } from 'src/utils/date';

// props
const props = defineProps<{
  isUserConnected: boolean;
}>();

// emits
const emits = defineEmits<{
  click: [click: Event];
}>();

// refs
const userProjects = ref<IProject[]>([]);

// functions
async function getUserProject() {
  if (!props.isUserConnected) return;
  userProjects.value = await getUserProjects();
}

// lifeCycle
onMounted(async () => {
  await getUserProject();
});

watchEffect(async () => {
  await getUserProject();
});
</script>

<template>
  <div>
    <div class="column q-pa-sm full-width">
      <div class="flex justify-end q-gutter-x-sm">
        <q-btn
          color="primary"
          icon="home"
          to="/"
        >
          <q-tooltip> Page d'accueil </q-tooltip>
        </q-btn>

        <q-btn
          color="primary"
          icon="settings"
          to="/manage-users"
        >
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
          :to="{ name: 'project-show', params: { id: project.id } }"
        >
          <q-item-section>
            <q-item-label>{{ project.title }}</q-item-label>
            <!-- <q-item-label caption>
              {{
                formatToDDMMYYYHHmm(project.updatedAt)
              }}
            </q-item-label> -->
          </q-item-section>
        </q-item>
      </q-list>
    </div>
  </div>
</template>
