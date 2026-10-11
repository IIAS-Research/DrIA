<script setup lang="ts">
import { computed, onUnmounted, ref, watch } from 'vue';
import { useRoute } from 'vue-router';
import { getUserProjects } from 'src/api/projects.api';
import { IProject, IUser } from 'src/components/models';

const props = defineProps<{
  isUserConnected: boolean;
  isUserAdmin?: boolean;
  isOpen: boolean;
  user?: IUser;
}>();

const emits = defineEmits<{
  logout: [];
  close: [];
  navigate: [];
}>();

const route = useRoute();
const userProjects = ref<IProject[]>([]);
const loading = ref(false);
const error = ref('');
const search = ref<string | null>('');
let requestRevision = 0;

function searchable(value: string) {
  return value.normalize('NFD').replace(/[\u0300-\u036f]/g, '').toLocaleLowerCase('fr');
}

const filteredProjects = computed(() => {
  const query = searchable(search.value?.trim() ?? '');
  return query ? userProjects.value.filter((project) => searchable(project.title).includes(query)) : userProjects.value;
});
const userName = computed(() => [props.user?.firstName, props.user?.lastName].filter(Boolean).join(' ') || 'Mon compte');
const userInitials = computed(() => {
  const initials = [props.user?.firstName?.trim()[0], props.user?.lastName?.trim()[0]].filter(Boolean).join('');
  return (initials || props.user?.email?.[0] || 'U').toLocaleUpperCase('fr');
});
const isAdminRoute = computed(() => route.path.startsWith('/admin-dashboard'));

function isProjectActive(projectId: number) {
  const selectedId = Array.isArray(route.params.id) ? route.params.id[0] : route.params.id;
  return selectedId !== undefined && String(projectId) === selectedId;
}

const dateFormatter = new Intl.DateTimeFormat('fr-FR', {
  day: '2-digit', month: 'short', year: 'numeric',
});

function projectDate(project: IProject) {
  for (const entry of [
    { value: project.updatedAt, label: 'Mis à jour le' },
    { value: project.createdAt, label: 'Créé le' },
  ]) {
    if (!entry.value || !/^\d{4}-\d{2}-\d{2}(?:T\d{2}:\d{2}(?::\d{2}(?:\.\d+)?)?(?:Z|[+-]\d{2}:\d{2})?)?$/.test(entry.value)) continue;
    const parsed = new Date(entry.value);
    if (!Number.isNaN(parsed.getTime())) return `${entry.label} ${dateFormatter.format(parsed)}`;
  }
  return '';
}

async function loadProjects() {
  if (!props.isUserConnected || !props.isOpen) return;
  const revision = ++requestRevision;
  loading.value = true;
  error.value = '';
  try {
    const projects = await getUserProjects();
    if (revision !== requestRevision || !props.isUserConnected || !props.isOpen) return;
    userProjects.value = projects;
    if (projects.length <= 6) search.value = '';
  } catch {
    if (revision !== requestRevision || !props.isUserConnected || !props.isOpen) return;
    error.value = 'Les projets n’ont pas pu être chargés.';
  } finally {
    if (revision === requestRevision) loading.value = false;
  }
}

watch(() => [props.isUserConnected, props.isOpen], ([connected, opened]) => {
  requestRevision += 1;
  loading.value = false;
  if (!connected) {
    userProjects.value = [];
    error.value = '';
    search.value = '';
    return;
  }
  if (opened) void loadProjects();
}, { immediate: true });

onUnmounted(() => { requestRevision += 1; });
</script>

<template>
  <nav
    id="dria-navigation"
    class="dria-navigation dria-navigation-colors"
    aria-label="Navigation principale"
  >
    <header class="navigation-header">
      <router-link
        :to="{ name: 'home' }"
        class="navigation-brand"
        aria-label="DrIA, accueil"
        @click="emits('navigate')"
      >
        <img src="/images/baiddy_logo.svg" width="38" height="31" alt="">
        <span>DrIA</span>
      </router-link>
      <q-btn
        flat
        round
        dense
        icon="close"
        class="navigation-close"
        aria-label="Fermer le menu"
        @click="emits('close')"
      />
    </header>

    <q-list class="navigation-main-links">
      <q-item
        clickable
        v-ripple
        :to="{ name: 'home' }"
        :active="route.name === 'home'"
        active-class="navigation-link-active"
        class="navigation-link navigation-home"
        :aria-current="route.name === 'home' ? 'page' : undefined"
        @click="emits('navigate')"
      >
        <q-item-section avatar><q-icon name="home" size="21px" /></q-item-section>
        <q-item-section>
          <q-item-label>Accueil</q-item-label>
          <q-item-label caption>Créer une étude</q-item-label>
        </q-item-section>
      </q-item>
    </q-list>

    <section class="navigation-projects" aria-labelledby="navigation-projects-title">
      <div class="projects-heading">
        <h2 id="navigation-projects-title">Mes projets</h2>
        <span v-if="!loading && !error" class="projects-count">{{ userProjects.length }}</span>
      </div>
      <q-input
        v-if="userProjects.length > 6"
        v-model="search"
        outlined
        dense
        clearable
        :debounce="120"
        placeholder="Rechercher un projet"
        aria-label="Rechercher un projet"
        class="projects-search"
      >
        <template #prepend><q-icon name="search" size="18px" /></template>
      </q-input>

      <div class="projects-scroll">
        <div v-if="loading" class="projects-state" role="status">
          <q-spinner size="24px" class="projects-spinner" />
          <p>Chargement de vos projets…</p>
        </div>
        <div v-else-if="error" class="projects-state projects-error" role="alert">
          <q-icon name="cloud_off" size="25px" />
          <p>{{ error }}</p>
          <q-btn flat no-caps dense label="Réessayer" icon="refresh" @click="loadProjects" />
        </div>
        <div v-else-if="!userProjects.length" class="projects-state">
          <q-icon name="folder_open" size="28px" />
          <p>Aucun projet pour le moment.</p>
          <span>Vos études apparaîtront ici.</span>
          <q-btn
            flat
            no-caps
            dense
            label="Créer une étude"
            :to="{ name: 'home' }"
            @click="emits('navigate')"
          />
        </div>
        <div v-else-if="!filteredProjects.length" class="projects-state">
          <q-icon name="search" size="25px" />
          <p>Aucun projet ne correspond.</p>
          <q-btn flat no-caps dense label="Effacer la recherche" @click="search = ''" />
        </div>
        <q-list v-else class="projects-list">
          <q-item
            v-for="project in filteredProjects"
            :key="project.id"
            clickable
            v-ripple
            :to="{ name: 'project-show', params: { id: project.id } }"
            :active="isProjectActive(project.id)"
            active-class="navigation-link-active"
            class="navigation-link project-link"
            :aria-current="isProjectActive(project.id) ? 'page' : undefined"
            @click="emits('navigate')"
          >
            <q-item-section avatar><q-icon name="folder_open" size="18px" /></q-item-section>
            <q-item-section>
              <q-item-label :lines="2" class="project-title">{{ project.title }}</q-item-label>
              <q-item-label v-if="projectDate(project)" caption class="project-date">
                {{ projectDate(project) }}
              </q-item-label>
            </q-item-section>
            <q-tooltip class="dria-navigation-colors project-name-tooltip" max-width="280px">
              {{ project.title }}
            </q-tooltip>
          </q-item>
        </q-list>
      </div>
    </section>

    <footer class="navigation-footer">
      <q-item
        v-if="isUserAdmin"
        clickable
        v-ripple
        :to="{ name: 'admin-dashboard' }"
        :active="isAdminRoute"
        active-class="navigation-link-active"
        class="navigation-link navigation-admin"
        :aria-current="isAdminRoute ? 'location' : undefined"
        @click="emits('navigate')"
      >
        <q-item-section avatar><q-icon name="settings" size="19px" /></q-item-section>
        <q-item-section><q-item-label>Administration</q-item-label></q-item-section>
      </q-item>
      <div class="navigation-account">
        <q-avatar size="34px" class="account-avatar">{{ userInitials }}</q-avatar>
        <div class="account-details">
          <div class="account-name ellipsis" :title="userName">{{ userName }}</div>
          <div v-if="user?.email" class="account-email ellipsis" :title="user.email">{{ user.email }}</div>
        </div>
      </div>
      <q-btn
        flat
        no-caps
        align="left"
        icon="logout"
        label="Se déconnecter"
        class="navigation-logout"
        @click="emits('logout')"
      />
    </footer>
  </nav>
</template>

<style scoped lang="scss">
.dria-navigation {
  display: flex;
  flex-direction: column;
  height: 100%;
  min-height: 0;
  color: var(--project-ink);
  background: var(--project-paper);
  overflow: hidden;
}
.navigation-header { display: flex; align-items: center; justify-content: space-between; gap: 16px; padding: 24px 20px 21px; border-bottom: 1px solid var(--project-border); }
.navigation-brand { display: inline-flex; align-items: center; gap: 12px; text-decoration: none; color: var(--project-ink); }
.navigation-brand img { display: block; }
.navigation-brand span { font-size: 24px; font-weight: 600; letter-spacing: -.04em; }
.navigation-close { color: var(--project-muted); }
.navigation-main-links { padding: 15px 12px 12px; }
.navigation-link { position: relative; min-width: 0; border-radius: 5px; color: var(--project-ink); }
.navigation-link :deep(.q-item__section--avatar) { min-width: 32px; padding-right: 10px; color: var(--project-muted); }
.navigation-link :deep(.q-item__label) { font-size: 13px; line-height: 1.45 !important; }
.navigation-link :deep(.q-item__label--caption) { margin-top: 4px; color: var(--project-muted); font-size: 11px; }
.navigation-home { min-height: 60px; }
.navigation-link-active { background: var(--project-selection-surface); color: var(--project-selection-ink); }
.navigation-link-active::before { position: absolute; top: 12px; bottom: 12px; left: 0; width: 3px; border-radius: 0 2px 2px 0; background: var(--project-brand); content: ''; }
.navigation-link-active :deep(.q-item__section--avatar) { color: var(--project-selection-ink); }
.navigation-link-active .project-title { font-weight: 500; }
.navigation-brand:focus-visible { outline: 2px solid var(--project-ink); outline-offset: 5px; border-radius: 2px; }
.navigation-link:focus-visible { outline: 2px solid var(--project-ink); outline-offset: -2px; }
.navigation-projects { display: flex; flex: 1; flex-direction: column; min-height: 0; padding: 6px 12px 16px; }
.projects-heading { display: flex; align-items: center; justify-content: space-between; gap: 12px; padding: 12px 12px 14px; }
.projects-heading h2 { margin: 0; color: var(--project-muted); font-size: 11px; font-weight: 600; letter-spacing: .08em; line-height: 1.4; text-transform: uppercase; }
.projects-count { font-size: 11px; color: var(--project-muted); font-variant-numeric: tabular-nums; }
.projects-search { margin: 0 3px 12px; }
.projects-search :deep(.q-field__control) { height: 37px; min-height: 37px; background: var(--project-surface); border-radius: 5px; }
.projects-search :deep(.q-field__control::before) { border-color: var(--project-border); }
.projects-search :deep(.q-field__native) { color: var(--project-ink); font-size: 12px; }
.projects-search :deep(.q-field__marginal) { height: 37px; color: var(--project-muted); }
.projects-scroll { flex: 1; min-height: 0; overflow-x: hidden; overflow-y: auto; overscroll-behavior: contain; scrollbar-width: thin; scrollbar-color: var(--project-border) transparent; }
.projects-list { padding-right: 3px; }
.project-link { min-height: 57px; margin-bottom: 3px; padding: 12px; align-items: flex-start; }
.project-link :deep(.q-item__section--avatar) { padding-top: 2px; }
.project-title { overflow-wrap: anywhere; }
.project-date { font-variant-numeric: tabular-nums; }
.projects-state { display: flex; flex-direction: column; align-items: flex-start; gap: 10px; padding: 22px 12px; color: var(--project-muted); }
.projects-state p { margin: 0; font-size: 12px; line-height: 1.6; }
.projects-state > span { margin-top: -5px; font-size: 11px; line-height: 1.6; }
.projects-state :deep(.q-btn) { margin-left: -7px; color: var(--q-primary); font-size: 12px; }
.projects-spinner { color: var(--q-primary); }
.navigation-footer { flex-shrink: 0; padding: 10px 12px 16px; border-top: 1px solid var(--project-border); }
.navigation-admin { min-height: 43px; margin-bottom: 10px; }
.navigation-account { display: flex; align-items: center; gap: 10px; padding: 12px 11px 14px; }
.account-avatar { flex-shrink: 0; color: var(--project-ink); background: var(--project-brand); font-size: 12px; font-weight: 600; }
.account-details { min-width: 0; }
.account-name { color: var(--project-ink); font-size: 12px; font-weight: 500; line-height: 1.5; }
.account-email { margin-top: 2px; color: var(--project-muted); font-size: 11px; line-height: 1.5; }
.navigation-logout { width: 100%; min-height: 40px; padding-left: 12px; color: var(--project-muted); font-size: 12px; border-radius: 5px; }
.navigation-logout :deep(.q-icon) { font-size: 18px; }
.navigation-logout:hover, .navigation-logout:focus-visible { color: var(--q-negative); }
@media (max-height: 620px) {
  .navigation-header { padding-top: 16px; padding-bottom: 15px; }
  .navigation-main-links { padding-top: 10px; padding-bottom: 6px; }
  .navigation-footer { padding-top: 6px; padding-bottom: 9px; }
  .navigation-admin { margin-bottom: 3px; }
  .navigation-account { padding-top: 7px; padding-bottom: 9px; }
}
</style>
