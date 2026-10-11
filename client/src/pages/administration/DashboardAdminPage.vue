<script setup lang="ts">
import { computed, onMounted, onUnmounted, ref } from 'vue';
import AdminPageShell from 'src/components/administration/AdminPageShell.vue';
import { getAdminMonitoring, getAdminUsers } from 'src/api/administration.api';
import type { IUser } from 'src/components/models';
import type { IPullingData } from 'src/api/admin.api';

const users = ref<IUser[]>();
const monitoring = ref<IPullingData>();
const loading = ref(false);
const usersUnavailable = ref(false);
const monitoringUnavailable = ref(false);
const administratorCount = computed(() => users.value?.filter((user) => user.isAdmin).length);
const activeMonitoringCount = computed(() => monitoring.value
  ? [...monitoring.value.connectionsModel, ...monitoring.value.connectionsExplain].filter((connection) => connection.isPulling).length : undefined);
const number = new Intl.NumberFormat('en-US');
const count = (value: number | undefined) => value === undefined ? '—' : number.format(value);
let controller: AbortController | undefined;
let revision = 0;

const entries = [
  { route: 'admin-users', icon: 'group', category: 'Access and roles', title: 'Users', description: 'Create accounts, update profiles and grant administrator access.', action: 'Manage users' },
  { route: 'admin-projects', icon: 'folder_open', category: 'Studies and data', title: 'Projects', description: 'Find studies and their owners, and manage projects stored in DrIA.', action: 'Browse projects' },
  { route: 'admin-pulling', icon: 'sync', category: 'Activity', title: 'Job monitoring', description: 'Monitor training runs and explanations, and manage their monitoring connections.', action: 'View monitoring' },
];

async function load() {
  controller?.abort();
  controller = new AbortController();
  const currentController = controller;
  const currentRevision = ++revision;
  loading.value = true;
  const results = await Promise.allSettled([
    getAdminUsers(currentController.signal), getAdminMonitoring(currentController.signal),
  ]);
  if (currentRevision !== revision || currentController.signal.aborted) return;
  usersUnavailable.value = results[0].status === 'rejected';
  monitoringUnavailable.value = results[1].status === 'rejected';
  users.value = results[0].status === 'fulfilled' ? results[0].value as IUser[] : undefined;
  monitoring.value = results[1].status === 'fulfilled' ? results[1].value as IPullingData : undefined;
  loading.value = false;
}
onMounted(load);
onUnmounted(() => { revision += 1; controller?.abort(); });
</script>

<template>
  <AdminPageShell title="Administration" description="Manage access, projects and job monitoring in one place.">
    <template #actions><q-btn outline no-caps color="primary" icon="refresh" label="Refresh" :loading="loading" @click="load" /></template>

    <div v-if="usersUnavailable || monitoringUnavailable" class="admin-feedback" role="status">
      <q-icon name="info_outline" size="20px" />
      <p>Some indicators are unavailable. Refresh this page or open the relevant section.</p>
    </div>
    <section class="admin-metrics" aria-label="Administration indicators" :aria-busy="loading">
      <div class="admin-metric"><span>Users</span><strong><q-spinner v-if="loading" color="primary" size="24px" /><template v-else>{{ count(users?.length) }}</template></strong><p>{{ usersUnavailable ? 'List temporarily unavailable' : 'DrIA accounts' }}</p></div>
      <div class="admin-metric"><span>Administrators</span><strong><q-spinner v-if="loading" color="primary" size="24px" /><template v-else>{{ count(administratorCount) }}</template></strong><p>{{ usersUnavailable ? 'List temporarily unavailable' : 'Accounts with administrator access' }}</p></div>
      <div class="admin-metric"><span>Active connections</span><strong><q-spinner v-if="loading" color="primary" size="24px" /><template v-else>{{ count(activeMonitoringCount) }}</template></strong><p>{{ monitoringUnavailable ? 'Monitoring temporarily unavailable' : 'Monitoring connections for training runs and explanations' }}</p></div>
    </section>

    <section class="admin-entry-section" aria-labelledby="admin-sections-title">
      <div class="admin-entry-section-heading"><h2 id="admin-sections-title">Management tools</h2><p>Choose the section you need.</p></div>
      <div class="admin-entries">
        <router-link v-for="entry in entries" :key="entry.route" :to="{ name: entry.route }" class="admin-entry">
          <div class="admin-entry-top"><span class="admin-entry-icon"><q-icon :name="entry.icon" size="25px" aria-hidden="true" /></span><span class="admin-entry-category">{{ entry.category }}</span></div>
          <h3>{{ entry.title }}</h3>
          <p>{{ entry.description }}</p>
          <span class="admin-entry-action">{{ entry.action }}<q-icon name="arrow_forward" size="18px" aria-hidden="true" /></span>
        </router-link>
      </div>
    </section>
  </AdminPageShell>
</template>

<style scoped>
.admin-entry-section { margin-top: 34px; }
.admin-entry-section-heading { margin-bottom: 18px; }
.admin-entry-section-heading h2 { margin: 0; font-size: 17px; font-weight: 600; line-height: 1.5; }
.admin-entry-section-heading p { margin: 6px 0 0; color: var(--project-muted); font-size: 12px; line-height: 1.7; }
.admin-entries { display: grid; grid-template-columns: repeat(3, minmax(0, 1fr)); gap: 18px; }
.admin-entry { display: flex; flex-direction: column; min-width: 0; padding: 24px; color: var(--project-ink); background: var(--project-surface); border: 1px solid var(--project-border); border-radius: 12px; text-decoration: none; transition: border-color .18s, background-color .18s; }
.admin-entry:hover { border-color: var(--project-brand); background: var(--project-tint); }
.admin-entry:focus-visible { outline: 2px solid var(--q-primary); outline-offset: 3px; }
.admin-entry-top { display: flex; align-items: center; gap: 12px; }
.admin-entry-icon { display: inline-flex; align-items: center; justify-content: center; flex-shrink: 0; width: 46px; height: 46px; color: var(--project-selection-ink); background: var(--project-tint); border: 1px solid #d5e9e8; border-radius: 10px; }
.admin-entry-category { color: var(--project-muted); font-size: 11px; line-height: 1.5; }
.admin-entry h3 { margin: 22px 0 10px; font-size: 20px; font-weight: 600; line-height: 1.4; }
.admin-entry > p { flex: 1; margin: 0; color: var(--project-muted); font-size: 13px; line-height: 1.8; }
.admin-entry-action { display: flex; justify-content: space-between; align-items: center; gap: 12px; margin-top: 26px; padding-top: 18px; color: var(--q-primary); border-top: 1px solid var(--project-border); font-size: 12px; font-weight: 500; }
@media (max-width: 899px) {
  .admin-entries { grid-template-columns: 1fr; gap: 12px; }
  .admin-entry { display: grid; grid-template-columns: 1fr auto; gap: 8px 18px; padding: 22px; }
  .admin-entry-top, .admin-entry h3, .admin-entry > p { grid-column: 1; }
  .admin-entry h3 { margin: 8px 0 0; }
  .admin-entry-action { grid-column: 2; grid-row: 1 / span 3; align-self: center; max-width: 180px; margin: 0; padding: 0; border: 0; }
}
@media (max-width: 599px) {
  .admin-entry-section { margin-top: 28px; }
  .admin-entry { display: flex; padding: 20px; }
  .admin-entry h3 { margin: 16px 0 8px; font-size: 19px; }
  .admin-entry-action { width: 100%; max-width: none; margin-top: 18px; padding-top: 16px; border-top: 1px solid var(--project-border); }
}
</style>
