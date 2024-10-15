<script setup lang="ts">
import {
  onMounted, ref, watchEffect,
} from 'vue';
import { getUserProjects } from 'src/api/projects.api';
import { IProject } from 'src/components/models';

// props
const props = defineProps<{
  isUserConnected: boolean;
  isUserAdmin?: boolean;
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
          v-if="isUserAdmin"
          color="primary"
          icon="settings"
          :to="{ name: 'admin-users'}"
        >
          <q-tooltip> Gestion des utilisateurs </q-tooltip>
        </q-btn>

        <q-btn
          v-if="isUserAdmin"
          color="primary"
          icon="sync"
          :to="{ name: 'admin-pulling'}"
        >
          <q-tooltip> Pulling Debug </q-tooltip>
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

      <div class="text-weight-medium text-body1 q-pt-md q-pb-sm q-pl-md">
        Mes projets
      </div>
      <q-list
        separator
        bordered
        style="max-width: 100%;"
      >
        <q-item
          v-for="project in userProjects"
          :key="project.id"
          clickable
          v-ripple
          class="bg-grey-3"
          :to="{ name: 'project-show', params: { id: project.id } }"
        >
          <q-item-section>
            <q-item-label class="ellipsis">
              {{ project.title }}
            </q-item-label>
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
