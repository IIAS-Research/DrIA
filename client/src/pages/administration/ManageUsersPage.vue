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
  { label: 'All roles', value: 'all' },
  { label: 'Administrators', value: 'admin' },
  { label: 'Users', value: 'user' },
];
const pagination = ref({ rowsPerPage: 15, sortBy: 'account', descending: false });
const columns: QTableColumn[] = [
  {
    name: 'account', label: 'Account', field: (user: IUser) => fullName(user), align: 'left', sortable: true,
  },
  {
    name: 'role', label: 'Access', field: 'isAdmin', align: 'left', sortable: true,
  },
  {
    name: 'actions', label: 'Actions', field: 'id', align: 'right',
  },
];

function normalize(value: string): string {
  return value.normalize('NFD').replace(/[\u0300-\u036f]/g, '').toLocaleLowerCase('en-US').trim();
}

function fullName(user: IUser | ICreateUserDTO): string {
  return [user.firstName, user.lastName].filter(Boolean).join(' ').trim() || user.email;
}

function initials(user: IUser): string {
  const nameInitials = [user.firstName, user.lastName].filter(Boolean).map((name) => name.trim().charAt(0)).join('');
  return (nameInitials || user.email.slice(0, 2)).toLocaleUpperCase('en-US');
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
      feedback.value = `Account for ${fullName(response)} updated.`;
    } else {
      const response = await createAdminUser({ ...user, password: user.password ?? '' });
      users.value = [response, ...users.value];
      clearFilters();
      feedback.value = `Account for ${fullName(response)} created.`;
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
    feedback.value = `Account for ${fullName(user)} deleted.`;
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
    title="Users"
    description="Manage accounts and access to DrIA."
  >
    <template #actions>
      <q-btn
        unelevated
        no-caps
        color="primary"
        icon="person_add_alt"
        label="Create user"
        @click="openCreate"
      />
    </template>

    <div class="admin-metrics">
      <div class="admin-metric">
        <span>Accounts</span>
        <strong>{{ countsAvailable ? users.length : '—' }}</strong>
      </div>
      <div class="admin-metric">
        <span>Administrators</span>
        <strong>{{ countsAvailable ? adminCount : '—' }}</strong>
      </div>
      <div class="admin-metric">
        <span>Users</span>
        <strong>{{ countsAvailable ? users.length - adminCount : '—' }}</strong>
      </div>
    </div>

    <div v-if="feedback" class="admin-feedback" role="status">
      <q-icon name="check_circle" size="20px" />
      <span>{{ feedback }}</span>
      <q-btn flat round dense icon="close" aria-label="Dismiss confirmation" @click="feedback = ''" />
    </div>

    <div v-if="loadError" class="admin-feedback admin-feedback--error" role="alert">
      <q-icon name="info_outline" size="20px" />
      <div>
        <strong>Could not load users.</strong>
        <div>{{ loadError }}</div>
      </div>
      <q-btn flat no-caps label="Retry" :loading="loading" @click="loadUsers" />
    </div>

    <section class="admin-panel" aria-label="User list">
      <div class="admin-panel-header">
        <div>
          <h2>Application accounts</h2>
          <p>Find a user to edit their profile or access.</p>
        </div>
        <span v-if="!loading && !loadError" class="admin-cell-meta">
          {{ visibleUsers.length }} {{ visibleUsers.length === 1 ? 'account shown' : 'accounts shown' }}
        </span>
      </div>

      <div class="admin-toolbar">
        <q-input
          v-model="search"
          class="admin-search"
          outlined
          dense
          clearable
          label="Search by name or email"
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
          label="Access"
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
        rows-per-page-label="Accounts per page"
        loading-label="Loading users…"
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
              <span class="admin-cell-meta">Access</span>
              <span class="admin-badge" :class="row.isAdmin ? 'admin-badge--success' : 'admin-badge--neutral'">
                <q-icon :name="row.isAdmin ? 'admin_panel_settings' : 'person_outline'" size="16px" />
                {{ row.isAdmin ? 'Administrator' : 'User' }}
              </span>
            </div>
            <div class="admin-mobile-actions">
              <q-btn
                flat
                dense
                no-caps
                icon="edit"
                label="Edit"
                color="primary"
                :aria-label="`Edit account for ${fullName(row)}`"
                @click="openEdit(row)"
              />
              <q-btn
                flat
                dense
                no-caps
                icon="delete_outline"
                label="Delete"
                :aria-label="`Delete account for ${fullName(row)}`"
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
              {{ row.isAdmin ? 'Administrator' : 'User' }}
            </span>
          </q-td>
        </template>
        <template #body-cell-actions="{ row }">
          <q-td class="text-right">
            <div class="admin-row-actions">
              <q-btn flat dense no-caps icon="edit" label="Edit" color="primary" @click="openEdit(row)" />
              <q-btn
                flat
                round
                dense
                icon="delete_outline"
                :aria-label="`Delete account for ${fullName(row)}`"
                @click="openDelete(row)"
              >
                <q-tooltip>Delete account</q-tooltip>
              </q-btn>
            </div>
          </q-td>
        </template>
        <template #no-data>
          <div class="admin-empty">
            <q-icon :name="hasFilters ? 'search_off' : 'people_outline'" size="36px" />
            <h3>{{ loading ? 'Loading accounts…' : hasFilters ? 'No users found' : 'No accounts yet' }}</h3>
            <p v-if="!loading">{{ hasFilters ? 'Try another name, email or role.' : 'Create the first account to give access to DrIA.' }}</p>
            <q-btn v-if="!loading && hasFilters" flat no-caps color="primary" label="Clear filters" @click="clearFilters" />
          </div>
        </template>
      </q-table>
    </section>

    <q-dialog v-model="dialogUpdateUser" :persistent="saving">
      <q-card class="admin-dialog project-workspace-colors">
        <q-card-section class="admin-dialog-heading">
          <div>
            <h2>{{ userToManage && 'id' in userToManage ? 'Edit account' : 'Create user' }}</h2>
            <p>{{ userToManage && 'id' in userToManage ? 'Update the profile and access rights.' : 'Enter the profile and sign-in details.' }}</p>
          </div>
          <q-btn flat round dense icon="close" aria-label="Close form" :disable="saving" @click="closeForm" />
        </q-card-section>
        <q-card-section class="q-pa-none">
          <div v-if="saveError" class="admin-feedback admin-feedback--error q-ma-lg" role="alert">
            <q-icon name="info_outline" size="20px" />
            <div><strong>Could not save the account.</strong><div>{{ saveError }}</div></div>
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
          <div><h2>Delete this account?</h2><p>This deletion is permanent.</p></div>
          <q-btn flat round dense icon="close" aria-label="Close confirmation" :disable="deleting" @click="deleteDialog = false" />
        </q-card-section>
        <q-card-section>
          <div class="admin-person admin-delete-person">
            <q-avatar class="admin-avatar" size="40px">{{ initials(userToDelete) }}</q-avatar>
            <div>
              <div class="admin-cell-title">{{ fullName(userToDelete) }}</div>
              <div class="admin-cell-meta admin-user-email">{{ userToDelete.email }}</div>
            </div>
          </div>
          <p class="admin-cell-meta">This user will no longer be able to sign in to DrIA.</p>
          <div v-if="deleteError" class="admin-feedback admin-feedback--error" role="alert">
            <q-icon name="info_outline" size="20px" />
            <div><strong>Deletion failed.</strong><div>{{ deleteError }}</div></div>
          </div>
        </q-card-section>
        <q-card-actions class="admin-dialog-actions">
          <q-btn flat no-caps label="Cancel" :disable="deleting" @click="deleteDialog = false" />
          <q-btn unelevated no-caps color="negative" label="Delete account" :loading="deleting" @click="confirmDelete" />
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
