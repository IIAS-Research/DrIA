<script setup lang="ts">
import { computed, onBeforeUnmount, onMounted, ref } from 'vue';
import { useQuasar, type QTableProps } from 'quasar';
import type { IPullingData } from 'src/api/admin.api';
import { getAdminMonitoring, stopAdminMonitoring } from 'src/api/administration.api';
import AdminPageShell from 'src/components/administration/AdminPageShell.vue';

type CalculationType = 'training' | 'explanation';
interface MonitoringRow {
  key: string;
  jobWebServiceId: string;
  type: CalculationType;
  isPulling: boolean;
  status: string;
}

const $q = useQuasar();
const rows = ref<MonitoringRow[]>([]);
const loading = ref(false);
const loaded = ref(false);
const loadError = ref('');
const search = ref('');
const typeFilter = ref<CalculationType | 'all'>('all');
const lastUpdated = ref<Date | null>(null);
const confirmationOpen = ref(false);
const selectedConnection = ref<MonitoringRow | null>(null);
const stopping = ref(false);
const stopError = ref('');
const feedback = ref('');
let requestController: AbortController | null = null;

const typeOptions = [
  { label: 'All jobs', value: 'all' },
  { label: 'Training runs', value: 'training' },
  { label: 'Explanations', value: 'explanation' },
];
const columns: QTableProps['columns'] = [
  { name: 'calculation', label: 'Job', field: 'jobWebServiceId', align: 'left', sortable: true },
  { name: 'status', label: 'Job status', field: 'status', align: 'left', sortable: true },
  { name: 'monitoring', label: 'Monitoring', field: 'isPulling', align: 'left', sortable: true },
  { name: 'actions', label: 'Actions', field: 'key', align: 'right' },
];
const activeCount = computed(() => rows.value.filter((row) => row.isPulling).length);
const interruptedCount = computed(() => rows.value.length - activeCount.value);
const filteredRows = computed(() => {
  const query = (search.value ?? '').trim().toLocaleLowerCase('en-US');
  return rows.value.filter((row) =>
    (typeFilter.value === 'all' || row.type === typeFilter.value)
    && (!query || row.jobWebServiceId.toLocaleLowerCase('en-US').includes(query)));
});
const matchingConnections = computed(() => rows.value.filter((row) =>
  row.jobWebServiceId === selectedConnection.value?.jobWebServiceId));
const lastUpdatedLabel = computed(() => lastUpdated.value
  ? new Intl.DateTimeFormat('en-US', { hour: '2-digit', minute: '2-digit', second: '2-digit' }).format(lastUpdated.value)
  : '');

function typeLabel(type: CalculationType): string {
  return type === 'training' ? 'Training run' : 'Explanation';
}

function calculationState(status: string): { label: string; tone: string } {
  const states: Record<string, { label: string; tone: string }> = {
    QUEUED: { label: 'Queued', tone: 'neutral' },
    PENDING: { label: 'Queued', tone: 'neutral' },
    RUNNING: { label: 'Running', tone: 'success' },
    COMPUTING: { label: 'Running', tone: 'success' },
    FINISHED: { label: 'Completed', tone: 'success' },
    CACHED: { label: 'Results available', tone: 'success' },
    CRASHED: { label: 'Job failed', tone: 'error' },
    CANCELED: { label: 'Canceled', tone: 'neutral' },
    CANCELLED: { label: 'Canceled', tone: 'neutral' },
    TIMEOUT: { label: 'Time limit reached', tone: 'warning' },
  };
  return states[status.toUpperCase()] ?? { label: 'Status unavailable', tone: 'neutral' };
}

function formatConnections(data: IPullingData): MonitoringRow[] {
  return [
    ...(data.connectionsModel ?? []).map((connection) => ({ ...connection, type: 'training' as const })),
    ...(data.connectionsExplain ?? []).map((connection) => ({ ...connection, type: 'explanation' as const })),
  ].map((connection) => ({
    key: `${connection.type}:${connection.jobWebServiceId}`,
    jobWebServiceId: connection.jobWebServiceId,
    type: connection.type,
    isPulling: connection.isPulling,
    status: connection.status ?? '',
  })).sort((a, b) => Number(b.isPulling) - Number(a.isPulling)
    || a.jobWebServiceId.localeCompare(b.jobWebServiceId));
}

async function refresh() {
  if (loading.value || stopping.value) return;
  requestController = new AbortController();
  const controller = requestController;
  loading.value = true;
  loadError.value = '';
  try {
    const data = await getAdminMonitoring(controller.signal);
    if (controller.signal.aborted) return;
    rows.value = formatConnections(data);
    loaded.value = true;
    lastUpdated.value = new Date();
  } catch {
    if (!controller.signal.aborted) loadError.value = 'Job monitoring is unavailable. Try again shortly.';
  } finally {
    if (!controller.signal.aborted) loading.value = false;
  }
}

function openConfirmation(row: MonitoringRow) {
  selectedConnection.value = row;
  stopError.value = '';
  feedback.value = '';
  confirmationOpen.value = true;
}

async function stopMonitoring() {
  if (!selectedConnection.value || stopping.value) return;
  const jobId = selectedConnection.value.jobWebServiceId;
  stopping.value = true;
  stopError.value = '';
  try {
    await stopAdminMonitoring(jobId);
    rows.value = rows.value.filter((row) => row.jobWebServiceId !== jobId);
    confirmationOpen.value = false;
    selectedConnection.value = null;
    feedback.value = 'Monitoring removed. The job was not canceled.';
  } catch {
    stopError.value = 'Could not remove monitoring. Try again.';
  } finally {
    stopping.value = false;
  }
}

onMounted(() => { void refresh(); });
onBeforeUnmount(() => { requestController?.abort(); });
</script>

<template>
  <AdminPageShell title="Job monitoring" description="View the status of training runs and explanations monitored by DrIA.">
    <template #actions>
      <q-btn outline no-caps color="primary" icon="refresh" label="Refresh" :loading="loading" :disable="stopping" @click="refresh" />
    </template>

    <div v-if="loadError" class="admin-feedback admin-feedback--error" role="alert">
      <q-icon name="info_outline" size="20px" />
      <p>{{ loadError }}</p>
      <q-btn flat no-caps color="primary" label="Retry" :loading="loading" @click="refresh" />
    </div>
    <div v-if="feedback" class="admin-feedback" role="status">
      <q-icon name="check_circle_outline" size="20px" />
      <p>{{ feedback }}</p>
      <q-btn flat round dense icon="close" aria-label="Dismiss message" @click="feedback = ''" />
    </div>

    <div class="admin-metrics" aria-label="Monitoring summary">
      <div class="admin-metric">
        <span>Monitored jobs</span>
        <strong>{{ loaded ? rows.length : '—' }}</strong>
        <p>Training runs and explanations</p>
      </div>
      <div class="admin-metric">
        <span>Active connections</span>
        <strong>{{ loaded ? activeCount : '—' }}</strong>
        <p>Status retrieved from the service</p>
      </div>
      <div class="admin-metric">
        <span>Inactive connections</span>
        <strong>{{ loaded ? interruptedCount : '—' }}</strong>
        <p>Saved status, without updates</p>
      </div>
    </div>

    <section class="admin-panel" aria-labelledby="monitoring-list-title" :aria-busy="loading">
      <div class="admin-panel-header">
        <div>
          <h2 id="monitoring-list-title">DrIA jobs</h2>
          <p>Monitoring retrieves job status and results.</p>
        </div>
        <span v-if="lastUpdatedLabel" class="monitoring-updated">Updated at {{ lastUpdatedLabel }}</span>
      </div>

      <div class="admin-toolbar">
        <q-input v-model="search" class="admin-search" outlined dense clearable placeholder="Search by ID" aria-label="Search jobs by ID">
          <template #prepend><q-icon name="search" size="20px" /></template>
        </q-input>
        <q-select v-model="typeFilter" class="admin-filter" outlined dense emit-value map-options :options="typeOptions" aria-label="Filter by job type" />
      </div>

      <q-table v-if="rows.length || loading" class="admin-table monitoring-table" flat :grid="$q.screen.lt.sm" :rows="filteredRows" :columns="columns" row-key="key" :loading="loading" :pagination="{ rowsPerPage: 10 }" :rows-per-page-options="[10, 25, 50]" rows-per-page-label="Jobs per page" no-data-label="No jobs match your search.">
        <template #item="slotProps">
          <article class="admin-mobile-card">
            <div class="admin-mobile-heading">
              <div>
                <div class="admin-cell-title monitoring-type"><q-icon :name="slotProps.row.type === 'training' ? 'tune' : 'insights'" size="18px" />{{ typeLabel(slotProps.row.type) }}</div>
                <span class="admin-cell-meta monitoring-mobile-id">{{ slotProps.row.jobWebServiceId }}</span>
              </div>
            </div>
            <div class="admin-mobile-details">
              <div>
                <span>Job status</span>
                <span :class="['admin-badge', 'admin-badge--' + calculationState(slotProps.row.status).tone]">{{ calculationState(slotProps.row.status).label }}</span>
              </div>
              <div>
                <span>Monitoring</span>
                <span :class="['admin-badge', slotProps.row.isPulling ? 'admin-badge--success' : 'admin-badge--neutral']">{{ slotProps.row.isPulling ? 'Active' : 'Inactive' }}</span>
              </div>
            </div>
            <div class="admin-mobile-actions">
              <q-btn flat no-caps color="primary" :label="slotProps.row.isPulling ? 'Stop monitoring' : 'Remove monitoring'" :disable="loading || stopping" @click="openConfirmation(slotProps.row)" />
            </div>
          </article>
        </template>
        <template #body-cell-calculation="slotProps">
          <q-td :props="slotProps">
            <div class="admin-cell-title monitoring-type"><q-icon :name="slotProps.row.type === 'training' ? 'tune' : 'insights'" size="18px" />{{ typeLabel(slotProps.row.type) }}</div>
            <span class="admin-cell-meta monitoring-id" :title="slotProps.row.jobWebServiceId">{{ slotProps.row.jobWebServiceId }}</span>
          </q-td>
        </template>
        <template #body-cell-status="slotProps">
          <q-td :props="slotProps">
            <span :class="['admin-badge', 'admin-badge--' + calculationState(slotProps.row.status).tone]">{{ calculationState(slotProps.row.status).label }}</span>
          </q-td>
        </template>
        <template #body-cell-monitoring="slotProps">
          <q-td :props="slotProps">
            <span :class="['admin-badge', slotProps.row.isPulling ? 'admin-badge--success' : 'admin-badge--neutral']">{{ slotProps.row.isPulling ? 'Active' : 'Inactive' }}</span>
          </q-td>
        </template>
        <template #body-cell-actions="slotProps">
          <q-td :props="slotProps">
            <div class="admin-row-actions">
              <q-btn flat no-caps color="primary" :label="slotProps.row.isPulling ? 'Stop monitoring' : 'Remove monitoring'" :disable="loading || stopping" @click="openConfirmation(slotProps.row)" />
            </div>
          </q-td>
        </template>
        <template #no-data>
          <div v-if="!loading" class="admin-empty monitoring-filter-empty">
            <q-icon name="search_off" size="26px" />
            <h3>No jobs found</h3>
            <p>Change the job ID or job type.</p>
            <q-btn flat no-caps color="primary" label="Clear filters" @click="search = ''; typeFilter = 'all'" />
          </div>
        </template>
      </q-table>
      <div v-else-if="loaded" class="admin-empty">
        <q-icon name="task_alt" size="32px" />
        <h3>No monitored jobs yet</h3>
        <p>Training runs and explanations appear here when monitored by DrIA.</p>
      </div>
    </section>

    <p class="monitoring-help"><q-icon name="info_outline" size="18px" />Stopping monitoring removes the connection to DrIA. It does not cancel the job in the service.</p>

    <q-dialog v-model="confirmationOpen" :persistent="stopping">
      <q-card class="admin-dialog project-workspace-colors" aria-labelledby="monitoring-confirmation-title">
        <q-card-section class="admin-dialog-heading">
          <div>
            <h2 id="monitoring-confirmation-title">{{ selectedConnection?.isPulling ? 'Stop monitoring this job?' : 'Remove monitoring for this job?' }}</h2>
            <p>DrIA will no longer retrieve updates or results for this job. The job itself will not be canceled.</p>
          </div>
        </q-card-section>
        <q-card-section class="monitoring-dialog-body">
          <p v-if="matchingConnections.length > 1">This ID is shared by {{ matchingConnections.length }} monitoring connections. All will be removed.</p>
          <div class="monitoring-confirmation-id"><span>Job ID</span><strong>{{ selectedConnection?.jobWebServiceId }}</strong></div>
          <p v-if="stopError" class="admin-feedback admin-feedback--error" role="alert">{{ stopError }}</p>
        </q-card-section>
        <q-card-actions class="admin-dialog-actions" align="right">
          <q-btn flat no-caps label="Keep monitoring" :disable="stopping" @click="confirmationOpen = false" />
          <q-btn unelevated no-caps color="primary" :label="selectedConnection?.isPulling ? 'Stop monitoring' : 'Remove monitoring'" :loading="stopping" @click="stopMonitoring" />
        </q-card-actions>
      </q-card>
    </q-dialog>
  </AdminPageShell>
</template>

<style scoped lang="scss">
.monitoring-updated { color: #6d7b89; font-size: 12px; white-space: nowrap; }
.monitoring-id { display: block; max-width: 270px; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; font-family: ui-monospace, SFMono-Regular, Menlo, monospace; font-size: 11px; }
.monitoring-mobile-id { font-family: ui-monospace, SFMono-Regular, Menlo, monospace; font-size: 11px; overflow-wrap: anywhere; }
.monitoring-type { display: flex; align-items: center; gap: 7px; }
.monitoring-type .q-icon { color: #556c7b; }
.monitoring-help { display: flex; align-items: flex-start; gap: 8px; margin: 16px 0 0; color: var(--project-muted); font-size: 12px; line-height: 1.6; }
.monitoring-help .q-icon { flex-shrink: 0; margin-top: 1px; }
.monitoring-filter-empty { width: 100%; }
.monitoring-confirmation-id { padding: 14px 16px; border-radius: 12px; background: #f5f8fa; }
.monitoring-dialog-body { padding: 24px; }
.monitoring-dialog-body > p { font-size: 12px; line-height: 1.7; }
.monitoring-dialog-body .admin-feedback { margin: 16px 0 0; }
.monitoring-confirmation-id span { display: block; color: #6d7b89; font-size: 12px; margin-bottom: 5px; }
.monitoring-confirmation-id strong { font-family: ui-monospace, SFMono-Regular, Menlo, monospace; font-size: 12px; overflow-wrap: anywhere; }
@media (max-width: 600px) { .monitoring-updated { display: block; margin-top: 8px; } .monitoring-id { max-width: 210px; } }
</style>
