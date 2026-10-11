<script setup lang="ts">
import { computed, ref, watch } from 'vue';
import type { IPipeline, IRecipe } from '../models';
import { recipeAt, recipeEntries, recipeLabel, recipeSummary, type RecipePath } from 'src/utils/pipeline-editor';
import { displayServiceName } from 'src/utils/service-label';
import RecipeFlow from './RecipeFlow.vue';

const props = withDefaults(defineProps<{ node: IRecipe; context?: 'search' | 'retained' }>(), { context: 'search' });
const retained = computed(() => props.context === 'retained');
const study = computed<IPipeline>(() => ({ schema_version: 1, pipeline: props.node }));
const selectedPath = ref<RecipePath>(['pipeline']);
const search = ref('');
const selected = computed(() => recipeAt(study.value, selectedPath.value) ?? props.node);
const entries = computed(() => recipeEntries(selected.value, selectedPath.value));
const included = computed(() => entries.value.filter((entry) => !entry.removed).length);
const filteredEntries = computed(() => {
  const query = normalize(search.value);
  return entries.value.filter((entry) => normalize(`${recipeLabel(entry.node)} ${entry.node.component ?? ''}`).includes(query));
});
const parameters = computed(() => Object.entries(selected.value.params ?? {}));
const mode = computed(() => selected.value.type === 'family' ? selected.value.mode ?? 'choice' : selected.value.type);
const relation = computed(() => ({
  sequence: 'Étapes dans l’ordre', choice: 'Alternatives incluses', adaptive: 'Étapes adaptées aux données',
  optional: 'Étape facultative', collection: 'Composants de l’ensemble',
}[mode.value] ?? 'Composants de cette étape'));
const explanation = computed(() => retained.value
  ? typeof selected.value.description === 'string' && selected.value.description
    ? selected.value.description
    : mode.value === 'sequence'
      ? 'Ces étapes sont celles enregistrées dans le pipeline du modèle, dans leur ordre d’exécution.'
      : 'Ce composant fait partie du modèle affiché. Les paramètres ci-dessous sont les valeurs utilisées par ce modèle.'
  : ({
  sequence: 'Les étapes sont exécutées dans cet ordre. Chacune reçoit les données préparées par la précédente.',
  choice: 'DrIA compare ces alternatives. Pour chaque essai, une seule alternative est retenue à cet endroit du pipeline.',
  adaptive: 'DrIA applique les étapes compatibles avec les données, dans l’ordre enregistré. Certaines peuvent être ignorées si elles ne sont pas utiles.',
  optional: 'Cette étape peut être appliquée ou ignorée pendant la recherche.',
  collection: 'Les composants inclus sont exécutés pour cette partie de l’étude.',
  use: 'Ce composant réalise une étape de préparation des données ou d’apprentissage du modèle.',
}[mode.value] ?? 'Ce groupe rassemble les composants enregistrés pour cette étape.'));
const parent = computed(() => {
  for (let length = selectedPath.value.length - 1; length >= 1; length -= 1) {
    const path = selectedPath.value.slice(0, length);
    const node = recipeAt(study.value, path);
    if (node) return { node, path };
  }
  return undefined;
});
const selectedEntry = computed(() => parent.value
  ? recipeEntries(parent.value.node, parent.value.path).find((entry) => samePath(entry.path, selectedPath.value))
  : undefined);
const flowSelection = computed(() => {
  const memberIndex = selectedPath.value.indexOf('members');
  return memberIndex < 0 ? selectedPath.value : selectedPath.value.slice(0, memberIndex);
});
const parameterLabels: Record<string, string> = {
  n_estimators: 'Nombre d’arbres', iterations: 'Nombre d’itérations', max_iter: 'Itérations maximales',
  learning_rate: 'Vitesse d’apprentissage', max_depth: 'Profondeur maximale', depth: 'Profondeur',
  min_samples_split: 'Minimum pour une séparation', min_samples_leaf: 'Minimum par feuille',
  min_data_in_leaf: 'Minimum par feuille', n_neighbors: 'Nombre de voisins',
  k_neighbors: 'Voisins pour le rééquilibrage', m_neighbors: 'Voisins pour détecter les frontières',
  n_components: 'Nombre de composantes', max_features: 'Variables utilisées', max_samples: 'Observations utilisées',
  random_state: 'Graine aléatoire', strategy: 'Méthode de remplacement', fill_value: 'Valeur de remplacement',
  sampling_strategy: 'Méthode de rééquilibrage', class_weight: 'Pondération des classes',
  criterion: 'Critère de séparation', loss: 'Fonction d’erreur', loss_function: 'Fonction d’erreur',
  penalty: 'Régularisation', solver: 'Méthode de résolution', C: 'Intensité inverse de régularisation',
  alpha: 'Intensité de régularisation', tol: 'Tolérance de convergence',
  bootstrap: 'Échantillonnage avec remise', fit_intercept: 'Calcul d’une constante',
  pos_label: 'Classe positive', nsamples: 'Échantillons d’explication',
  unknown_value: 'Code d’une catégorie inconnue', start_value: 'Premier code des catégories',
  sort_categories: 'Tri des catégories',
};

function samePath(left: RecipePath, right: RecipePath): boolean { return JSON.stringify(left) === JSON.stringify(right); }
function normalize(value: string | null | undefined): string { return (value ?? '').normalize('NFD').replace(/[\u0300-\u036f]/g, '').toLowerCase().trim(); }
function select(path: RecipePath): void { selectedPath.value = path; search.value = ''; }
function condition(value: unknown): string {
  const labels: Record<string, string> = {
    missing_values: 'Présence de valeurs manquantes', categorical_columns: 'Présence de variables catégorielles',
    numerical_columns: 'Présence de variables numériques', imbalanced_classes: 'Catégories déséquilibrées',
  };
  return labels[String(value)] ?? (typeof value === 'string' ? displayServiceName(value) : display(value));
}
function display(value: unknown): string {
  if (value === undefined) return 'Non renseigné';
  if (value === null) return 'Aucune valeur (null)';
  if (typeof value === 'boolean') return value ? 'Oui' : 'Non';
  return typeof value === 'object' ? JSON.stringify(value) : String(value);
}
function parameterValue(parameter: any): unknown {
  if (parameter && typeof parameter === 'object' && !Array.isArray(parameter)) {
    if (['parameter', 'const'].includes(parameter.type)) return parameter.value;
    if (['int', 'float', 'categorical'].includes(parameter.type)) return parameter.initial;
  }
  return parameter;
}
function isSearch(parameter: any): boolean {
  if (parameter?.fixed === true || parameter?.type === 'const' || parameter?.optimizable === false) return false;
  return ['int', 'float', 'categorical'].includes(parameter?.type)
    || parameter?.type === 'parameter' && (Array.isArray(parameter.domain) || Array.isArray(parameter.categorical));
}
function parameterScope(parameter: any): string {
  if (!isSearch(parameter)) return 'Valeur fixe';
  const values = parameter.categorical ?? (parameter.type === 'categorical' ? parameter.values : undefined);
  if (Array.isArray(values) && values.length) return `Valeurs explorées : ${values.map(display).join(' · ')}`;
  const bounds = parameter.domain ?? (['int', 'float'].includes(parameter.type) ? [parameter.low, parameter.high] : undefined);
  if (Array.isArray(bounds) && bounds.length === 2) return `Recherche de ${display(bounds[0])} à ${display(bounds[1])}`;
  return 'Ajusté pendant la recherche';
}
watch(() => props.node, () => { select(['pipeline']); }, { immediate: true });
</script>

<template>
  <section class="pipeline-overview" :aria-label="retained ? 'Pipeline du modèle' : 'Possibilités de la recherche'">
    <div class="pipeline-overview__columns">
      <section class="pipeline-map">
        <div class="pipeline-map__heading"><h3>{{ retained ? recipeLabel(node) : 'Pipeline de la recherche' }}</h3><span>{{ retained ? 'Étapes du modèle' : 'Ordre et alternatives' }}</span></div>
        <p class="pipeline-map__hint">Sélectionnez une étape pour comprendre son rôle et voir sa configuration.</p>
        <RecipeFlow :node="node" :path="['pipeline']" :selected-path="flowSelection" :components="[]" readonly :retained="retained" :initial-expanded-depth="retained ? undefined : 1" @select="select" />
      </section>
      <section class="pipeline-detail" aria-label="Détail de l’étape sélectionnée">
        <q-btn v-if="parent" flat dense no-caps icon="arrow_back" :label="recipeLabel(parent.node)" class="pipeline-detail__back" @click="select(parent.path)" />
        <div class="pipeline-detail__heading"><span>Étape sélectionnée</span><h3>{{ recipeLabel(selected) }}</h3><code v-if="selected.component">{{ selected.component }}</code></div>
        <p class="pipeline-detail__explanation">{{ explanation }}</p>
        <div v-if="selectedEntry?.removed" class="pipeline-detail__notice"><q-icon name="block" size="17px" />Ce composant est exclu de la recherche enregistrée.</div>
        <div v-if="selected.optional" class="pipeline-detail__notice"><q-icon name="alt_route" size="17px" />Cette étape peut être ignorée pendant la recherche.</div>
        <div v-if="selected.condition" class="pipeline-detail__notice"><q-icon name="rule" size="17px" />Condition : {{ condition(selected.condition) }}</div>
        <template v-if="entries.length">
          <div class="pipeline-detail__list-heading"><h4>{{ retained ? 'Étapes utilisées' : relation }}</h4><small>{{ retained ? `${entries.length} étape${entries.length > 1 ? 's' : ''}` : `${included} inclus sur ${entries.length}` }}</small></div>
          <q-input v-if="entries.length > 6" v-model="search" outlined dense clearable label="Rechercher un composant" class="pipeline-detail__search"><template #prepend><q-icon name="search" size="19px" /></template></q-input>
          <div class="pipeline-detail__entries">
            <button v-for="entry in filteredEntries" :key="JSON.stringify(entry.path)" class="pipeline-entry" :class="{ 'pipeline-entry--removed': entry.removed }" @click="select(entry.path)">
              <span v-if="mode === 'sequence'" class="pipeline-entry__number">{{ entry.index + 1 }}</span>
              <q-icon v-else :name="entry.removed ? 'block' : entry.node.type === 'use' ? 'extension' : 'account_tree'" size="18px" />
              <span class="pipeline-entry__copy"><strong>{{ recipeLabel(entry.node) }}</strong><small>{{ entry.removed ? 'Exclu de la recherche' : recipeSummary(entry.node) }}</small></span>
              <q-icon name="chevron_right" size="18px" />
            </button>
            <p v-if="!filteredEntries.length" class="pipeline-detail__empty">Aucun composant ne correspond à cette recherche.</p>
          </div>
        </template>
        <h4 v-if="parameters.length" class="pipeline-detail__parameter-heading">{{ retained ? 'Paramètres retenus' : 'Réglages enregistrés' }}</h4>
        <dl v-if="parameters.length" class="pipeline-values">
          <div v-for="[key, parameter] in parameters" :key="key">
            <dt>{{ parameterLabels[key] ?? displayServiceName(key) }}<code v-if="parameterLabels[key]">{{ key }}</code></dt>
            <dd><strong>{{ display(parameterValue(parameter)) }}</strong><small v-if="!retained">{{ isSearch(parameter) ? 'Valeur de départ · ' : '' }}{{ parameterScope(parameter) }}</small><p v-if="parameter?.description">{{ parameter.description }}</p></dd>
          </div>
        </dl>
        <p v-else-if="!entries.length" class="pipeline-detail__empty">Aucun paramètre supplémentaire enregistré pour cette étape.</p>
      </section>
    </div>
    <p v-if="!retained" class="pipeline-overview__edit-note"><q-icon name="tune" size="16px" />Pour modifier les étapes ou leurs paramètres, utilisez « Adapter cette configuration ».</p>
  </section>
</template>

<style scoped>
.pipeline-overview { min-width: 0; color: var(--project-ink); }
.pipeline-overview h3, .pipeline-overview h4 { margin: 0; line-height: 1.5; font-weight: 600; }
.pipeline-overview h3 { font-size: 16px; }
.pipeline-overview h4 { font-size: 13px; }
.pipeline-entry:focus-visible { outline: 2px solid var(--q-primary); outline-offset: 3px; }
.pipeline-overview__columns { display: grid; grid-template-columns: minmax(0, 1.2fr) minmax(300px, 1fr); gap: 20px; align-items: start; }
.pipeline-map { min-width: 0; padding: 18px; border-radius: 12px; background: var(--project-paper); border: 1px solid var(--project-border); }
.pipeline-map__heading { display: flex; justify-content: space-between; align-items: baseline; gap: 12px; flex-wrap: wrap; }
.pipeline-map__heading span { color: var(--project-muted); font-size: 10px; }
.pipeline-map__hint { margin: 7px 0 16px; font-size: 12px; line-height: 1.7; color: var(--project-muted); }
.pipeline-detail { min-width: 0; padding: 20px; border: 1px solid var(--project-border); border-radius: 12px; background: var(--project-surface); }
.pipeline-detail__back { font-size: 11px; color: var(--q-primary); max-width: 100%; margin: -7px 0 14px -8px; }
.pipeline-detail__back :deep(.q-btn__content) { flex-wrap: nowrap; text-align: left; }
.pipeline-detail__back :deep(.block) { white-space: normal; overflow-wrap: anywhere; }
.pipeline-detail__heading > span { display: block; font-size: 10px; color: var(--project-muted); margin-bottom: 5px; }
.pipeline-detail__heading h3 { overflow-wrap: anywhere; }
.pipeline-detail__heading code { display: block; color: var(--project-muted); font: 11px/1.6 monospace; margin-top: 4px; overflow-wrap: anywhere; }
.pipeline-detail__explanation { font-size: 12px; line-height: 1.8; color: var(--project-muted); margin: 10px 0 18px; }
.pipeline-detail__notice { display: flex; gap: 8px; align-items: baseline; padding: 10px 12px; background: var(--project-paper); border-radius: 7px; margin: 8px 0; font-size: 11px; line-height: 1.7; overflow-wrap: anywhere; }
.pipeline-detail__notice .q-icon { flex-shrink: 0; color: var(--q-primary); }
.pipeline-detail__list-heading { display: flex; justify-content: space-between; align-items: center; gap: 10px; margin: 18px 0 10px; }
.pipeline-detail__list-heading small { color: var(--project-muted); white-space: nowrap; font-size: 10px; }
.pipeline-detail__search { margin: 0 0 10px; }
.pipeline-detail__entries { max-height: 380px; overflow-y: auto; border: 1px solid var(--project-border); border-radius: 8px; }
.pipeline-entry { display: flex; gap: 10px; align-items: center; text-align: left; border: 0; background: transparent; color: var(--project-ink); font: inherit; width: 100%; padding: 12px; cursor: pointer; }
.pipeline-entry + .pipeline-entry { border-top: 1px solid var(--project-border); }
.pipeline-entry:hover { background: var(--project-paper); }
.pipeline-entry > .q-icon { color: var(--project-muted); flex-shrink: 0; }
.pipeline-entry__copy { flex: 1; min-width: 0; display: flex; flex-direction: column; gap: 4px; }
.pipeline-entry strong { font-size: 12px; line-height: 1.5; font-weight: 500; overflow-wrap: anywhere; }
.pipeline-entry small { font-size: 10px; color: var(--project-muted); line-height: 1.5; }
.pipeline-entry__number { display: flex; align-items: center; justify-content: center; width: 22px; height: 22px; flex-shrink: 0; border-radius: 50%; background: var(--project-tint); color: var(--q-primary); font-size: 10px; }
.pipeline-entry--removed { color: var(--project-muted); background: var(--project-paper); }
.pipeline-detail__empty { font-size: 12px; line-height: 1.7; color: var(--project-muted); margin: 0; padding: 12px; }
.pipeline-detail__parameter-heading { margin-top: 22px !important; }
.pipeline-values { margin: 10px 0 0; }
.pipeline-values > div { padding: 12px 0; border-top: 1px solid var(--project-border); }
.pipeline-values dt { font-size: 12px; line-height: 1.6; overflow-wrap: anywhere; color: var(--project-muted); }
.pipeline-values dt code { display: inline; font: 10px/1.6 monospace; margin-left: 7px; }
.pipeline-values dd { margin: 5px 0 0; overflow-wrap: anywhere; white-space: pre-wrap; }
.pipeline-values strong { display: block; font-size: 13px; font-weight: 500; }
.pipeline-values small, .pipeline-values p { display: block; font-size: 11px; color: var(--project-muted); line-height: 1.7; margin: 4px 0 0; }
.pipeline-overview__edit-note { display: flex; align-items: baseline; gap: 8px; margin: 18px 0 0; color: var(--project-muted); line-height: 1.7; font-size: 12px; }
.pipeline-overview__edit-note .q-icon { flex-shrink: 0; color: var(--q-primary); }
@media (max-width: 1099px) { .pipeline-overview__columns { grid-template-columns: minmax(0, 1fr); } }
@media (max-width: 599px) { .pipeline-map { padding: 13px 10px; } .pipeline-detail { padding: 16px; } }
</style>
