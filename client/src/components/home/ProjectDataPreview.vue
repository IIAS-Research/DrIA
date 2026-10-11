<script setup lang="ts">
import { computed, onMounted, onUnmounted, ref, watch } from 'vue';
import { ProjectDataset, ProjectColumnAnnotation, projectCsvScalar } from 'src/utils/project-data';

const props = withDefaults(defineProps<{
  dataset?: ProjectDataset;
  roles: Record<string, ProjectColumnAnnotation>;
  selectable?: boolean;
  activeColumns?: string[];
  groupColumns?: string[];
  selectionHint?: string;
  context?: 'creation' | 'project';
}>(), {
  selectable: false,
  activeColumns: () => [],
  groupColumns: () => [],
  selectionHint: '',
  context: 'creation',
});

const emit = defineEmits<{ (event: 'select-column', name: string): void }>();
const showExcluded = ref(true);
const page = ref(1);
const rowsPerPage = ref(10);
const previewHeading = ref<HTMLElement>();
const availableWidth = ref(900);
const minimumColumnWidth = ref(160);
const chosenColumns = ref<string[] | null>(null);
const columnSearch = ref('');
let resizeObserver: ResizeObserver | undefined;

const columns = computed(() => props.dataset?.columns ?? []);
const excludedColumns = computed(() => columns.value.filter((name) => isExcluded(name)));
const eligibleColumns = computed(() => columns.value.filter((name) => showExcluded.value || !isExcluded(name)));
const defaultColumnCount = computed(() => Math.max(1, Math.min(6, Math.floor((availableWidth.value - 54) / minimumColumnWidth.value))));
const requiredColumns = computed(() => {
  const roleOrder = ['target', 'event', 'time', 'group', 'identifier', 'unavailable'];
  const rank = (name: string) => {
    const position = roleOrder.indexOf(props.roles[name]?.role ?? '');
    return position < 0 ? roleOrder.length : position;
  };
  return eligibleColumns.value.filter((name) => rank(name) < roleOrder.length || props.activeColumns.includes(name))
    .sort((a, b) => rank(a) - rank(b));
});
const displayedColumns = computed(() => {
  const required = new Set(requiredColumns.value);
  const optional = eligibleColumns.value.filter((name) => !required.has(name));
  const selected = chosenColumns.value === null
    ? optional.slice(0, Math.max(0, defaultColumnCount.value - required.size))
    : optional.filter((name) => chosenColumns.value?.includes(name));
  // Put the study's landmarks first so they remain easy to find in a wide CSV.
  return [...requiredColumns.value, ...selected];
});
const filteredColumns = computed(() => {
  const search = (columnSearch.value ?? '').trim().toLocaleLowerCase('fr');
  return columns.value.filter((name) => `${name} ${props.roles[name]?.label ?? ''}`.toLocaleLowerCase('fr').includes(search));
});
const pageCount = computed(() => Math.max(1, Math.ceil((props.dataset?.rows.length ?? 0) / rowsPerPage.value)));
const displayedRows = computed(() => (props.dataset?.rows ?? []).slice((page.value - 1) * rowsPerPage.value, page.value * rowsPerPage.value));
const missingCells = computed(() => Object.values(props.dataset?.profiles ?? {}).reduce((count, profile) => count + profile.missingCount, 0));
const relatedGroups = computed(() => {
  const groups = new Map<string, number>();
  (props.dataset?.rows ?? []).forEach((row) => {
    const key = groupKey(row);
    if (key !== undefined) groups.set(key, (groups.get(key) ?? 0) + 1);
  });
  return groups;
});
const hasRelatedRows = computed(() => [...relatedGroups.value.values()].some((count) => count > 1));
const number = new Intl.NumberFormat('fr-FR');

watch(() => props.dataset, (dataset) => {
  page.value = 1;
  if (!dataset) showExcluded.value = true;
});
// Profiling replaces the preview object; keep the user's choices across that
// update and across TRAIN / TEST files containing the same named columns.
watch(() => JSON.stringify([...columns.value].sort()), () => {
  chosenColumns.value = null;
  columnSearch.value = '';
  showExcluded.value = true;
});
watch(rowsPerPage, () => { page.value = 1; });
watch(pageCount, (count) => { page.value = Math.min(page.value, count); });

function measureColumns() {
  if (previewHeading.value) availableWidth.value = previewHeading.value.clientWidth;
  minimumColumnWidth.value = window.matchMedia('(max-width: 599px)').matches ? 145 : 160;
}

function setColumnVisibility(name: string, visible: boolean) {
  if (requiredColumns.value.includes(name) || (!showExcluded.value && isExcluded(name))) return;
  const chosen = new Set(chosenColumns.value ?? displayedColumns.value.filter((column) => !requiredColumns.value.includes(column)));
  if (visible) chosen.add(name);
  else chosen.delete(name);
  chosenColumns.value = [...chosen];
}

onMounted(() => {
  measureColumns();
  if (typeof ResizeObserver !== 'undefined' && previewHeading.value) {
    resizeObserver = new ResizeObserver(measureColumns);
    resizeObserver.observe(previewHeading.value);
  } else window.addEventListener('resize', measureColumns);
});
onUnmounted(() => {
  resizeObserver?.disconnect();
  window.removeEventListener('resize', measureColumns);
});

function isExcluded(name: string): boolean {
  return ['identifier', 'unavailable'].includes(props.roles[name]?.role ?? '');
}

function annotationColor(name: string): string {
  const role = props.roles[name]?.role;
  if (role === 'group' || role === 'predictor') return 'secondary';
  if (isExcluded(name)) return 'grey-7';
  return 'accent';
}

function annotationIcon(name: string): string {
  return ({
    target: 'flag', event: 'flag', time: 'schedule', group: 'group',
    identifier: 'badge', unavailable: 'visibility_off', predictor: 'input',
  })[props.roles[name]?.role ?? 'predictor'];
}

function groupKey(row: Record<string, string>): string | undefined {
  if (!props.groupColumns.length || props.groupColumns.some((column) => !row[column]?.trim())) return undefined;
  return JSON.stringify(props.groupColumns.map((column) => {
    const profile = props.dataset?.profiles[column];
    return profile ? projectCsvScalar(row[column], profile) : row[column];
  }));
}

function relatedCount(row: Record<string, string>): number {
  const key = groupKey(row);
  return key === undefined ? 0 : relatedGroups.value.get(key) ?? 0;
}

function columnDescription(name: string): string {
  const profile = props.dataset?.profiles[name];
  if (!profile) return '';
  const type = { numeric: 'Nombre', boolean: 'Oui / non', text: 'Texte ou catégorie', empty: 'Colonne vide' }[profile.type];
  return profile.missingCount ? `${type} · ${number.format(profile.missingCount)} valeur${profile.missingCount > 1 ? 's' : ''} manquante${profile.missingCount > 1 ? 's' : ''}` : type;
}

function selectColumn(name: string) {
  if (props.selectable) emit('select-column', name);
}
</script>

<template>
  <q-card flat bordered class="project-preview">
    <div ref="previewHeading" class="preview-heading q-pa-md">
      <div class="row items-start justify-between q-col-gutter-sm">
        <div class="col">
          <div class="text-subtitle1 text-weight-medium">Vos données</div>
          <div v-if="dataset" class="text-caption text-grey-7">
            <template v-if="dataset.isComplete">
              {{ number.format(dataset.rowCount) }} observation{{ dataset.rowCount > 1 ? 's' : '' }}
              · {{ dataset.columns.length }} colonne{{ dataset.columns.length > 1 ? 's' : '' }}
            </template>
            <template v-else>Analyse du fichier en cours…</template>
          </div>
        </div>
        <div v-if="dataset" class="col-auto">
          <q-badge color="secondary" outline label="Aperçu" />
        </div>
      </div>
      <div v-if="dataset" class="text-caption text-grey-7 q-mt-sm">
        Les {{ dataset.rows.length }} premières observations sont affichées.
        <template v-if="context === 'project'">Les rôles indiqués sont ceux attribués dans ce projet.</template>
        <template v-else>Vos réponses précisent le rôle des colonnes.</template>
      </div>
      <div v-if="dataset" class="column-controls q-mt-sm">
        <q-btn-dropdown
          outline
          dense
          no-caps
          color="primary"
          icon="view_column"
          :label="`Colonnes : ${displayedColumns.length} / ${columns.length}`"
          :content-class="context === 'project' ? 'project-workspace-colors' : 'project-creation-colors'"
          @show="columnSearch = ''"
        >
          <div class="column-menu q-pa-md">
            <div class="text-subtitle2">Colonnes affichées</div>
            <p class="text-caption text-grey-7 q-mt-xs q-mb-sm">Masquer une colonne ici ne l’exclut pas de l’étude.</p>
            <div class="row q-gutter-x-xs q-mb-sm">
              <q-btn flat dense no-caps color="primary" label="Vue automatique" @click="chosenColumns = null" />
              <q-btn flat dense no-caps color="primary" label="Tout afficher" @click="chosenColumns = [...columns]" />
            </div>
            <q-input v-model="columnSearch" dense outlined clearable label="Rechercher une colonne" class="q-mb-sm" />
            <div class="column-list" role="group" aria-label="Colonnes de l’aperçu">
              <q-checkbox
                v-for="name in filteredColumns"
                :key="name"
                :model-value="displayedColumns.includes(name)"
                :disable="requiredColumns.includes(name) || (!showExcluded && isExcluded(name))"
                dense
                color="primary"
                class="column-option"
                @update:model-value="setColumnVisibility(name, $event)"
              >
                <div class="column-option-name">{{ name }}</div>
                <div v-if="requiredColumns.includes(name) || (!showExcluded && isExcluded(name))" class="text-caption text-grey-7">
                  <q-icon :name="!showExcluded && isExcluded(name) ? 'visibility_off' : 'lock'" size="12px" aria-hidden="true" />
                  {{ roles[name]?.label }} · {{ !showExcluded && isExcluded(name) ? 'masquée' : 'toujours visible' }}
                </div>
              </q-checkbox>
              <div v-if="!filteredColumns.length" class="text-caption text-grey-7 q-py-sm">Aucune colonne ne correspond à votre recherche.</div>
            </div>
          </div>
        </q-btn-dropdown>
        <span v-if="columns.length > displayedColumns.length" class="text-caption text-grey-7">
          {{ columns.length - displayedColumns.length }} autre{{ columns.length - displayedColumns.length > 1 ? 's' : '' }} colonne{{ columns.length - displayedColumns.length > 1 ? 's' : '' }} disponible{{ columns.length - displayedColumns.length > 1 ? 's' : '' }}
        </span>
      </div>
      <div v-if="selectable && selectionHint" class="selection-hint q-mt-sm" role="status">
        <q-icon name="touch_app" size="18px" class="q-mr-xs" />{{ selectionHint }}
      </div>
      <q-toggle
        v-if="excludedColumns.length"
        v-model="showExcluded"
        color="primary"
        dense
        class="q-mt-sm text-caption"
        :label="`Afficher les ${excludedColumns.length} colonne${excludedColumns.length > 1 ? 's' : ''} exclue${excludedColumns.length > 1 ? 's' : ''}`"
      />
    </div>

    <template v-if="dataset">
      <div class="preview-scroll" tabindex="0" role="region" aria-label="Aperçu du fichier, défilement horizontal possible">
        <table class="preview-table">
          <caption class="sr-only">Aperçu des données importées et rôle de chaque colonne dans l’étude</caption>
          <thead>
            <tr>
              <th class="observation-number" scope="col">N°</th>
              <th
                v-for="name in displayedColumns"
                :key="name"
                scope="col"
                :class="{
                  'excluded-column': isExcluded(name),
                  'outcome-column': ['target', 'event', 'time'].includes(roles[name]?.role ?? ''),
                  'group-column': roles[name]?.role === 'group',
                  'active-column': activeColumns.includes(name),
                }"
              >
                <button
                  type="button"
                  class="column-header"
                  :class="{ 'selectable-header': selectable }"
                  :disabled="!selectable"
                  :aria-label="selectable ? `Sélectionner la colonne ${name}` : name"
                  :aria-pressed="selectable ? activeColumns.includes(name) : undefined"
                  @click="selectColumn(name)"
                >
                  <span class="column-name">{{ name }}</span>
                  <q-icon v-if="selectable" :name="activeColumns.includes(name) ? 'check_circle' : 'add_circle_outline'" size="18px" />
                </button>
                <q-badge
                  v-if="roles[name]"
                  :color="annotationColor(name)"
                  :text-color="annotationColor(name) === 'accent' ? 'dark' : 'white'"
                  :aria-label="[roles[name].label, roles[name].reason].filter(Boolean).join(' : ')"
                  class="column-role"
                >
                  <q-icon :name="annotationIcon(name)" size="14px" class="q-mr-xs" />{{ roles[name].label }}
                  <q-tooltip v-if="roles[name].reason">{{ roles[name].reason }}</q-tooltip>
                </q-badge>
                <div class="column-profile">{{ columnDescription(name) }}</div>
              </th>
            </tr>
          </thead>
          <tbody>
            <tr v-for="(row, rowIndex) in displayedRows" :key="(page - 1) * rowsPerPage + rowIndex" :class="{ 'related-observation': relatedCount(row) > 1 }">
              <th class="observation-number" scope="row">
                {{ (page - 1) * rowsPerPage + rowIndex + 1 }}
                <q-icon v-if="relatedCount(row) > 1" name="link" color="secondary" size="16px" class="related-icon">
                  <q-tooltip>Cette observation et {{ relatedCount(row) - 1 }} autre{{ relatedCount(row) > 2 ? 's' : '' }} partagent le même regroupement dans cet aperçu.</q-tooltip>
                </q-icon>
              </th>
              <td
                v-for="name in displayedColumns"
                :key="name"
                :class="{
                  'excluded-column': isExcluded(name),
                  'outcome-column': ['target', 'event', 'time'].includes(roles[name]?.role ?? ''),
                  'group-column': roles[name]?.role === 'group',
                  'active-column': activeColumns.includes(name),
                  'missing-cell': !row[name]?.trim(),
                }"
              >
                <span v-if="row[name]?.trim()" class="cell-value" :title="row[name]">{{ row[name] }}</span>
                <span v-else class="missing-value" aria-label="Valeur manquante">
                  —
                  <q-tooltip>Valeur manquante</q-tooltip>
                </span>
              </td>
            </tr>
          </tbody>
        </table>
      </div>

      <div class="preview-footer q-pa-sm">
        <div class="row items-center justify-between q-col-gutter-sm">
          <div class="col-auto row items-center">
            <label for="preview-row-count" class="text-caption text-grey-7 q-mr-sm">Lignes par page</label>
            <select id="preview-row-count" v-model.number="rowsPerPage" class="row-count-select">
              <option :value="10">10</option>
              <option :value="20">20</option>
              <option :value="50">50</option>
            </select>
          </div>
          <q-pagination v-model="page" :max="pageCount" :max-pages="4" color="primary" size="sm" direction-links boundary-numbers />
        </div>
        <div v-if="missingCells" class="text-caption text-grey-7 q-mt-sm">
          <span class="missing-key" aria-hidden="true" />{{ number.format(missingCells) }} cellule{{ missingCells > 1 ? 's' : '' }} vide{{ missingCells > 1 ? 's' : '' }} {{ dataset.isComplete ? 'dans le fichier' : 'dans les lignes analysées' }}.
        </div>
        <div v-if="hasRelatedRows" class="text-caption text-grey-7 q-mt-xs">
          <q-icon name="link" color="secondary" size="16px" /> Les lignes marquées sont liées dans cet aperçu. Le regroupement de l’étude s’applique au fichier entier.
        </div>
      </div>
    </template>
    <div v-else class="empty-preview q-pa-xl text-center">
      <q-icon name="table_chart" color="secondary" size="44px" />
      <div class="text-subtitle2 q-mt-md">Votre tableau apparaîtra ici</div>
      <p class="text-caption text-grey-7 q-mt-sm q-mb-none">Importez vos données pour les explorer et préparer votre étude, étape par étape.</p>
    </div>
  </q-card>
</template>

<style scoped lang="scss">
.project-preview {
  width: 100%;
  min-width: 0;
  max-width: 100%;
  overflow: hidden;
  border-radius: 8px;
  background: var(--project-surface);
}

.preview-heading { border-bottom: 1px solid var(--project-border); }
.column-controls { display: flex; align-items: center; flex-wrap: wrap; gap: 8px 12px; }
.column-menu { width: 340px; max-width: calc(100vw - 24px); }
.column-list { display: flex; flex-direction: column; max-height: 300px; overflow-y: auto; }
.column-option { padding: 8px 2px; min-width: 0; }
.column-option :deep(.q-checkbox__label) { min-width: 0; }
.column-option-name { overflow-wrap: anywhere; font-size: 13px; }
.selection-hint { color: var(--q-primary); font-size: 13px; line-height: 1.5; }
.preview-scroll { max-height: 520px; overflow: auto; overscroll-behavior: contain; }
.preview-scroll:focus-visible { outline: 2px solid var(--q-primary); outline-offset: -2px; }
.preview-table { border-spacing: 0; width: 100%; font-size: 13px; }
.preview-table th,
.preview-table td { padding: 10px 12px; text-align: left; border-bottom: 1px solid var(--project-border); border-right: 1px solid var(--project-border); }
.preview-table thead th { position: sticky; top: 0; z-index: 1; min-width: 160px; max-width: 260px; vertical-align: top; background: var(--project-paper); }
.preview-table .observation-number { min-width: 54px; width: 54px; color: var(--project-muted); font-size: 12px; font-weight: 400; background: var(--project-paper); white-space: nowrap; }
.preview-table thead .observation-number { vertical-align: middle; }
.column-header { display: flex; align-items: center; justify-content: space-between; gap: 8px; width: 100%; padding: 0; border: 0; font: inherit; font-weight: 600; text-align: left; background: transparent; color: var(--project-ink); }
.column-header:disabled { opacity: 1; }
.selectable-header { cursor: pointer; color: var(--q-primary); }
.selectable-header:focus-visible { outline: 2px solid var(--q-primary); outline-offset: 4px; border-radius: 2px; }
.column-name { min-width: 0; overflow-wrap: anywhere; }
.column-role { max-width: 240px; margin-top: 7px; white-space: normal; line-height: 1.3; text-align: left; }
.column-profile { margin-top: 7px; color: var(--project-muted); font-size: 11px; font-weight: 400; line-height: 1.4; }
.cell-value { display: block; max-width: 240px; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
.preview-table .outcome-column { background: var(--project-selection-surface); box-shadow: inset 2px 0 var(--project-brand); }
.preview-table .group-column { background: var(--project-paper); }
.preview-table .active-column { background: var(--project-selection-surface); }
.preview-table thead .active-column { box-shadow: inset 0 3px 0 var(--q-accent); }
.preview-table .excluded-column { color: var(--project-muted); background: var(--project-paper); }
.excluded-column .column-header { color: var(--project-muted); }
.preview-table .missing-cell { background: #fff8e5; }
.missing-value { color: #98751d; }
.preview-table .related-observation .observation-number { box-shadow: inset 3px 0 0 var(--q-secondary); }
.related-icon { margin-left: 5px; }
.preview-footer { border-top: 1px solid var(--project-border); }
.row-count-select { padding: 3px 5px; background: var(--project-surface); border: 1px solid var(--project-border); border-radius: 4px; color: var(--project-ink); }
.missing-key { display: inline-block; width: 10px; height: 10px; margin-right: 5px; border: 1px solid #eedb9a; background: #fff8e5; }
.empty-preview { min-height: 280px; display: flex; flex-direction: column; align-items: center; justify-content: center; }
.sr-only { position: absolute; width: 1px; height: 1px; padding: 0; margin: -1px; overflow: hidden; clip: rect(0, 0, 0, 0); white-space: nowrap; border: 0; }

@media (max-width: 599px) {
  .preview-scroll { max-height: 340px; }
  .preview-table th,
  .preview-table td { padding: 8px 10px; }
  .preview-table thead th { min-width: 145px; }
}
</style>
