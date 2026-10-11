<script setup lang="ts">
import 'src/css/project-creation.scss';
import 'src/css/administration.scss';

defineProps<{ title: string; description: string }>();
const sections = [
  { name: 'admin-dashboard', label: 'Vue d’ensemble', icon: 'space_dashboard' },
  { name: 'admin-users', label: 'Utilisateurs', icon: 'group' },
  { name: 'admin-projects', label: 'Projets', icon: 'folder_open' },
  { name: 'admin-pulling', label: 'Suivi des calculs', icon: 'sync' },
];
</script>

<template>
  <q-page class="admin-page project-workspace-colors">
    <div class="admin-workspace">
      <q-btn flat dense no-caps icon="arrow_back" label="Retour à l’accueil" :to="{ name: 'home' }" class="admin-back-link" />
      <header class="admin-page-heading">
        <div class="admin-page-heading-copy">
          <p class="admin-eyebrow">Administration DrIA</p>
          <h1>{{ title }}</h1>
          <p class="admin-description">{{ description }}</p>
        </div>
        <div v-if="$slots.actions" class="admin-heading-actions"><slot name="actions" /></div>
      </header>
      <nav class="admin-navigation" aria-label="Rubriques de l’administration">
        <q-tabs align="left" no-caps active-color="primary" indicator-color="primary" outside-arrows mobile-arrows>
          <q-route-tab v-for="section in sections" :key="section.name" :to="{ name: section.name }" :label="section.label" :icon="section.icon" exact />
        </q-tabs>
      </nav>
      <main class="admin-content"><slot /></main>
    </div>
  </q-page>
</template>
