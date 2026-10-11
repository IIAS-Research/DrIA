<script setup lang="ts">
import { QTableColumn, useQuasar } from 'quasar';
import { computed, onBeforeUnmount, onMounted, ref } from 'vue';
import { ICreateUserDTO, IUser } from 'components/models';
import AdminPageShell from 'components/administration/AdminPageShell.vue';
import ManageUserForm from 'components/users/ManageUserForm.vue';
import {
  createAdminUser, deleteAdminUser, getAdminUsers, updateAdminUser,
} from 'src/api/administration.api';
import { apiError } from 'src/utils/iaml';

type ManagedUser = (IUser | ICreateUserDTO) & { password?: string };
type RoleFilter = 'all' | 'admin' | 'user';

const $q = useQuasar();
const users = ref<IUser[]>([]);
const loading = ref(true);
const hasLoadedUsers = ref(false);
const loadError = ref('');
const feedback = ref('');
const search = ref<string | null>('');
const roleFilter = ref<RoleFilter>('all');
const roleOptions = [
  { label: 'Tous les rôles', value: 'all' },
  { label: 'Administrateurs', value: 'admin' },
  { label: 'Utilisateurs', value: 'user' },
];
const pagination = ref({ rowsPerPage: 15, sortBy: 'account', descending: false });
const columns: QTableColumn[] = [
  {
    name: 'account', label: 'Compte', field: (user: IUser) => fullName(user), align: 'left', sortable: true,
  },
  {
    name: 'role', label: 'Accès', field: 'isAdmin', align: 'left', sortable: true,
  },
  {
    name: 'actions', label: 'Actions', field: 'id', align: 'right',
  },
];

function normalize(value: string): string {
  return value.normalize('NFD').replace(/[\u0300-\u036f]/g, '').toLocaleLowerCase('fr').trim();
}

function fullName(user: IUser | ICreateUserDTO): string {
  return [user.firstName, user.lastName].filter(Boolean).join(' ').trim() || user.email;
}

function initials(user: IUser): string {
  const nameInitials = [user.firstName, user.lastName].filter(Boolean).map((name) => name.trim().charAt(0)).join('');
  return (nameInitials || user.email.slice(0, 2)).toLocaleUpperCase('fr');
}

const adminCount = computed(() => users.value.filter((user) => user.isAdmin).length);
const countsAvailable = computed(() => hasLoadedUsers.value && !loadError.value);
const visibleUsers = computed(() => {
  const terms = normalize(search.value ?? '').split(/\s+/).filter(Boolean);
  return users.value.filter((user) => {
    const roleMatches = roleFilter.value === 'all' || (roleFilter.value === 'admin') === user.isAdmin;
    const searchable = normalize(`${user.firstName} ${user.lastName} ${user.email}`);
    return roleMatches && terms.every((term) => searchable.includes(term));
  });
});
const hasFilters = computed(() => Boolean(search.value?.trim()) || roleFilter.value !== 'all');

let loadController: AbortController | undefined;
async function loadUsers() {
  loadController?.abort();
  const controller = new AbortController();
  loadController = controller;
  loading.value = true;
  loadError.value = '';
  try {
    const response = await getAdminUsers(controller.signal);
    if (!controller.signal.aborted) {
      users.value = response;
      hasLoadedUsers.value = true;
    }
  } catch (error) {
    if (!controller.signal.aborted) loadError.value = apiError(error);
  } finally {
    if (!controller.signal.aborted) loading.value = false;
  }
}

function clearFilters() {
  search.value = '';
  roleFilter.value = 'all';
}

const userToManage = ref<ManagedUser>();
const dialogUpdateUser = ref(false);
const saving = ref(false);
const saveError = ref('');

function openCreate() {
  userToManage.value = {
    firstName: '', lastName: '', email: '', isAdmin: false, password: '',
  };
  saveError.value = '';
  dialogUpdateUser.value = true;
}

function openEdit(user: IUser) {
  // Keep the table row untouched while the form is edited or the request fails.
  userToManage.value = { ...user };
  saveError.value = '';
  dialogUpdateUser.value = true;
}

function closeForm() {
  if (saving.value) return;
  dialogUpdateUser.value = false;
  userToManage.value = undefined;
}

async function submit(user: ManagedUser) {
  if (saving.value || !userToManage.value) return;
  saving.value = true;
  saveError.value = '';
  feedback.value = '';
  try {
    if ('id' in user) {
      const response = await updateAdminUser(user.id, user);
      users.value = users.value.map((account) => (account.id === response.id ? response : account));
      feedback.value = `Le compte de ${fullName(response)} a été mis à jour.`;
    } else {
      const response = await createAdminUser({ ...user, password: user.password ?? '' });
      users.value = [response, ...users.value];
      clearFilters();
      feedback.value = `Le compte de ${fullName(response)} a été créé.`;
    }
    dialogUpdateUser.value = false;
    userToManage.value = undefined;
  } catch (error) {
    saveError.value = apiError(error);
  } finally {
    saving.value = false;
  }
}

const userToDelete = ref<IUser>();
const deleteDialog = ref(false);
const deleting = ref(false);
const deleteError = ref('');

function openDelete(user: IUser) {
  userToDelete.value = { ...user };
  deleteError.value = '';
  deleteDialog.value = true;
}

async function confirmDelete() {
  if (!userToDelete.value || deleting.value) return;
  const user = userToDelete.value;
  deleting.value = true;
  deleteError.value = '';
  feedback.value = '';
  try {
    await deleteAdminUser(user.id);
    users.value = users.value.filter((account) => account.id !== user.id);
    feedback.value = `Le compte de ${fullName(user)} a été supprimé.`;
    deleteDialog.value = false;
    userToDelete.value = undefined;
  } catch (error) {
    deleteError.value = apiError(error);
  } finally {
    deleting.value = false;
  }
}

onMounted(loadUsers);
onBeforeUnmount(() => loadController?.abort());
</script>

<template>
  <AdminPageShell
    title="Utilisateurs"
    description="Gérez les comptes et les droits d’accès à DrIA."
  >
    <template #actions>
      <q-btn
        unelevated
        no-caps
        color="primary"
        icon="person_add_alt"
        label="Créer un utilisateur"
        @click="openCreate"
      />
    </template>

    <div class="admin-metrics">
      <div class="admin-metric">
        <span>Comptes</span>
        <strong>{{ countsAvailable ? users.length : '—' }}</strong>
      </div>
      <div class="admin-metric">
        <span>Administrateurs</span>
        <strong>{{ countsAvailable ? adminCount : '—' }}</strong>
      </div>
      <div class="admin-metric">
        <span>Utilisateurs</span>
        <strong>{{ countsAvailable ? users.length - adminCount : '—' }}</strong>
      </div>
    </div>

    <div v-if="feedback" class="admin-feedback" role="status">
      <q-icon name="check_circle" size="20px" />
      <span>{{ feedback }}</span>
      <q-btn flat round dense icon="close" aria-label="Masquer la confirmation" @click="feedback = ''" />
    </div>

    <div v-if="loadError" class="admin-feedback admin-feedback--error" role="alert">
      <q-icon name="info_outline" size="20px" />
      <div>
        <strong>Impossible de charger les utilisateurs.</strong>
        <div>{{ loadError }}</div>
      </div>
      <q-btn flat no-caps label="Réessayer" :loading="loading" @click="loadUsers" />
    </div>

    <section class="admin-panel" aria-label="Liste des utilisateurs">
      <div class="admin-panel-header">
        <div>
          <h2>Comptes de l’application</h2>
          <p>Retrouvez un utilisateur pour modifier son profil ou ses droits.</p>
        </div>
        <span v-if="!loading && !loadError" class="admin-cell-meta">
          {{ visibleUsers.length }} {{ visibleUsers.length > 1 ? 'comptes affichés' : 'compte affiché' }}
        </span>
      </div>

      <div class="admin-toolbar">
        <q-input
          v-model="search"
          class="admin-search"
          outlined
          dense
          clearable
          label="Rechercher un nom ou un email"
          :disable="loading"
          @clear="search = ''"
        >
          <template #prepend><q-icon name="search" /></template>
        </q-input>
        <q-select
          v-model="roleFilter"
          class="admin-filter"
          outlined
          dense
          emit-value
          map-options
          label="Accès"
          :options="roleOptions"
          :disable="loading"
        />
      </div>

      <q-table
        v-if="!loadError || users.length"
        v-model:pagination="pagination"
        class="admin-table admin-users-table"
        flat
        :rows="visibleUsers"
        :columns="columns"
        :grid="$q.screen.lt.sm"
        row-key="id"
        :loading="loading"
        :rows-per-page-options="[15, 30, 50]"
        rows-per-page-label="Comptes par page"
        loading-label="Chargement des utilisateurs…"
      >
        <template #item="{ row }">
          <article class="admin-mobile-card">
            <div class="admin-mobile-heading">
              <div class="admin-person">
                <q-avatar class="admin-avatar" size="40px">{{ initials(row) }}</q-avatar>
                <div>
                  <div class="admin-cell-title">{{ fullName(row) }}</div>
                  <div class="admin-cell-meta admin-user-email">{{ row.email }}</div>
                </div>
              </div>
            </div>
            <div class="admin-mobile-details">
              <span class="admin-cell-meta">Accès</span>
              <span class="admin-badge" :class="row.isAdmin ? 'admin-badge--success' : 'admin-badge--neutral'">
                <q-icon :name="row.isAdmin ? 'admin_panel_settings' : 'person_outline'" size="16px" />
                {{ row.isAdmin ? 'Administrateur' : 'Utilisateur' }}
              </span>
            </div>
            <div class="admin-mobile-actions">
              <q-btn
                flat
                dense
                no-caps
                icon="edit"
                label="Modifier"
                color="primary"
                :aria-label="`Modifier le compte de ${fullName(row)}`"
                @click="openEdit(row)"
              />
              <q-btn
                flat
                dense
                no-caps
                icon="delete_outline"
                label="Supprimer"
                :aria-label="`Supprimer le compte de ${fullName(row)}`"
                @click="openDelete(row)"
              />
            </div>
          </article>
        </template>
        <template #body-cell-account="{ row }">
          <q-td>
            <div class="admin-person">
              <q-avatar class="admin-avatar" size="40px">{{ initials(row) }}</q-avatar>
              <div>
                <div class="admin-cell-title">{{ fullName(row) }}</div>
                <div class="admin-cell-meta admin-user-email">{{ row.email }}</div>
              </div>
            </div>
          </q-td>
        </template>
        <template #body-cell-role="{ row }">
          <q-td>
            <span class="admin-badge" :class="row.isAdmin ? 'admin-badge--success' : 'admin-badge--neutral'">
              <q-icon :name="row.isAdmin ? 'admin_panel_settings' : 'person_outline'" size="16px" />
              {{ row.isAdmin ? 'Administrateur' : 'Utilisateur' }}
            </span>
          </q-td>
        </template>
        <template #body-cell-actions="{ row }">
          <q-td class="text-right">
            <div class="admin-row-actions">
              <q-btn flat dense no-caps icon="edit" label="Modifier" color="primary" @click="openEdit(row)" />
              <q-btn
                flat
                round
                dense
                icon="delete_outline"
                :aria-label="`Supprimer le compte de ${fullName(row)}`"
                @click="openDelete(row)"
              >
                <q-tooltip>Supprimer le compte</q-tooltip>
              </q-btn>
            </div>
          </q-td>
        </template>
        <template #no-data>
          <div class="admin-empty">
            <q-icon :name="hasFilters ? 'search_off' : 'people_outline'" size="36px" />
            <h3>{{ loading ? 'Chargement des comptes…' : hasFilters ? 'Aucun utilisateur trouvé' : 'Aucun compte pour le moment' }}</h3>
            <p v-if="!loading">{{ hasFilters ? 'Essayez un autre nom, email ou rôle.' : 'Créez le premier compte pour donner accès à DrIA.' }}</p>
            <q-btn v-if="!loading && hasFilters" flat no-caps color="primary" label="Effacer les filtres" @click="clearFilters" />
          </div>
        </template>
      </q-table>
    </section>

    <q-dialog v-model="dialogUpdateUser" :persistent="saving">
      <q-card class="admin-dialog project-workspace-colors">
        <q-card-section class="admin-dialog-heading">
          <div>
            <h2>{{ userToManage && 'id' in userToManage ? 'Modifier le compte' : 'Créer un utilisateur' }}</h2>
            <p>{{ userToManage && 'id' in userToManage ? 'Mettez à jour le profil et les droits d’accès.' : 'Renseignez le profil et les identifiants de connexion.' }}</p>
          </div>
          <q-btn flat round dense icon="close" aria-label="Fermer le formulaire" :disable="saving" @click="closeForm" />
        </q-card-section>
        <q-card-section class="q-pa-none">
          <div v-if="saveError" class="admin-feedback admin-feedback--error q-ma-lg" role="alert">
            <q-icon name="info_outline" size="20px" />
            <div><strong>Le compte n’a pas pu être enregistré.</strong><div>{{ saveError }}</div></div>
          </div>
          <ManageUserForm
            v-if="userToManage"
            :user="userToManage"
            :loading="saving"
            @submit="submit"
            @cancel="closeForm"
          />
        </q-card-section>
      </q-card>
    </q-dialog>

    <q-dialog v-model="deleteDialog" :persistent="deleting">
      <q-card v-if="userToDelete" class="admin-dialog project-workspace-colors">
        <q-card-section class="admin-dialog-heading">
          <div><h2>Supprimer ce compte ?</h2><p>Cette suppression est définitive.</p></div>
          <q-btn flat round dense icon="close" aria-label="Fermer la confirmation" :disable="deleting" @click="deleteDialog = false" />
        </q-card-section>
        <q-card-section>
          <div class="admin-person admin-delete-person">
            <q-avatar class="admin-avatar" size="40px">{{ initials(userToDelete) }}</q-avatar>
            <div>
              <div class="admin-cell-title">{{ fullName(userToDelete) }}</div>
              <div class="admin-cell-meta admin-user-email">{{ userToDelete.email }}</div>
            </div>
          </div>
          <p class="admin-cell-meta">Cet utilisateur ne pourra plus se connecter à DrIA.</p>
          <div v-if="deleteError" class="admin-feedback admin-feedback--error" role="alert">
            <q-icon name="info_outline" size="20px" />
            <div><strong>La suppression a échoué.</strong><div>{{ deleteError }}</div></div>
          </div>
        </q-card-section>
        <q-card-actions class="admin-dialog-actions">
          <q-btn flat no-caps label="Annuler" :disable="deleting" @click="deleteDialog = false" />
          <q-btn unelevated no-caps color="negative" label="Supprimer le compte" :loading="deleting" @click="confirmDelete" />
        </q-card-actions>
      </q-card>
    </q-dialog>
  </AdminPageShell>
</template>

<style scoped lang="scss">
.admin-user-email { overflow-wrap: anywhere; }
.admin-delete-person { margin-bottom: 16px; }
.admin-feedback > div, .admin-feedback > span { flex: 1; min-width: 0; overflow-wrap: anywhere; }
.admin-users-table :deep(tbody td:first-child) { min-width: 230px; white-space: normal; }
.admin-users-table :deep(.admin-person > div:last-child) { min-width: 0; }
.admin-users-table :deep(.admin-row-actions) { justify-content: flex-end; flex-wrap: nowrap; }
.admin-users-table :deep(.admin-empty) { width: 100%; }
</style>
