<script setup lang="ts">
import { QTableColumn, useQuasar } from 'quasar';
import { computed, onMounted, onUnmounted, ref } from 'vue';
import AdminPageShell from 'src/components/administration/AdminPageShell.vue';
import { IProject, IUser } from 'src/components/models';
import { deleteAdminProject, getAdminProjects, getAdminUsers } from 'src/api/administration.api';
import { useUserStore } from 'src/stores/user-store';

const $q = useQuasar();
const userStore = useUserStore();
const pageSize = 25;
const projects = ref<IProject[]>([]);
const users = ref<IUser[]>([]);
const page = ref(1);
const hasNextPage = ref(false);
const loading = ref(true);
const loadError = ref('');
const search = ref<string | null>('');
const deleteDialog = ref(false);
const selectedProject = ref<IProject>();
const deleting = ref(false);
const deleteError = ref('');
let loadController: AbortController | undefined;
const usersController = new AbortController();
let disposed = false;

const columns: QTableColumn[] = [
  { name: 'project', label: 'Projet', field: 'title', align: 'left' },
  { name: 'owner', label: 'Propriétaire', field: 'userId', align: 'left' },
  { name: 'target', label: 'Résultat à prédire', field: 'csvColumnToPredict', align: 'left' },
  { name: 'actions', label: '', field: 'id', align: 'right' },
];
const usersById = computed(() => new Map(users.value.map((user) => [user.id, user])));
const owner = (project: IProject) => usersById.value.get(project.userId);
function ownerName(project: IProject): string {
  const user = owner(project);
  return user ? [user.firstName, user.lastName].filter(Boolean).join(' ').trim() || user.email
    : `Compte nº ${project.userId}`;
}
function ownerInitials(project: IProject): string {
  const user = owner(project);
  return user ? [user.firstName?.[0], user.lastName?.[0]].filter(Boolean).join('').toUpperCase()
    || user.email?.[0]?.toUpperCase() || '?' : '?';
}
const normalize = (value: string) => value.normalize('NFD').replace(/[\u0300-\u036f]/g, '').toLocaleLowerCase('fr');
const hasSearch = computed(() => Boolean(search.value?.trim()));
const filteredProjects = computed(() => {
  const query = normalize(search.value?.trim() ?? '');
  return query ? projects.value.filter((project) => normalize([
    project.title, project.description, project.csvColumnToPredict,
    ownerName(project), owner(project)?.email ?? '', String(project.id),
  ].join(' ')).includes(query)) : projects.value;
});
const countLabel = computed(() => hasSearch.value
  ? `${filteredProjects.value.length} résultat${filteredProjects.value.length > 1 ? 's' : ''} sur cette page`
  : `${projects.value.length} projet${projects.value.length > 1 ? 's' : ''} sur cette page`);

async function loadPage(targetPage = page.value): Promise<boolean> {
  loadController?.abort();
  const controller = new AbortController();
  loadController = controller;
  loading.value = true;
  loadError.value = '';
  try {
    // One extra result indicates another page; the API does not return a total.
    const result = await getAdminProjects(pageSize + 1, (targetPage - 1) * pageSize, controller.signal);
    if (disposed || controller.signal.aborted) return false;
    projects.value = result.slice(0, pageSize);
    hasNextPage.value = result.length > pageSize;
    page.value = targetPage;
    return true;
  } catch {
    if (!disposed && !controller.signal.aborted) {
      loadError.value = 'Les projets n’ont pas pu être chargés. Réessayez dans quelques instants.';
    }
    return false;
  } finally {
    if (!disposed && loadController === controller) loading.value = false;
  }
}
async function loadOwners() {
  try {
    const result = await getAdminUsers(usersController.signal);
    if (!disposed) users.value = result;
  } catch {
    // Projects remain usable when account labels cannot be retrieved.
  }
}
function askToDelete(project: IProject) {
  selectedProject.value = project;
  deleteError.value = '';
  deleteDialog.value = true;
}
async function confirmDelete() {
  const project = selectedProject.value;
  if (!project || deleting.value) return;
  deleting.value = true;
  deleteError.value = '';
  try {
    await deleteAdminProject(project.id);
    if (disposed) return;
    projects.value = projects.value.filter((item) => item.id !== project.id);
    deleteDialog.value = false;
    selectedProject.value = undefined;
    $q.notify({ type: 'positive', message: 'Projet supprimé.' });
    // Refill the current page after deletion to preserve the offset navigation.
    if (await loadPage() && projects.value.length === 0 && page.value > 1) {
      await loadPage(page.value - 1);
    }
  } catch {
    if (!disposed) deleteError.value = 'La suppression a échoué. Le projet a été conservé. Vous pouvez réessayer.';
  } finally {
    if (!disposed) deleting.value = false;
  }
}

onMounted(() => { void loadPage(); void loadOwners(); });
onUnmounted(() => { disposed = true; loadController?.abort(); usersController.abort(); });
</script>

<template>
  <AdminPageShell
    title="Projets"
    description="Retrouvez les projets de la plateforme et les comptes qui les portent."
  >
    <template #actions>
      <q-btn
        outline no-caps color="primary" icon="refresh" label="Actualiser"
        :disable="loading || deleting" @click="loadPage()"
      />
    </template>

    <section class="admin-panel" aria-label="Gestion des projets">
      <div class="admin-panel-header">
        <div>
          <h2>Projets de la plateforme</h2>
          <p>Consultez les informations essentielles et gérez les projets existants.</p>
        </div>
        <span v-if="!loading && !loadError" class="admin-badge admin-badge--neutral">{{ countLabel }}</span>
      </div>

      <div class="admin-toolbar projects-toolbar">
        <q-input
          v-model="search" outlined dense clearable class="admin-search"
          label="Rechercher dans cette page" debounce="150" :disable="loading"
        >
          <template #prepend><q-icon name="search" /></template>
        </q-input>
        <p class="projects-search-help">La recherche porte sur les projets de la page affichée.</p>
      </div>

      <div v-if="loadError" class="admin-feedback admin-feedback--error" role="alert">
        <q-icon name="error_outline" size="22px" />
        <span>{{ loadError }}</span>
        <q-btn flat no-caps color="primary" label="Réessayer" :disable="loading" @click="loadPage()" />
      </div>

      <q-table
        v-if="!loadError || projects.length"
        class="admin-table projects-table" flat :rows="filteredProjects" :columns="columns"
        row-key="id" :loading="loading" :pagination="{ rowsPerPage: 0 }" hide-pagination
        :grid="$q.screen.lt.sm"
        no-data-label="Aucun projet" :aria-busy="loading"
      >
        <template #item="{ row }">
          <article class="admin-mobile-card">
            <div class="admin-mobile-heading">
              <div>
                <div class="admin-cell-title">{{ row.title || 'Projet sans titre' }}</div>
                <p v-if="row.description" class="projects-description">{{ row.description }}</p>
                <div class="admin-cell-meta">Projet nº {{ row.id }}</div>
              </div>
            </div>
            <dl class="admin-mobile-details">
              <div>
                <dt>Propriétaire</dt>
                <dd>
                  <div class="projects-owner">
                    <q-avatar size="34px" class="projects-owner-avatar" aria-hidden="true">{{ ownerInitials(row) }}</q-avatar>
                    <div class="projects-owner-info">
                      <div class="admin-cell-title">{{ ownerName(row) }}</div>
                      <div v-if="owner(row)?.email && ownerName(row) !== owner(row)?.email" class="admin-cell-meta">{{ owner(row)?.email }}</div>
                    </div>
                  </div>
                </dd>
              </div>
              <div>
                <dt>Résultat à prédire</dt>
                <dd>
                  <span v-if="row.csvColumnToPredict" class="admin-badge admin-badge--neutral projects-target">{{ row.csvColumnToPredict }}</span>
                  <span v-else class="admin-cell-meta">Non renseigné</span>
                </dd>
              </div>
            </dl>
            <div class="admin-mobile-actions">
              <q-btn
                v-if="row.userId === userStore.currentUser?.id"
                flat no-caps color="primary" label="Ouvrir" icon-right="arrow_outward"
                :to="`/projets/${row.id}`" :disable="deleting"
              />
              <q-btn
                flat no-caps color="negative" icon="delete_outline" label="Supprimer"
                :aria-label="`Supprimer le projet ${row.title}`" :disable="loading || deleting"
                @click="askToDelete(row)"
              />
            </div>
          </article>
        </template>
        <template #body-cell-project="{ row }">
          <q-td class="projects-title-cell">
            <div class="admin-cell-title">{{ row.title || 'Projet sans titre' }}</div>
            <p v-if="row.description" class="projects-description">{{ row.description }}</p>
            <div class="admin-cell-meta">Projet nº {{ row.id }}</div>
          </q-td>
        </template>
        <template #body-cell-owner="{ row }">
          <q-td>
            <div class="projects-owner">
              <q-avatar size="34px" class="projects-owner-avatar" aria-hidden="true">{{ ownerInitials(row) }}</q-avatar>
              <div class="projects-owner-info">
                <div class="admin-cell-title">{{ ownerName(row) }}</div>
                <div v-if="owner(row)?.email && ownerName(row) !== owner(row)?.email" class="admin-cell-meta">{{ owner(row)?.email }}</div>
              </div>
            </div>
          </q-td>
        </template>
        <template #body-cell-target="{ row }">
          <q-td class="projects-target-cell">
            <span v-if="row.csvColumnToPredict" class="admin-badge admin-badge--neutral projects-target">{{ row.csvColumnToPredict }}</span>
            <span v-else class="admin-cell-meta">Non renseigné</span>
          </q-td>
        </template>
        <template #body-cell-actions="{ row }">
          <q-td>
            <div class="admin-row-actions">
              <q-btn
                v-if="row.userId === userStore.currentUser?.id"
                flat no-caps color="primary" label="Ouvrir" icon-right="arrow_outward"
                :to="`/projets/${row.id}`" :disable="deleting"
              />
              <q-btn
                flat round size="sm" icon="delete_outline" class="projects-delete-button"
                :aria-label="`Supprimer le projet ${row.title}`" :disable="loading || deleting"
                @click="askToDelete(row)"
              ><q-tooltip>Supprimer le projet</q-tooltip></q-btn>
            </div>
          </q-td>
        </template>
        <template #no-data>
          <div class="admin-empty full-width">
            <q-spinner v-if="loading" color="primary" size="32px" />
            <q-icon v-else :name="hasSearch ? 'search_off' : 'folder_open'" size="36px" />
            <h3>{{ loading ? 'Chargement des projets' : hasSearch ? 'Aucun résultat sur cette page' : 'Aucun projet à afficher' }}</h3>
            <p v-if="!loading">{{ hasSearch ? 'Essayez un autre terme ou consultez une autre page.' : 'Les projets créés sur la plateforme apparaîtront ici.' }}</p>
            <q-btn v-if="hasSearch && !loading" flat no-caps color="primary" label="Effacer la recherche" @click="search = ''" />
          </div>
        </template>
      </q-table>

      <footer class="projects-pagination" aria-label="Pagination des projets">
        <span class="admin-cell-meta">Page {{ page }} <span class="projects-page-size">· Jusqu’à {{ pageSize }} projets</span></span>
        <div class="projects-pagination-buttons">
          <q-btn
            flat no-caps icon="chevron_left" label="Précédente" color="primary"
            :disable="loading || deleting || page <= 1" @click="loadPage(page - 1)"
          />
          <q-btn
            flat no-caps icon-right="chevron_right" label="Suivante" color="primary"
            :disable="loading || deleting || !hasNextPage" @click="loadPage(page + 1)"
          />
        </div>
      </footer>
    </section>

    <q-dialog v-model="deleteDialog" :persistent="deleting">
      <q-card class="admin-dialog project-workspace-colors">
        <q-card-section class="admin-dialog-heading">
          <div>
            <div class="projects-delete-symbol"><q-icon name="delete_outline" size="25px" /></div>
            <h2>Supprimer ce projet ?</h2>
            <p>Le projet <strong>{{ selectedProject?.title || 'sans titre' }}</strong>, ses données et ses entraînements seront définitivement supprimés.</p>
          </div>
          <q-btn flat round dense icon="close" aria-label="Fermer la confirmation" :disable="deleting" @click="deleteDialog = false" />
        </q-card-section>
        <q-card-section v-if="deleteError" class="q-pt-none">
          <div class="admin-feedback admin-feedback--error" role="alert">{{ deleteError }}</div>
        </q-card-section>
        <q-card-actions class="admin-dialog-actions">
          <q-btn flat no-caps label="Conserver le projet" :disable="deleting" @click="deleteDialog = false" />
          <q-btn unelevated no-caps color="negative" label="Supprimer le projet" :loading="deleting" @click="confirmDelete" />
        </q-card-actions>
      </q-card>
    </q-dialog>
  </AdminPageShell>
</template>

<style scoped lang="scss">
.projects-toolbar { flex-wrap: wrap; gap: 12px 20px; }
.projects-search-help { margin: 0; color: #74818e; font-size: 12px; }
.projects-table :deep(td) { vertical-align: middle; padding-top: 20px; padding-bottom: 20px; }
.projects-table :deep(.admin-row-actions) { justify-content: flex-end; flex-wrap: nowrap; }
.projects-table :deep(td.projects-title-cell) { min-width: 240px; max-width: 440px; white-space: normal; }
.projects-title-cell .admin-cell-title { overflow-wrap: anywhere; }
.projects-description {
  display: -webkit-box;
  margin: 5px 0 7px;
  color: #6a7887;
  font-size: 12px;
  line-height: 1.6;
  overflow: hidden;
  overflow-wrap: anywhere;
  -webkit-line-clamp: 2;
  -webkit-box-orient: vertical;
}
.projects-owner { display: flex; align-items: center; gap: 10px; }
.projects-owner-avatar { flex-shrink: 0; background: #eff7f7; color: #287f83; font-size: 12px; font-weight: 650; }
.projects-owner-info { max-width: 240px; min-width: 140px; white-space: normal; overflow-wrap: anywhere; }
.admin-mobile-card .projects-owner-info { min-width: 0; max-width: none; }
.admin-mobile-card .admin-cell-title { overflow-wrap: anywhere; }
.projects-target-cell { max-width: 220px; }
.projects-target { max-width: 100%; white-space: normal; overflow-wrap: anywhere; }
.projects-delete-button { color: #758390; }
.projects-delete-button:hover { color: var(--q-negative); background: #fff3f2; }
.projects-pagination { display: flex; align-items: center; justify-content: space-between; gap: 12px; padding: 16px 24px; border-top: 1px solid #edf0f3; }
.projects-pagination-buttons { display: flex; gap: 4px; }
.projects-delete-symbol { display: grid; place-items: center; width: 46px; height: 46px; margin-bottom: 18px; border-radius: 14px; background: #fff3f2; color: var(--q-negative); }
@media (max-width: 600px) {
  .projects-search-help { width: 100%; }
  .projects-pagination { flex-wrap: wrap; padding: 14px 16px; }
  .projects-pagination-buttons { margin-left: auto; }
  .projects-page-size { display: none; }
}
</style>
