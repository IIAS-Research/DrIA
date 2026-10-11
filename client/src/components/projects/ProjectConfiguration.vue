<script setup lang="ts">
import { computed, ref, watch } from 'vue';
import { copyToClipboard, exportFile } from 'quasar';
import { IProject } from '../models';
import { formatToDDMMYYYHHmm } from 'src/utils/date';
import { metricKey } from 'src/utils/iaml';
import { formatBudget } from 'src/utils/project-results';
import ProjectAnalysisList from './ProjectAnalysisList.vue';
import PipelineOverview from '../pipeline/PipelineOverview.vue';

const props = defineProps<{ project: IProject; processId?: number }>();
const emit = defineEmits<{ (e: 'restart', processId: number): void }>();
const selectedId = ref<number | null>(null);
const feedback = ref('');
const copied = ref(false);
const processes = computed(() => [...(props.project.processes ?? [])].sort((a, b) =>
  (new Date(b.date).getTime() || 0) - (new Date(a.date).getTime() || 0) || b.id - a.id));
const selected = computed(() => processes.value.find((process) => process.id === selectedId.value));
const study = computed(() => selected.value?.pipeline);
const choices = computed(() => processes.value.map((process) => ({
  value: process.id,
  label: `Entraînement du ${formatToDDMMYYYHHmm(process.date)} · ${formatBudget(process.maxDuration)}`,
})));
const active = computed(() => processes.value.some((process) => ['QUEUED', 'RUNNING'].includes(process.status)));
const fields: { key: 'pipeline' | 'metrics' | 'statistics' | 'explanations'; label: string; description: string; icon: string }[] = [
  { key: 'pipeline', label: 'Prétraitements et modèles', description: 'Les étapes, articulations et alternatives proposées à DrIA.', icon: 'account_tree' },
  { key: 'metrics', label: 'Mesure des performances', description: 'Les indicateurs configurés pour évaluer les modèles.', icon: 'straighten' },
  { key: 'statistics', label: 'Statistiques descriptives', description: 'Les analyses des données configurées pour cette étude.', icon: 'query_stats' },
  { key: 'explanations', label: 'Analyses et explicabilité', description: 'Les composants prévus pour explorer le comportement des modèles.', icon: 'lightbulb_outline' },
];
const optionSections: { key: 'options' | 'fit_options'; label: string }[] = [
  { key: 'options', label: 'Recherche' }, { key: 'fit_options', label: 'Entraînement final' },
];
const configurationJson = computed(() => study.value ? JSON.stringify(study.value, null, 2) : '');
const objective = computed(() => {
  const process = selected.value;
  if (!process) return 'Non renseigné';
  const key = process.objective?.key ?? process.statusSnapshot?.objective?.key
    ?? process.pipeline?.options?.main_metric;
  const metrics = process.models?.map((model) => model.metrics)
    .find((value) => key ? metricKey(value) === key : Boolean(metricKey(value)));
  const name = key ? metrics?.metrics_info?.[key]?.name ?? String(key)
    : metrics ? metrics.metrics_info?.[metricKey(metrics)]?.name ?? metricKey(metrics) : 'Choix automatique par DrIA';
  const greater = process.objective?.greater_is_better ?? process.statusSnapshot?.objective?.greater_is_better
    ?? metrics?.objective?.greater_is_better;
  return `${name}${greater === undefined ? '' : greater ? ' · à maximiser' : ' · à minimiser'}`;
});
const validation = computed(() => {
  const splitter = study.value?.options?.splitter;
  const id = typeof splitter === 'string' ? splitter : splitter?.id;
  const labels: Record<string, string> = {
    kfold: 'Validation croisée', stratified_kfold: 'Validation croisée stratifiée',
    group_kfold: 'Validation croisée par groupes', stratified_group_kfold: 'Validation croisée stratifiée par groupes',
    leave_one_group_out: 'Un groupe laissé de côté à chaque passage',
    train_test: 'Partition d’apprentissage et de validation', holdout: 'Partition d’apprentissage et de validation',
  };
  return id ? labels[id] ?? String(id) : 'Non renseignée dans cette configuration';
});
const testLabel = computed(() => ({
  split: '20 % des données réservées au test final',
  external: 'Fichier indépendant pour le test final',
  cross_validation: 'Aucun jeu de test séparé',
})[props.project.evaluationMode ?? ''] ?? 'Mode de test final non renseigné');
const optionLabels: Record<string, string> = {
  max_duration: 'Budget de recherche (secondes)', max_stage_duration: 'Budget maximal par étape (secondes)',
  max_workers: 'Calculs parallèles', main_metric: 'Clé du critère principal',
  n_candidates: 'Nombre de modèles conservés', patience: 'Patience',
  splitter: 'Méthode de validation', optimizer: 'Méthode de recherche',
  train_on_n_samples: 'Nombre de lignes pour la recherche', refit_on_sample: 'Réentraînement sur l’échantillon',
  keep_training_history: 'Conservation de l’historique de recherche', preprocessor: 'Prétraitement global',
  verbose: 'Niveau de détail des journaux', time_before_sample_use: 'Délai avant l’échantillonnage',
};

function display(value: unknown): string {
  if (value === null || value === undefined) return 'Non renseigné';
  if (typeof value === 'boolean') return value ? 'Oui' : 'Non';
  return typeof value === 'object' ? JSON.stringify(value, null, 2) : String(value);
}

function entries(key: 'fit_options' | 'options'): [string, unknown][] {
  return Object.entries(study.value?.[key] ?? {});
}

async function copyConfiguration() {
  try {
    await copyToClipboard(configurationJson.value);
    copied.value = true;
    feedback.value = 'Configuration copiée.';
  } catch {
    copied.value = false;
    feedback.value = 'La copie est indisponible dans ce navigateur. Vous pouvez télécharger la configuration.';
  }
}

function downloadConfiguration() {
  const result = exportFile(`projet-${props.project.id}-entrainement-${selected.value?.id}.json`, configurationJson.value, 'application/json');
  feedback.value = result === true ? 'Configuration téléchargée.' : 'Le navigateur n’a pas autorisé le téléchargement.';
}

watch([() => props.processId, () => props.project.id], () => {
  selectedId.value = processes.value.some((process) => process.id === props.processId)
    ? props.processId! : processes.value[0]?.id ?? null;
}, { immediate: true });
watch(processes, () => {
  if (!processes.value.some((process) => process.id === selectedId.value)) selectedId.value = processes.value[0]?.id ?? null;
});
watch(selectedId, () => { feedback.value = ''; copied.value = false; });
</script>

<template>
  <section class="project-configuration" aria-label="Configuration des entraînements">
    <div class="section-heading">
      <div>
        <h2>La configuration de l’étude</h2>
        <p>Chaque entraînement conserve ses réglages. Consultez-les ici ou adaptez-les pour une nouvelle recherche.</p>
      </div>
    </div>

    <div v-if="!processes.length" class="configuration-empty">
      <q-icon name="account_tree" size="30px" />
      <h3>Aucune configuration enregistrée</h3>
      <p>La configuration apparaîtra avec le premier entraînement du projet.</p>
    </div>

    <template v-else>
      <div class="configuration-toolbar">
        <q-select
          v-model="selectedId"
          :options="choices"
          emit-value
          map-options
          outlined
          label="Configuration de l’entraînement"
          class="configuration-selector"
          popup-content-class="project-workspace-colors"
        />
        <q-btn
          v-if="selected && study"
          unelevated
          no-caps
          color="primary"
          label="Adapter cette configuration"
          icon="tune"
          :to="{ name: 'pipeline-fork', params: { id: project.id, processId: selected.id } }"
        />
      </div>

      <template v-if="selected && study">
        <dl class="configuration-summary">
          <div><dt>Temps de recherche</dt><dd>{{ formatBudget(selected.maxDuration) }}</dd></div>
          <div><dt>Critère principal</dt><dd>{{ objective }}</dd></div>
          <div><dt>Validation interne</dt><dd>{{ validation }}</dd><small>{{ testLabel }}</small></div>
        </dl>
        <p class="configuration-note">
          <q-icon name="lock_outline" size="16px" />
          Configuration enregistrée, consultée en lecture seule. Le budget couvre la recherche ; l’évaluation finale peut se poursuivre ensuite.
        </p>

        <div class="configuration-sections">
          <q-expansion-item
            v-for="field in fields"
            :key="`${selected.id}:${field.key}`"
            :label="field.label"
            :caption="field.description"
            :icon="field.icon"
            :default-opened="field.key === 'pipeline'"
            class="configuration-section"
          >
            <div class="configuration-section-content">
              <template v-if="field.key === 'pipeline'">
                <p class="configuration-search-note">Le schéma présente l’ensemble des étapes et des alternatives proposées à DrIA, dans leur articulation d’origine. Le budget de recherche ne garantit pas que chaque possibilité a été testée.</p>
                <PipelineOverview v-if="study.pipeline" :node="study.pipeline" />
                <p v-else class="not-configured">Aucun parcours de recherche n’est enregistré pour cet entraînement.</p>
              </template>
              <ProjectAnalysisList v-else-if="study[field.key]" :node="study[field.key]!" />
              <p v-else class="not-configured">Aucun composant enregistré pour cette partie de l’étude.</p>
            </div>
          </q-expansion-item>

          <q-expansion-item
            :key="`${selected.id}:options`"
            label="Options de recherche et d’entraînement"
            caption="Budgets, méthode de recherche, validation et nombre de candidats."
            icon="settings"
            class="configuration-section"
          >
            <div class="configuration-section-content option-sections">
              <section v-for="section in optionSections" :key="section.key">
                <h3>{{ section.label }}</h3>
                <dl v-if="entries(section.key).length" class="option-list">
                  <div v-for="[key, value] in entries(section.key)" :key="key">
                    <dt>{{ optionLabels[key] ?? key }}<small v-if="optionLabels[key]">{{ key }}</small></dt>
                    <dd>{{ display(value) }}</dd>
                  </div>
                </dl>
                <p v-else class="not-configured">Aucune option enregistrée.</p>
              </section>
            </div>
          </q-expansion-item>

          <q-expansion-item
            :key="`${selected.id}:json`"
            label="Configuration complète · JSON"
            caption="Tous les paramètres tels qu’ils ont été transmis au service DrIA."
            icon="data_object"
            class="configuration-section"
          >
            <div class="configuration-section-content">
              <div class="json-actions">
                <q-btn flat no-caps :icon="copied ? 'check' : 'content_copy'" label="Copier" @click="copyConfiguration" />
                <q-btn flat no-caps icon="download" label="Télécharger" @click="downloadConfiguration" />
                <span v-if="feedback" role="status">{{ feedback }}</span>
              </div>
              <pre class="configuration-json" tabindex="0" aria-label="Configuration complète au format JSON">{{ configurationJson }}</pre>
            </div>
          </q-expansion-item>
        </div>

        <div class="configuration-restart">
          <div><strong>Poursuivre avec ces réglages</strong><p>Choisissez un nouveau budget de recherche en conservant cette configuration.</p></div>
          <q-btn outline no-caps color="primary" label="Reprendre cette recherche" icon="refresh" :disable="active" @click="emit('restart', selected.id)" />
        </div>
      </template>
      <p v-else class="not-configured">La configuration de cet entraînement n’est pas disponible.</p>
    </template>
  </section>
</template>

<style scoped>
.project-configuration { color: var(--project-ink); min-width: 0; }
.section-heading { margin-bottom: 24px; }
h2 { margin: 0 0 8px; font-size: 22px; font-weight: 600; line-height: 1.3; letter-spacing: -.3px; }
.section-heading p { margin: 0; max-width: 700px; color: var(--project-muted); line-height: 1.7; }
.configuration-toolbar { display: flex; align-items: center; justify-content: space-between; gap: 20px; flex-wrap: wrap; margin-bottom: 24px; }
.configuration-selector { flex: 1 1 340px; max-width: 570px; min-width: 0; }
.configuration-toolbar > .q-btn { max-width: 100%; min-height: 44px; }
.configuration-summary { display: grid; grid-template-columns: minmax(0, .9fr) minmax(0, 1.1fr) minmax(0, 1.5fr); margin: 0; border: 1px solid var(--project-border); background: var(--project-surface); border-radius: 12px; overflow: hidden; }
.configuration-summary > div { padding: 20px 22px; min-width: 0; }
.configuration-summary > div + div { border-left: 1px solid var(--project-border); }
.configuration-summary dt { margin-bottom: 8px; color: var(--project-muted); font-size: 12px; }
.configuration-summary dd { margin: 0; font-weight: 600; line-height: 1.6; overflow-wrap: anywhere; }
.configuration-summary small { display: block; color: var(--project-muted); line-height: 1.6; margin-top: 4px; }
.configuration-note { display: flex; align-items: baseline; gap: 8px; color: var(--project-muted); line-height: 1.7; font-size: 12px; margin: 14px 2px 24px; }
.configuration-note .q-icon { flex-shrink: 0; }
.configuration-sections { display: grid; gap: 12px; }
.configuration-section { border: 1px solid var(--project-border); border-radius: 12px; background: var(--project-surface); min-width: 0; }
.configuration-section > :deep(.q-expansion-item__container > .q-item) { padding: 18px 22px; border-radius: 12px; }
.configuration-section :deep(.q-item__section--main) { min-width: 0; }
.configuration-section :deep(.q-item__label) { white-space: normal; overflow-wrap: anywhere; }
.configuration-section :deep(.q-item__label--caption) { color: var(--project-muted); line-height: 1.5; margin-top: 6px; }
.configuration-section :deep(.q-item__section--side) { color: var(--q-primary); }
.configuration-section-content { padding: 0 22px 20px; min-width: 0; }
.configuration-search-note { margin: 0 0 22px; font-size: 13px; line-height: 1.7; color: var(--project-muted); }
.not-configured { margin: 0; padding: 12px 0; color: var(--project-muted); line-height: 1.7; }
.option-sections { display: grid; gap: 24px; }
.option-sections h3 { font-size: 14px; margin: 8px 0 16px; font-weight: 600; }
.option-list { margin: 0; }
.option-list > div { display: grid; grid-template-columns: minmax(0, 1fr) minmax(0, 1.4fr); gap: 16px; padding: 12px 0; border-top: 1px solid var(--project-border); }
.option-list dt { font-size: 13px; line-height: 1.6; overflow-wrap: anywhere; }
.option-list dt small { display: block; font: 11px/1.6 monospace; color: var(--project-muted); }
.option-list dd { margin: 0; font-size: 13px; line-height: 1.6; white-space: pre-wrap; overflow-wrap: anywhere; }
.json-actions { display: flex; flex-wrap: wrap; gap: 8px; align-items: center; color: var(--q-primary); margin-bottom: 12px; }
.json-actions span { color: var(--project-muted); font-size: 12px; }
.configuration-json { margin: 0; padding: 18px; background: var(--project-paper); border-radius: 8px; border: 1px solid var(--project-border); max-height: 460px; overflow: auto; font: 12px/1.7 monospace; max-width: 100%; }
.configuration-restart { display: flex; align-items: center; justify-content: space-between; flex-wrap: wrap; gap: 16px; margin-top: 28px; padding: 22px; background: var(--project-tint); border-radius: 12px; }
.configuration-restart strong { font-size: 14px; }
.configuration-restart p { margin: 5px 0 0; color: var(--project-muted); line-height: 1.6; font-size: 13px; }
.configuration-restart .q-btn { min-height: 42px; max-width: 100%; }
.configuration-empty { padding: 48px 24px; text-align: center; background: var(--project-surface); border: 1px solid var(--project-border); border-radius: 12px; }
.configuration-empty .q-icon { color: var(--q-primary); }
.configuration-empty h3 { margin: 16px 0 8px; font-size: 18px; }
.configuration-empty p { margin: 0; color: var(--project-muted); }
@media (max-width: 799px) {
  .configuration-summary { grid-template-columns: minmax(0, 1fr); }
  .configuration-summary > div + div { border-left: 0; border-top: 1px solid var(--project-border); }
  .configuration-summary > div { padding: 16px 18px; }
}
@media (max-width: 599px) {
  h2 { font-size: 20px; }
  .configuration-toolbar { gap: 14px; }
  .configuration-selector { flex-basis: 100%; max-width: none; }
  .configuration-toolbar > .q-btn { width: 100%; }
  .configuration-section > :deep(.q-expansion-item__container > .q-item) { padding: 16px 14px; }
  .configuration-section-content { padding: 0 12px 16px; }
  .configuration-section :deep(.q-item__section--avatar) { min-width: 36px; padding-right: 10px; }
  .option-list > div { grid-template-columns: minmax(0, 1fr); gap: 5px; }
  .configuration-restart { padding: 18px; }
  .configuration-restart .q-btn { width: 100%; }
}
</style>
