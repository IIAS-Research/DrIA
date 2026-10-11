<script setup lang="ts">
import { computed, nextTick, ref, watch } from 'vue';
import type { QForm } from 'quasar';
import type { IRecipe } from '../models';
import { displayServiceName } from 'src/utils/service-label';

const props = defineProps<{ node: IRecipe; catalogueComponent?: any; disabled?: boolean }>();
const emit = defineEmits<{
  (event: 'changed'): void;
  (event: 'validity', valid: boolean, node: IRecipe): void;
}>();
type InputKind = 'number' | 'text' | 'boolean' | 'select' | 'json';
interface ParameterDraft {
  value: any;
  low: any;
  high: any;
  choices: any[];
  json: string;
  search: boolean;
  dirty: boolean;
  error: string;
}
interface NodeDrafts {
  initial: Record<string, any>;
  present: Set<string>;
  fields: Record<string, ParameterDraft>;
}
const form = ref<QForm>();
const snapshots = new WeakMap<IRecipe, NodeDrafts>();
const current = ref<NodeDrafts>({ initial: {}, present: new Set(), fields: {} });
const labels: Record<string, [string, string?]> = {
  n_estimators: ['Nombre d’arbres', 'Plus d’arbres peut améliorer le modèle, mais augmente le temps de calcul.'],
  iterations: ['Nombre d’itérations'], max_iter: ['Nombre maximal d’itérations'],
  learning_rate: ['Vitesse d’apprentissage', 'Contrôle l’importance donnée à chaque nouvelle étape d’apprentissage.'],
  max_depth: ['Profondeur maximale des arbres'], depth: ['Profondeur des arbres'],
  min_samples_split: ['Minimum d’observations pour une séparation'],
  min_samples_leaf: ['Minimum d’observations par feuille'], min_data_in_leaf: ['Minimum d’observations par feuille'],
  n_neighbors: ['Nombre de voisins'], k_neighbors: ['Voisins pour le rééquilibrage'], m_neighbors: ['Voisins pour détecter les frontières'],
  n_components: ['Nombre de composantes'], max_features: ['Variables utilisées'], max_samples: ['Observations utilisées'],
  random_state: ['Graine aléatoire', 'Une même graine facilite la reproduction des résultats.'],
  strategy: ['Méthode de remplacement'], fill_value: ['Valeur de remplacement'],
  missing_values: ['Valeur considérée comme manquante'], missing_label: ['Libellé pour les valeurs manquantes'],
  sampling_strategy: ['Méthode de rééquilibrage'], class_weight: ['Pondération des classes'],
  criterion: ['Critère de séparation'], loss: ['Fonction d’erreur'], loss_function: ['Fonction d’erreur'],
  penalty: ['Type de régularisation'], solver: ['Méthode de résolution'],
  C: ['Intensité inverse de régularisation', 'Une valeur plus faible renforce la régularisation.'],
  alpha: ['Intensité de régularisation'], l1_ratio: ['Part de régularisation L1'],
  tol: ['Tolérance de convergence', 'Définit à partir de quel niveau d’amélioration le calcul peut s’arrêter.'],
  bootstrap: ['Échantillonnage avec remise'], bootstrap_features: ['Variables tirées avec remise'],
  fit_intercept: ['Calculer une constante'], fit_prior: ['Estimer la fréquence des classes'],
  probability: ['Calculer les probabilités'], oob_score: ['Évaluation hors échantillon'],
  nsamples: ['Nombre d’échantillons d’explication', 'Davantage d’échantillons affine l’explication, avec un calcul plus long.'],
  pos_label: ['Classe positive', 'Classe considérée comme l’événement à détecter.'],
  average: ['Méthode d’agrégation'], threshold: ['Seuil'],
  unknown_value: ['Code d’une catégorie inconnue'], start_value: ['Premier code des catégories'],
  sort_categories: ['Trier les catégories'], copy: ['Travailler sur une copie'],
  add_indicator: ['Ajouter un indicateur de valeur manquante'], keep_empty_features: ['Conserver les colonnes entièrement vides'],
  kernel: ['Fonction de similarité'], gamma: ['Influence de chaque observation'],
  degree: ['Degré du polynôme'], shrinkage: ['Régularisation de la covariance'],
  sample_size: ['Taille de l’échantillon'],
};
const integerKeys = new Set([
  'n_estimators', 'iterations', 'max_iter', 'max_depth', 'depth', 'min_data_in_leaf',
  'n_neighbors', 'k_neighbors', 'm_neighbors', 'random_state', 'nsamples',
  'sample_size', 'degree', 'start_value', 'unknown_value', 'ngram_min', 'ngram_max',
  'border_count', 'leaf_estimation_iterations', 'verbose',
]);
const parameters = computed(() => [...new Set([
  ...Object.keys(props.node.params ?? {}),
  ...Object.keys(props.catalogueComponent?.parameters ?? {}),
])].filter((key) => key !== 'n_jobs' && key !== 'max_workers'));
const analysis = computed(() => ['metrics', 'statistics', 'explanations'].includes(
  props.node.kind ?? props.catalogueComponent?.kind,
));

function clone<T>(value: T): T {
  return value === undefined ? value : JSON.parse(JSON.stringify(value));
}
function descriptor(key: string): any {
  return props.node.params && Object.prototype.hasOwnProperty.call(props.node.params, key)
    ? props.node.params[key] : props.catalogueComponent?.parameters?.[key];
}
function originalDescriptor(key: string): any { return current.value.initial[key]; }
function catalogueDescriptor(key: string): any { return props.catalogueComponent?.parameters?.[key]; }
function valueKey(parameter: any): 'value' | 'initial' | undefined {
  if (!parameter || typeof parameter !== 'object' || Array.isArray(parameter)) return undefined;
  if (['parameter', 'const'].includes(parameter.type)) return 'value';
  if (['int', 'float', 'categorical'].includes(parameter.type)) return 'initial';
  return undefined;
}
function parameterValue(parameter: any): any {
  const key = valueKey(parameter);
  return key ? parameter[key] : parameter;
}
function numericBounds(parameter: any): any[] {
  if (parameter?.type === 'parameter' && Array.isArray(parameter.domain)) return parameter.domain;
  return ['int', 'float'].includes(parameter?.type) ? [parameter.low, parameter.high] : [];
}
function categoricalValues(parameter: any): any[] {
  if (parameter?.type === 'parameter' && Array.isArray(parameter.categorical)) return parameter.categorical;
  return parameter?.type === 'categorical' && Array.isArray(parameter.values) ? parameter.values : [];
}
function availableChoices(key: string): any[] {
  const result: any[] = [];
  for (const value of [
    ...categoricalValues(catalogueDescriptor(key)), ...categoricalValues(originalDescriptor(key)),
    ...categoricalValues(descriptor(key)),
  ]) if (!result.some((item) => sameValue(item, value))) result.push(value);
  return result;
}
function sameValue(left: any, right: any): boolean { return JSON.stringify(left) === JSON.stringify(right); }
function label(key: string): string {
  return labels[key]?.[0] ?? displayServiceName(key);
}
function description(key: string): string {
  return labels[key]?.[1] ?? descriptor(key)?.description ?? catalogueDescriptor(key)?.description ?? '';
}
function display(value: any): string {
  if (value === null || value === undefined) return 'Automatique';
  if (value === true) return 'Oui';
  if (value === false) return 'Non';
  const translated: Record<string, string> = {
    mean: 'Moyenne', median: 'Médiane', most_frequent: 'Valeur la plus fréquente',
    constant: 'Valeur constante', auto: 'Automatique', minority: 'Classe minoritaire',
    balanced: 'Équilibrée', uniform: 'Uniforme', distance: 'Selon la distance',
  };
  return typeof value === 'object' ? JSON.stringify(value) : translated[String(value)] ?? String(value);
}
function kind(key: string): InputKind {
  if (availableChoices(key).length) return 'select';
  const parameter = originalDescriptor(key);
  const value = parameterValue(parameter);
  if (typeof value === 'boolean') return 'boolean';
  if (typeof value === 'number' || ['int', 'float'].includes(parameter?.type)
    || numericBounds(parameter).length) return 'number';
  if (value === null || value === undefined || typeof value === 'object') return 'json';
  return 'text';
}
function isInteger(key: string): boolean {
  const parameter = descriptor(key);
  if (parameter?.type === 'float' || parameter?.integer === false) return false;
  return parameter?.type === 'int' || parameter?.integer === true || parameter?.dtype === 'int'
    || integerKeys.has(key)
    || (['min_samples_split', 'min_samples_leaf', 'n_components'].includes(key)
      && Number.isInteger(parameterValue(originalDescriptor(key)))
      && numericBounds(originalDescriptor(key)).every(Number.isInteger));
}
function supportsSearch(key: string): boolean {
  if (analysis.value) return false;
  const parameter = descriptor(key);
  const original = originalDescriptor(key);
  const catalogue = catalogueDescriptor(key);
  if ((parameter?.optimizable ?? original?.optimizable ?? catalogue?.optimizable) === false) return false;
  return numericBounds(parameter).length === 2 || numericBounds(original).length === 2
    || numericBounds(catalogue).length === 2 || availableChoices(key).length > 1;
}
function makeDraft(parameter: any, key: string): ParameterDraft {
  const range = numericBounds(parameter).length ? numericBounds(parameter) : numericBounds(catalogueDescriptor(key));
  const value = parameterValue(parameter);
  return {
    value: clone(value), low: range[0], high: range[1],
    choices: clone(categoricalValues(parameter)), json: JSON.stringify(value ?? null, null, 2),
    search: !analysis.value && ['parameter', 'int', 'float', 'categorical'].includes(parameter?.type)
      && parameter?.fixed !== true
      && parameter?.optimizable !== false && (range.length === 2 || categoricalValues(parameter).length > 1),
    dirty: false, error: '',
  };
}
function initialise(): void {
  let saved = snapshots.get(props.node);
  if (!saved) {
    saved = { initial: {}, present: new Set(Object.keys(props.node.params ?? {})), fields: {} };
    snapshots.set(props.node, saved);
  }
  current.value = saved;
  for (const key of parameters.value) {
    if (current.value.fields[key]) continue;
    current.value.initial[key] = clone(descriptor(key));
    current.value.fields[key] = makeDraft(descriptor(key), key);
  }
  publishValidity();
  nextTick(() => form.value?.resetValidation());
}
function validationError(key: string): string {
  const draft = current.value.fields[key];
  if (!draft) return '';
  if (draft.error) return draft.error;
  if (kind(key) === 'json') {
    try { JSON.parse(draft.json); } catch { return 'Cette valeur JSON est incomplète ou incorrecte.'; }
  }
  // Library defaults can retain an initial value outside their suggested search range.
  // Validate user edits without rejecting an untouched descriptor supplied by DrIA.
  if (!draft.dirty) return '';
  if (kind(key) === 'number') {
    if (draft.value === '' || draft.value === null || draft.value === undefined
      || !Number.isFinite(Number(draft.value))) return 'Indiquez une valeur numérique.';
    if (isInteger(key) && !Number.isInteger(Number(draft.value))) return 'Une valeur entière est attendue.';
    if (draft.search) {
      if ([draft.low, draft.high].some((value) => value === '' || value == null || !Number.isFinite(Number(value))))
        return 'Renseignez les deux limites de recherche.';
      const low = Number(draft.low); const high = Number(draft.high); const value = Number(draft.value);
      if (isInteger(key) && (!Number.isInteger(low) || !Number.isInteger(high))) return 'Les limites doivent être des entiers.';
      if (low > high) return 'La limite basse doit être inférieure ou égale à la limite haute.';
      if (value < low || value > high) return 'La valeur de départ doit être comprise entre les deux limites.';
    }
  }
  if (kind(key) === 'select' && draft.search) {
    if (!draft.choices.length) return 'Choisissez au moins une valeur à explorer.';
    if (!draft.choices.some((value) => sameValue(value, draft.value))) return 'La valeur de départ doit faire partie des valeurs à explorer.';
  }
  return '';
}
function publishValidity(): boolean {
  const valid = parameters.value.every((key) => !validationError(key));
  emit('validity', valid, props.node);
  return valid;
}
function commit(key: string): void {
  const draft = current.value.fields[key];
  if (validationError(key)) { publishValidity(); return; }
  let parameter = clone(descriptor(key));
  let field = valueKey(parameter);
  if (!field && draft.search && catalogueDescriptor(key)?.type === 'parameter') {
    parameter = clone(catalogueDescriptor(key)); field = 'value';
  }
  if (parameter?.type === 'const' && draft.search) {
    parameter = { ...clone(catalogueDescriptor(key) ?? originalDescriptor(key)), type: 'parameter' };
    field = 'value';
  }
  const value = kind(key) === 'number' ? Number(draft.value) : clone(draft.value);
  if (field) {
    parameter[field] = value;
    if (parameter.type === 'parameter') {
      parameter.fixed = analysis.value || !draft.search;
      if (analysis.value) { parameter.domain = null; parameter.categorical = null; }
      else if (draft.search) {
        parameter.optimizable = true;
        parameter.retained_initial = null;
        if (kind(key) === 'number') {
          parameter.domain = [Number(draft.low), Number(draft.high)];
          parameter.categorical = null; parameter.explicit_domain = true;
        } else {
          parameter.categorical = clone(draft.choices);
          parameter.domain = null; parameter.explicit_domain = false;
        }
      }
    } else if (['int', 'float'].includes(parameter.type) && draft.search) {
      parameter.low = Number(draft.low); parameter.high = Number(draft.high);
    } else if (parameter.type === 'categorical' && draft.search) parameter.values = clone(draft.choices);
    else if (!draft.search && ['int', 'float', 'categorical'].includes(parameter.type)) parameter = { type: 'const', value };
  } else parameter = value;
  props.node.params ??= {};
  props.node.params[key] = parameter;
  emit('changed');
  publishValidity();
}
function update(key: string, field: keyof ParameterDraft, value: any): void {
  const draft = current.value.fields[key];
  (draft as any)[field] = value;
  draft.dirty = true; draft.error = '';
  if (field === 'json') {
    try { draft.value = JSON.parse(value); }
    catch { draft.error = 'Cette valeur JSON est incomplète ou incorrecte.'; }
  }
  commit(key);
}
function toggleSearch(key: string, search: boolean): void {
  const draft = current.value.fields[key];
  if (search && !draft.choices.length) draft.choices = clone(availableChoices(key));
  update(key, 'search', search);
}
function reset(key: string): void {
  if (current.value.present.has(key)) {
    props.node.params ??= {}; props.node.params[key] = clone(current.value.initial[key]);
  } else if (props.node.params) delete props.node.params[key];
  current.value.fields[key] = makeDraft(current.value.initial[key], key);
  emit('changed'); publishValidity();
  nextTick(() => form.value?.resetValidation());
}
async function validate(): Promise<boolean> {
  const valid = publishValidity();
  const formValid = await form.value?.validate();
  return valid && formValid !== false;
}
watch([() => props.node, () => props.catalogueComponent], initialise, { immediate: true });
defineExpose({ validate });
</script>

<template>
  <q-form ref="form" class="recipe-parameters" @submit.prevent>
    <div class="recipe-parameters__intro">
      <div>
        <h4>Paramètres de l’étape</h4>
        <p v-if="parameters.some(supportsSearch)">Fixez une valeur ou définissez les valeurs que DrIA peut explorer pendant la recherche.</p>
        <p v-else-if="parameters.length">Les valeurs choisies seront utilisées à chaque exécution de cette étape.</p>
      </div>
      <span v-if="parameters.length" class="recipe-parameters__count">{{ parameters.length }} paramètre{{ parameters.length > 1 ? 's' : '' }}</span>
    </div>
    <div v-if="!parameters.length" class="recipe-parameters__empty">
      <q-icon name="tune" size="24px" />
      <div>Cette étape ne propose pas de paramètre à modifier.</div>
    </div>
    <section v-for="key in parameters" :key="key" class="recipe-parameter">
      <div class="recipe-parameter__heading">
        <div><h5>{{ label(key) }}</h5><code>{{ key }}</code></div>
        <q-btn flat dense no-caps icon="restart_alt" label="Réinitialiser" :disable="disabled || !current.fields[key]?.dirty" @click="reset(key)">
          <q-tooltip>Revenir à la configuration de cette étape avant vos modifications</q-tooltip>
        </q-btn>
      </div>
      <p v-if="description(key)" class="recipe-parameter__description">{{ description(key) }}</p>
      <template v-if="current.fields[key]">
        <div class="recipe-parameter__mode">
          <q-toggle v-if="supportsSearch(key)" :model-value="current.fields[key].search" label="Explorer plusieurs valeurs" :disable="disabled" @update:model-value="(value) => toggleSearch(key, value)" />
          <span v-else class="recipe-parameter__fixed"><q-icon name="lock_outline" size="15px" /> Valeur fixe</span>
          <span v-if="supportsSearch(key)" class="recipe-parameter__mode-hint">{{ current.fields[key].search ? 'DrIA recherchera la valeur la plus adaptée.' : 'Cette valeur sera identique pour tous les essais.' }}</span>
        </div>
        <q-input v-if="kind(key) === 'number'" :model-value="current.fields[key].value" outlined type="number" :step="isInteger(key) ? 1 : 'any'" :label="current.fields[key].search ? 'Valeur de départ' : 'Valeur'" :disable="disabled" :error="!!validationError(key)" :error-message="validationError(key)" :rules="[() => validationError(key) || true]" lazy-rules reactive-rules @update:model-value="(value) => update(key, 'value', value)" />
        <q-select v-else-if="kind(key) === 'select'" :model-value="current.fields[key].value" :options="availableChoices(key).map((value) => ({ label: display(value), value }))" emit-value map-options outlined :label="current.fields[key].search ? 'Valeur de départ' : 'Valeur'" :disable="disabled" :error="!!validationError(key)" :error-message="validationError(key)" :rules="[() => validationError(key) || true]" reactive-rules @update:model-value="(value) => update(key, 'value', value)" />
        <q-toggle v-else-if="kind(key) === 'boolean'" :model-value="current.fields[key].value" :label="current.fields[key].value ? 'Activé' : 'Désactivé'" :disable="disabled" @update:model-value="(value) => update(key, 'value', value)" />
        <q-input v-else-if="kind(key) === 'text'" :model-value="current.fields[key].value" outlined label="Valeur" :disable="disabled" @update:model-value="(value) => update(key, 'value', value)" />
        <q-input v-else :model-value="current.fields[key].json" outlined autogrow type="textarea" label="Valeur structurée (JSON)" hint="null laisse le choix automatique. Les valeurs structurées conservent leur format." :disable="disabled" :error="!!validationError(key)" :error-message="validationError(key)" :rules="[() => validationError(key) || true]" reactive-rules class="recipe-parameter__json" @update:model-value="(value) => update(key, 'json', value)" />
        <div v-if="current.fields[key].search && kind(key) === 'number'" class="recipe-parameter__bounds">
          <q-input :model-value="current.fields[key].low" outlined type="number" :step="isInteger(key) ? 1 : 'any'" label="Limite basse" :disable="disabled" @update:model-value="(value) => update(key, 'low', value)" />
          <q-input :model-value="current.fields[key].high" outlined type="number" :step="isInteger(key) ? 1 : 'any'" label="Limite haute" :disable="disabled" @update:model-value="(value) => update(key, 'high', value)" />
        </div>
        <q-select v-if="current.fields[key].search && kind(key) === 'select'" :model-value="current.fields[key].choices" :options="availableChoices(key).map((value) => ({ label: display(value), value }))" emit-value map-options multiple use-chips outlined label="Valeurs à explorer" hint="DrIA comparera uniquement les valeurs sélectionnées." :disable="disabled" @update:model-value="(value) => update(key, 'choices', value)" />
        <p class="recipe-parameter__initial">Configuration initiale : {{ display(parameterValue(originalDescriptor(key))) }}</p>
      </template>
    </section>
  </q-form>
</template>

<style scoped lang="scss">
.recipe-parameters {
  min-width: 0;
  color: var(--project-ink, #243746);
  &__intro { display: flex; align-items: flex-start; gap: 12px; justify-content: space-between; margin-bottom: 18px; }
  &__intro h4 { font-size: 16px; font-weight: 650; line-height: 1.45; margin: 0 0 6px; }
  &__intro p { font-size: 13px; line-height: 1.6; margin: 0; color: var(--project-muted, #657786); }
  &__count { font-size: 11px; white-space: nowrap; border-radius: 6px; background: var(--project-paper, #f5f7fa); padding: 5px 8px; }
  &__empty { display: flex; gap: 12px; align-items: center; padding: 20px; border: 1px dashed var(--project-border, #e0e7ed); border-radius: 12px; color: var(--project-muted, #657786); }
}
.recipe-parameter {
  padding: 18px 0;
  border-top: 1px solid var(--project-border, #e0e7ed);
  &__heading { display: flex; align-items: flex-start; justify-content: space-between; gap: 10px; }
  &__heading > div { min-width: 0; }
  &__heading h5 { font-size: 14px; font-weight: 650; line-height: 1.5; margin: 0; overflow-wrap: anywhere; }
  &__heading code { font-size: 11px; line-height: 1.5; color: var(--project-muted, #657786); overflow-wrap: anywhere; }
  &__heading .q-btn { color: var(--project-primary, #287f83); flex-shrink: 0; font-size: 11px; }
  &__description { margin: 8px 0 0; font-size: 12px; line-height: 1.7; color: var(--project-muted, #657786); }
  &__mode { display: flex; flex-wrap: wrap; align-items: center; gap: 0 12px; margin: 12px 0; }
  &__mode .q-toggle { margin-left: -9px; font-size: 12px; }
  &__mode-hint { font-size: 11px; line-height: 1.6; color: var(--project-muted, #657786); }
  &__fixed { display: inline-flex; gap: 5px; align-items: center; font-size: 11px; color: var(--project-muted, #657786); }
  &__bounds { display: grid; grid-template-columns: repeat(2, minmax(0, 1fr)); gap: 10px; margin: 12px 0; }
  &__initial { color: var(--project-muted, #657786); font-size: 11px; line-height: 1.7; margin: 10px 0 0; overflow-wrap: anywhere; }
  &__json :deep(textarea) { font-family: monospace; font-size: 12px; overflow-wrap: anywhere; }
  :deep(.q-field) { min-width: 0; }
  :deep(.q-field__control), :deep(.q-field__native), :deep(.q-field__input), :deep(.q-chip) { min-width: 0; }
  :deep(.q-field__label) { max-width: calc(100% - 24px); }
  :deep(.q-toggle__inner--truthy) { color: var(--project-primary, #287f83); }
  :deep(.q-chip__content) { white-space: normal; overflow-wrap: anywhere; }
}
@media (max-width: 599px) {
  .recipe-parameters__intro { flex-wrap: wrap; }
  .recipe-parameter__bounds { grid-template-columns: minmax(0, 1fr); }
  .recipe-parameter__heading .q-btn :deep(.q-btn__content) { gap: 3px; }
}
</style>
