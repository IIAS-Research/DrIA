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
  { label: 'Tous les calculs', value: 'all' },
  { label: 'Entraînements', value: 'training' },
  { label: 'Explications', value: 'explanation' },
];
const columns: QTableProps['columns'] = [
  { name: 'calculation', label: 'Calcul', field: 'jobWebServiceId', align: 'left', sortable: true },
  { name: 'status', label: 'État du calcul', field: 'status', align: 'left', sortable: true },
  { name: 'monitoring', label: 'Suivi', field: 'isPulling', align: 'left', sortable: true },
  { name: 'actions', label: 'Actions', field: 'key', align: 'right' },
];
const activeCount = computed(() => rows.value.filter((row) => row.isPulling).length);
const interruptedCount = computed(() => rows.value.length - activeCount.value);
const filteredRows = computed(() => {
  const query = (search.value ?? '').trim().toLocaleLowerCase('fr');
  return rows.value.filter((row) =>
    (typeFilter.value === 'all' || row.type === typeFilter.value)
    && (!query || row.jobWebServiceId.toLocaleLowerCase('fr').includes(query)));
});
const matchingConnections = computed(() => rows.value.filter((row) =>
  row.jobWebServiceId === selectedConnection.value?.jobWebServiceId));
const lastUpdatedLabel = computed(() => lastUpdated.value
  ? new Intl.DateTimeFormat('fr-FR', { hour: '2-digit', minute: '2-digit', second: '2-digit' }).format(lastUpdated.value)
  : '');

function typeLabel(type: CalculationType): string {
  return type === 'training' ? 'Entraînement' : 'Explication';
}

function calculationState(status: string): { label: string; tone: string } {
  const states: Record<string, { label: string; tone: string }> = {
    QUEUED: { label: 'En attente', tone: 'neutral' },
    PENDING: { label: 'En attente', tone: 'neutral' },
    RUNNING: { label: 'En cours', tone: 'success' },
    COMPUTING: { label: 'En cours', tone: 'success' },
    FINISHED: { label: 'Terminé', tone: 'success' },
    CACHED: { label: 'Résultats disponibles', tone: 'success' },
    CRASHED: { label: 'Échec du calcul', tone: 'error' },
    CANCELED: { label: 'Annulé', tone: 'neutral' },
    CANCELLED: { label: 'Annulé', tone: 'neutral' },
    TIMEOUT: { label: 'Temps maximal atteint', tone: 'warning' },
  };
  return states[status.toUpperCase()] ?? { label: 'État indisponible', tone: 'neutral' };
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
    if (!controller.signal.aborted) loadError.value = 'Le suivi des calculs est indisponible. Réessayez dans quelques instants.';
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
    feedback.value = 'Le suivi a été retiré. Le calcul n’a pas été annulé.';
  } catch {
    stopError.value = 'Le suivi n’a pas pu être retiré. Réessayez.';
  } finally {
    stopping.value = false;
  }
}

onMounted(() => { void refresh(); });
onBeforeUnmount(() => { requestController?.abort(); });
</script>

<template>
  <AdminPageShell title="Suivi des calculs" description="Consultez l’état des entraînements et des explications suivis par DrIA.">
    <template #actions>
      <q-btn outline no-caps color="primary" icon="refresh" label="Actualiser" :loading="loading" :disable="stopping" @click="refresh" />
    </template>

    <div v-if="loadError" class="admin-feedback admin-feedback--error" role="alert">
      <q-icon name="info_outline" size="20px" />
      <p>{{ loadError }}</p>
      <q-btn flat no-caps color="primary" label="Réessayer" :loading="loading" @click="refresh" />
    </div>
    <div v-if="feedback" class="admin-feedback" role="status">
      <q-icon name="check_circle_outline" size="20px" />
      <p>{{ feedback }}</p>
      <q-btn flat round dense icon="close" aria-label="Fermer le message" @click="feedback = ''" />
    </div>

    <div class="admin-metrics" aria-label="Résumé du suivi">
      <div class="admin-metric">
        <span>Calculs suivis</span>
        <strong>{{ loaded ? rows.length : '—' }}</strong>
        <p>Entraînements et explications</p>
      </div>
      <div class="admin-metric">
        <span>Suivis actifs</span>
        <strong>{{ loaded ? activeCount : '—' }}</strong>
        <p>État récupéré auprès du service</p>
      </div>
      <div class="admin-metric">
        <span>Suivis interrompus</span>
        <strong>{{ loaded ? interruptedCount : '—' }}</strong>
        <p>État conservé, sans actualisation</p>
      </div>
    </div>

    <section class="admin-panel" aria-labelledby="monitoring-list-title" :aria-busy="loading">
      <div class="admin-panel-header">
        <div>
          <h2 id="monitoring-list-title">Les calculs de DrIA</h2>
          <p>Le suivi permet de récupérer leur état et leurs résultats.</p>
        </div>
        <span v-if="lastUpdatedLabel" class="monitoring-updated">Actualisé à {{ lastUpdatedLabel }}</span>
      </div>

      <div class="admin-toolbar">
        <q-input v-model="search" class="admin-search" outlined dense clearable placeholder="Rechercher un identifiant" aria-label="Rechercher un calcul par son identifiant">
          <template #prepend><q-icon name="search" size="20px" /></template>
        </q-input>
        <q-select v-model="typeFilter" class="admin-filter" outlined dense emit-value map-options :options="typeOptions" aria-label="Filtrer par type de calcul" />
      </div>

      <q-table v-if="rows.length || loading" class="admin-table monitoring-table" flat :grid="$q.screen.lt.sm" :rows="filteredRows" :columns="columns" row-key="key" :loading="loading" :pagination="{ rowsPerPage: 10 }" :rows-per-page-options="[10, 25, 50]" rows-per-page-label="Calculs par page" no-data-label="Aucun calcul ne correspond à votre recherche.">
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
                <span>État du calcul</span>
                <span :class="['admin-badge', 'admin-badge--' + calculationState(slotProps.row.status).tone]">{{ calculationState(slotProps.row.status).label }}</span>
              </div>
              <div>
                <span>Suivi</span>
                <span :class="['admin-badge', slotProps.row.isPulling ? 'admin-badge--success' : 'admin-badge--neutral']">{{ slotProps.row.isPulling ? 'Actif' : 'Interrompu' }}</span>
              </div>
            </div>
            <div class="admin-mobile-actions">
              <q-btn flat no-caps color="primary" :label="slotProps.row.isPulling ? 'Arrêter le suivi' : 'Retirer du suivi'" :disable="loading || stopping" @click="openConfirmation(slotProps.row)" />
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
            <span :class="['admin-badge', slotProps.row.isPulling ? 'admin-badge--success' : 'admin-badge--neutral']">{{ slotProps.row.isPulling ? 'Actif' : 'Interrompu' }}</span>
          </q-td>
        </template>
        <template #body-cell-actions="slotProps">
          <q-td :props="slotProps">
            <div class="admin-row-actions">
              <q-btn flat no-caps color="primary" :label="slotProps.row.isPulling ? 'Arrêter le suivi' : 'Retirer du suivi'" :disable="loading || stopping" @click="openConfirmation(slotProps.row)" />
            </div>
          </q-td>
        </template>
        <template #no-data>
          <div v-if="!loading" class="admin-empty monitoring-filter-empty">
            <q-icon name="search_off" size="26px" />
            <h3>Aucun calcul trouvé</h3>
            <p>Modifiez l’identifiant recherché ou le type de calcul.</p>
            <q-btn flat no-caps color="primary" label="Effacer les filtres" @click="search = ''; typeFilter = 'all'" />
          </div>
        </template>
      </q-table>
      <div v-else-if="loaded" class="admin-empty">
        <q-icon name="task_alt" size="32px" />
        <h3>Aucun calcul suivi pour le moment</h3>
        <p>Les entraînements et les explications apparaissent ici lorsqu’ils sont suivis par DrIA.</p>
      </div>
    </section>

    <p class="monitoring-help"><q-icon name="info_outline" size="18px" />Arrêter un suivi retire sa connexion à DrIA. Cette action n’annule pas le calcul dans le service.</p>

    <q-dialog v-model="confirmationOpen" :persistent="stopping">
      <q-card class="admin-dialog project-workspace-colors" aria-labelledby="monitoring-confirmation-title">
        <q-card-section class="admin-dialog-heading">
          <div>
            <h2 id="monitoring-confirmation-title">{{ selectedConnection?.isPulling ? 'Arrêter ce suivi ?' : 'Retirer ce suivi ?' }}</h2>
            <p>DrIA ne récupérera plus les mises à jour ni les résultats de ce calcul. Le calcul lui-même ne sera pas annulé.</p>
          </div>
        </q-card-section>
        <q-card-section class="monitoring-dialog-body">
          <p v-if="matchingConnections.length > 1">Cet identifiant est partagé par {{ matchingConnections.length }} suivis. Ils seront tous retirés.</p>
          <div class="monitoring-confirmation-id"><span>Identifiant du calcul</span><strong>{{ selectedConnection?.jobWebServiceId }}</strong></div>
          <p v-if="stopError" class="admin-feedback admin-feedback--error" role="alert">{{ stopError }}</p>
        </q-card-section>
        <q-card-actions class="admin-dialog-actions" align="right">
          <q-btn flat no-caps label="Conserver le suivi" :disable="stopping" @click="confirmationOpen = false" />
          <q-btn unelevated no-caps color="primary" :label="selectedConnection?.isPulling ? 'Arrêter le suivi' : 'Retirer du suivi'" :loading="stopping" @click="stopMonitoring" />
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
