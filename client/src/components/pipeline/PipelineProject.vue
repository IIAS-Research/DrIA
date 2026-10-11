<script setup lang="ts">
import { computed, nextTick, onMounted, ref, watch } from 'vue';
import { getCatalogue, validateStudy } from 'src/api/ia-web-service.api';
import { apiError } from 'src/utils/iaml';
import { displayServiceName } from 'src/utils/service-label';
import { IPipeline, IRecipe } from '../models';
import {
  CatalogueComponent, RecipePath, cloneJson, recipeAt, recipeLabel, recipeSummary,
  recipeEntries, walkRecipes, newComponentRecipe, newFamilyRecipe, setMemberEnabled,
  clearPreset, structuralCheck, isValidStudyShape,
} from 'src/utils/pipeline-editor';
import RecipeFlow from './RecipeFlow.vue';
import RecipeParameters from './RecipeParameters.vue';
import 'src/css/pipeline-editor.scss';

const props = defineProps<{ pipeline: IPipeline; guidedSearch?: boolean }>();
const emits = defineEmits<{
  (event: 'isPipelineLoading', status: boolean): void;
  (event: 'validity-change', valid: boolean): void;
}>();
type Section = 'pipeline' | 'metrics' | 'statistics' | 'explanations' | 'options';
const sections: { key: Section; label: string; icon: string }[] = [
  { key: 'pipeline', label: 'Pipeline', icon: 'account_tree' },
  { key: 'metrics', label: 'Évaluation', icon: 'speed' },
  { key: 'statistics', label: 'Statistiques', icon: 'table_chart' },
  { key: 'explanations', label: 'Explicabilité', icon: 'insights' },
  { key: 'options', label: 'Recherche', icon: 'tune' },
];
function editableStudy(configuration: IPipeline): IPipeline {
  const copy = cloneJson(configuration);
  if (copy.options) delete copy.options.max_workers;
  return copy;
}
const pipelineRefLocalComponent = ref<IPipeline>(editableStudy(props.pipeline));
const section = ref<Section>('pipeline');
const pipelinePath: RecipePath = ['pipeline'];
const selectedPath = ref<RecipePath>(pipelinePath);
const catalogue = ref<any>();
const catalogueLoading = ref(false);
const catalogueError = ref('');
const loading = ref(false);
const error = ref('');
const feedback = ref('');
const parameterEditor = ref<InstanceType<typeof RecipeParameters>>();
const invalidNodes = ref(new Set<IRecipe>());
const history = ref<string[]>([]);
let lastSnapshot = JSON.stringify(pipelineRefLocalComponent.value);
const originalSnapshot = ref(lastSnapshot);
const jsonText = ref(JSON.stringify(pipelineRefLocalComponent.value, null, 2));
const jsonDirty = ref(false);
const jsonError = ref('');
const memberSearch = ref<string | null>('');
const inspectorElement = ref<HTMLElement>();
const components = computed<CatalogueComponent[]>(() => catalogue.value?.components ?? []);
const study = computed(() => pipelineRefLocalComponent.value);
const options = computed(() => study.value.options ?? (study.value.options = {}));
const fit = computed(() => study.value.fit_options ?? (study.value.fit_options = {}));
const pipelineRoot = computed(() => study.value.pipeline);
const selectedNode = computed(() => recipeAt(study.value, selectedPath.value));
const canDuplicate = computed(() => section.value === 'pipeline'
  && selectedPath.value[selectedPath.value.length - 2] === 'children'
  && recipeAt(study.value, selectedPath.value.slice(0, -2))?.type === 'choice');
const selectedComponent = computed(() => components.value.find((component) => component.id === selectedNode.value?.component));
const selectedEntries = computed(() => selectedNode.value ? recipeEntries(selectedNode.value, selectedPath.value) : []);
const memberEntries = computed(() => selectedEntries.value.filter((entry) => entry.source === 'members'));
const shownMembers = computed(() => memberEntries.value.filter(({ node }) => `${recipeLabel(node, components.value)} ${node.component}`.toLocaleLowerCase().includes((memberSearch.value ?? '').toLocaleLowerCase())));
const checks = computed(() => structuralCheck(study.value, components.value));
const settingsErrors = computed(() => {
  const problems: string[] = [];
  const integer = (value: any, min: number, max: number) => Number.isInteger(value) && value >= min && value <= max;
  if (!integer(options.value.max_duration, catalogue.value?.limits?.min_duration ?? 60, catalogue.value?.limits?.max_duration ?? 3600)) problems.push('Le budget de recherche doit être compris entre 60 et 3 600 secondes.');
  if (options.value.max_stage_duration != null && !integer(options.value.max_stage_duration, 1, options.value.max_duration)) problems.push('Le temps maximal par étape doit rester inférieur ou égal au budget de recherche.');
  if (fit.value.n_candidates != null && !integer(fit.value.n_candidates, 1, 100)) problems.push('Conservez entre 1 et 100 modèles.');
  if (fit.value.patience != null && !integer(fit.value.patience, -1, 10000)) problems.push('La patience doit être un entier compris entre −1 et 10 000.');
  if (fit.value.generation_sample_size != null && !integer(fit.value.generation_sample_size, 1, 1000000)) problems.push('Indiquez une taille d’échantillon entière et positive.');
  return problems;
});
const activeInvalidNodes = computed(() => {
  const active = new Set<IRecipe>();
  const visit = (node: IRecipe) => {
    active.add(node);
    recipeEntries(node, []).filter((entry) => !entry.removed).forEach((entry) => visit(entry.node));
  };
  for (const key of ['pipeline', 'metrics', 'statistics', 'explanations'] as const) if (study.value[key]) visit(study.value[key]!);
  return [...invalidNodes.value].filter((node) => active.has(node));
});
const configurationValid = computed(() => !checks.value.length && !settingsErrors.value.length && !activeInvalidNodes.value.length && !jsonDirty.value && !jsonError.value);
const modified = computed(() => JSON.stringify(study.value) !== originalSnapshot.value);
const metricChoices = computed(() => study.value.metrics ? recipeEntries(study.value.metrics, ['metrics']).filter((entry) => !entry.removed && entry.node.type === 'use').map(({ node }) => ({ label: recipeLabel(node, components.value), value: node.alias || node.component })) : []);
const collectionTitles: Record<string, { title: string; description: string }> = {
  metrics: { title: 'Comment comparer les modèles ?', description: 'Ces scores servent à évaluer les modèles. Le critère qui guide la recherche se choisit dans « Recherche ».' },
  statistics: { title: 'Quelles statistiques calculer ?', description: 'Choisissez les analyses du bilan des données. DrIA applique celles qui conviennent aux types de colonnes.' },
  explanations: { title: 'Comment comprendre les résultats ?', description: 'Ces analyses aident à comprendre les prédictions et à examiner les performances des modèles.' },
};
const collectionNode = computed(() => section.value === 'options' || section.value === 'pipeline' ? undefined : study.value[section.value]);
const collectionEntries = computed(() => collectionNode.value ? recipeEntries(collectionNode.value, [section.value]) : []);
const conditionChoices = [
  { label: 'Toujours proposer cette étape', value: undefined },
  { label: 'Si des valeurs sont manquantes', value: 'missing_values' },
];
const modeChoices = [
  { label: 'Un choix parmi les alternatives', value: 'choice' },
  { label: 'À la suite, dans cet ordre', value: 'sequence' },
  { label: 'Selon les données', value: 'adaptive' },
];
const modeHelp: Record<string, string> = {
  choice: 'DrIA compare les alternatives et retient un choix pour chaque pipeline.',
  sequence: 'Toutes les étapes sont exécutées dans l’ordre affiché. Les flèches permettent de modifier cet ordre.',
  adaptive: 'DrIA applique les traitements compatibles avec les données. Ce groupe ne représente pas des alternatives exclusives.',
};

function changed(structure = false) {
  if (structure) clearPreset(study.value);
  feedback.value = ''; error.value = '';
  const next = JSON.stringify(study.value);
  if (next !== lastSnapshot) { history.value = [...history.value.slice(-29), lastSnapshot]; lastSnapshot = next; }
  if (!jsonDirty.value) jsonText.value = JSON.stringify(study.value, null, 2);
}
function restoreSnapshot(snapshot: string) {
  pipelineRefLocalComponent.value = JSON.parse(snapshot); invalidNodes.value = new Set();
  selectedPath.value = pipelinePath; lastSnapshot = snapshot;
  jsonDirty.value = false; jsonError.value = ''; jsonText.value = JSON.stringify(study.value, null, 2);
  feedback.value = ''; error.value = '';
}
function undo() {
  const snapshot = history.value.pop();
  if (!snapshot) return;
  const path = selectedPath.value;
  restoreSnapshot(snapshot);
  selectedPath.value = recipeAt(study.value, path) ? path : pipelinePath;
}
const resetDialog = ref(false);
function reset() { restoreSnapshot(originalSnapshot.value); history.value = []; resetDialog.value = false; }
function select(path: RecipePath) {
  selectedPath.value = path; memberSearch.value = '';
  if (typeof window !== 'undefined' && window.innerWidth < 1000) nextTick(() => inspectorElement.value?.scrollIntoView({ behavior: 'smooth', block: 'start' }));
}
function revealInvalid(node: IRecipe) {
  let path: RecipePath | undefined;
  walkRecipes(study.value, (candidate, candidatePath) => { if (candidate === node) path = candidatePath; });
  if (!path) return;
  const destination = path;
  section.value = destination[0] as Section;
  nextTick(() => select(destination));
}
function selectParent() {
  const path = selectedPath.value;
  const count = path[path.length - 1] === 'recipe' ? path[path.length - 3] === 'members' ? 3 : 1 : 2;
  select(path.slice(0, -count));
}
function parameterValidity(valid: boolean, node?: IRecipe) {
  const target = node ?? selectedNode.value;
  if (!target) return;
  const updated = new Set(invalidNodes.value);
  if (valid) updated.delete(target); else updated.add(target);
  invalidNodes.value = updated;
}
function rename(value: string | number | null) {
  if (!selectedNode.value) return;
  const previous = selectedNode.value.alias;
  const alias = String(value ?? '').trim();
  if (alias) selectedNode.value.alias = alias; else delete selectedNode.value.alias;
  if (previous && previous !== alias) {
    walkRecipes(study.value, (node) => {
      if (Array.isArray(node.initial)) node.initial = node.initial.flatMap((item: any) => item === previous ? alias ? [alias] : [] : [item]);
    });
  }
  if (section.value === 'metrics' && options.value.main_metric === (previous || selectedNode.value.component)) options.value.main_metric = alias || selectedNode.value.component || null;
  changed(section.value === 'pipeline');
}
function setCondition(value: any) {
  if (!selectedNode.value) return;
  if (value) selectedNode.value.condition = value; else delete selectedNode.value.condition;
  changed(true);
}
function setOptional(value: boolean) {
  if (!selectedNode.value) return;
  selectedNode.value.optional = value;
  delete selectedNode.value.preset_start;
  changed(true);
}
function setMode(value: string) {
  const node = selectedNode.value;
  if (!node) return;
  if (node.recipe) { node.children = [...(node.children ?? []), node.recipe]; delete node.recipe; }
  if (node.type === 'family') node.mode = value;
  else node.type = value;
  if (value !== 'choice') ['optional', 'initial', 'preset_start', 'default_preset'].forEach((key) => delete node[key]);
  if (node.type === 'family') delete node.default_preset;
  changed(true);
}
function toggleMember(index: number, enabled: boolean) { if (selectedNode.value) { setMemberEnabled(selectedNode.value, index, enabled); changed(true); } }
function toggleCollectionMember(entry: any, enabled: boolean) {
  if (!collectionNode.value) return;
  setMemberEnabled(collectionNode.value, entry.index, enabled);
  if (!enabled && JSON.stringify(entry.path) === JSON.stringify(selectedPath.value)) selectedPath.value = [section.value];
  changed(true);
}
function optionalFitChanged() {
  for (const key of ['patience', 'generation_sample_size']) if (fit.value[key] === '' || fit.value[key] == null) delete fit.value[key];
  changed();
}
function updateBudget(value: any) {
  options.value.max_duration = value === '' || value == null ? null : Number(value);
  if (Number.isInteger(options.value.max_duration) && options.value.max_duration >= 60 && options.value.max_stage_duration > options.value.max_duration) options.value.max_stage_duration = options.value.max_duration;
  changed();
}
const registryLabels: Record<string, string> = { nb_folds: 'Nombre de plis', ratio: 'Part réservée à la validation', random_state: 'Graine aléatoire', nb_candidate: 'Candidats par génération', mutation_power: 'Intensité des mutations', initial_modifier: 'Variations initiales', max_iterations: 'Nombre maximal d’itérations' };
function registry(key: 'optimizer' | 'splitter') { return catalogue.value?.[key === 'optimizer' ? 'optimizers' : 'splitters'] ?? {}; }
function registryChoices(key: 'optimizer' | 'splitter') {
  const names: Record<string, string> = { genetic: 'Algorithme génétique', bayesian: 'Optimisation bayésienne', random: key === 'optimizer' ? 'Recherche aléatoire' : 'Séparation aléatoire', kfold: 'Validation croisée' };
  return Object.keys(registry(key)).map((id) => ({ label: names[id] || displayServiceName(id), value: id }));
}
function updateRegistry(key: 'optimizer' | 'splitter', id: string) {
  options.value[key] = { id, params: Object.fromEntries(Object.entries(registry(key)[id] ?? {}).map(([name, metadata]: [string, any]) => [name, cloneJson(metadata.default)])) }; changed();
}
function registryId(key: 'optimizer' | 'splitter'): string { return typeof options.value[key] === 'string' ? options.value[key] : options.value[key]?.id; }
function registryParams(key: 'optimizer' | 'splitter'): string[] { return Object.keys({ ...(registry(key)[registryId(key)] ?? {}), ...(options.value[key]?.params ?? {}) }); }
function registryValue(key: 'optimizer' | 'splitter', name: string): any { return options.value[key]?.params?.[name] ?? registry(key)[registryId(key)]?.[name]?.default; }
function updateRegistryParam(key: 'optimizer' | 'splitter', name: string, value: any) {
  if (typeof options.value[key] === 'string') options.value[key] = { id: options.value[key], params: {} };
  options.value[key].params ??= {}; options.value[key].params[name] = typeof registryValue(key, name) === 'number' ? Number(value) : value; changed();
}

const addDialog = ref(false);
const addPath = ref<RecipePath>([]);
const addType = ref<'family' | 'component' | 'group'>('family');
const addSearch = ref<string | null>('');
const addSelected = ref<string>();
const addKind = computed(() => String(addPath.value[0] ?? 'pipeline'));
const addItems = computed<{ id: string; label: string; caption: string }[]>(() => {
  let items: { id: string; label: string; caption: string }[] = [];
  if (addType.value === 'group') items = modeChoices.map((mode) => ({ id: mode.value, label: mode.label, caption: modeHelp[mode.value] }));
  else if (addType.value === 'family') items = (catalogue.value?.families ?? []).map((family: string) => ({ id: family, label: recipeLabel({ type: 'family', family }, components.value), caption: `${components.value.filter((component) => component.kind === addKind.value && component.available && component.families?.includes(family)).length} composants disponibles` })).filter((item: any) => !item.caption.startsWith('0 '));
  else items = components.value.filter((component) => component.kind === addKind.value && component.available).map((component) => ({ id: component.id, label: recipeLabel({ type: 'use', component: component.id }, components.value), caption: component.description || displayServiceName(component.id) }));
  const terms = (addSearch.value ?? '').toLocaleLowerCase().split(/\s+/).filter(Boolean);
  return items.filter((item) => terms.every((term) => `${item.label} ${item.id} ${item.caption}`.toLocaleLowerCase().includes(term)));
});
function openAdd(path: RecipePath) { addPath.value = path; addType.value = path[0] === 'pipeline' ? 'family' : 'component'; addSearch.value = ''; addSelected.value = undefined; addDialog.value = true; }
function addNode() {
  let parent = recipeAt(study.value, addPath.value);
  if (!parent && addPath.value.length === 1 && ['metrics', 'statistics', 'explanations'].includes(addKind.value)) {
    parent = { type: 'collection', kind: addKind.value, defaults: false, children: [] };
    study.value[addKind.value as 'metrics' | 'statistics' | 'explanations'] = parent;
  }
  if (!parent || !addSelected.value) return;
  if (parent.recipe) { parent.children = [...(parent.children ?? []), parent.recipe]; delete parent.recipe; }
  let node: IRecipe;
  if (addType.value === 'family') node = newFamilyRecipe(addSelected.value, components.value);
  else if (addType.value === 'group') node = { type: addSelected.value, kind: 'pipeline', children: [] };
  else { const component = components.value.find((entry) => entry.id === addSelected.value); if (!component) return; node = newComponentRecipe(component); }
  parent.children ??= [];
  let index = parent.children.length;
  const isModel = (candidate: IRecipe) => candidate.family?.includes('predictor') || ['classifier', 'regressor', 'survival'].includes(candidate.family ?? '') || components.value.find((entry) => entry.id === candidate.component)?.capabilities?.includes('predict');
  if (parent.type === 'sequence' && !isModel(node) && parent.children.length && isModel(parent.children[parent.children.length - 1])) index -= 1;
  parent.children.splice(index, 0, node); addDialog.value = false; changed(true); select([...addPath.value, 'children', index]);
}
function move(path: RecipePath, direction: number) {
  if (path[path.length - 2] !== 'children') return;
  const parent = recipeAt(study.value, path.slice(0, -2)), index = Number(path[path.length - 1]), destination = index + direction;
  if (!parent?.children || destination < 0 || destination >= parent.children.length) return;
  const child = parent.children.splice(index, 1)[0]; parent.children.splice(destination, 0, child);
  if (Array.isArray(parent.initial)) parent.initial = parent.initial.map((value: any) => value === index ? destination : value === destination ? index : value);
  selectedPath.value = [...path.slice(0, -1), destination]; changed(true);
}
const removeDialog = ref(false);
const removePath = ref<RecipePath>([]);
const removalLabel = computed(() => { const node = recipeAt(study.value, removePath.value); return node ? recipeLabel(node, components.value) : 'cette étape'; });
function requestRemove(path: RecipePath) { removePath.value = path; removeDialog.value = true; }
function removeNode() {
  if (removePath.value[removePath.value.length - 2] !== 'children') return;
  const parentPath = removePath.value.slice(0, -2);
  const parent = recipeAt(study.value, parentPath), index = Number(removePath.value[removePath.value.length - 1]);
  if (!parent?.children) return;
  const removed = parent.children.splice(index, 1)[0];
  if (Array.isArray(parent.initial)) {
    parent.initial = parent.initial.filter((value: any) => value !== index && value !== removed?.alias && value !== removed?.component).map((value: any) => typeof value === 'number' && value > index ? value - 1 : value);
    if (!parent.initial.length) delete parent.initial;
  }
  selectedPath.value = parentPath; removeDialog.value = false; changed(true);
}
function createPipeline() {
  study.value.pipeline = { type: 'sequence', kind: 'pipeline', children: [] };
  changed(true); select(pipelinePath);
}
function duplicateSelected() {
  if (!canDuplicate.value || !selectedNode.value) return;
  const parentPath = selectedPath.value.slice(0, -2);
  const parent = recipeAt(study.value, parentPath);
  if (!parent?.children) return;
  const index = Number(selectedPath.value[selectedPath.value.length - 1]) + 1;
  const copy = cloneJson(selectedNode.value);
  const clearAliases = (node: IRecipe) => { delete node.alias; delete node.initial; delete node.preset_start; recipeEntries(node, []).forEach((entry) => clearAliases(entry.node)); };
  clearAliases(copy);
  const aliases = new Set<string>(); walkRecipes(study.value, (node) => { if (node.alias) aliases.add(node.alias); });
  let suffix = 1; while (aliases.has(`alternative_${suffix}`)) suffix += 1;
  copy.alias = `alternative_${suffix}`;
  parent.children.splice(index, 0, copy);
  if (Array.isArray(parent.initial)) parent.initial = parent.initial.map((value: any) => typeof value === 'number' && value >= index ? value + 1 : value);
  changed(true); select([...parentPath, 'children', index]);
}
function applyJson(): boolean {
  try {
    const parsed = JSON.parse(jsonText.value);
    if (!isValidStudyShape(parsed)) throw new Error();
    pipelineRefLocalComponent.value = editableStudy(parsed); invalidNodes.value = new Set(); jsonDirty.value = false; jsonError.value = '';
    selectedPath.value = pipelinePath; changed(); return true;
  } catch { jsonError.value = 'Cette configuration JSON est invalide. Corrigez-la ou abandonnez ces modifications.'; return false; }
}
function discardJson() { jsonDirty.value = false; jsonError.value = ''; jsonText.value = JSON.stringify(study.value, null, 2); }
function getConfiguration(): IPipeline | undefined {
  if (jsonDirty.value && !applyJson()) return undefined;
  if (!configurationValid.value) { error.value = 'Complétez les réglages signalés avant de poursuivre.'; return undefined; }
  return study.value;
}
async function validateConfiguration(): Promise<IPipeline | undefined> {
  if (loading.value || parameterEditor.value && !await parameterEditor.value.validate()) return undefined;
  const configuration = getConfiguration(); if (!configuration) return undefined;
  loading.value = true; error.value = ''; emits('isPipelineLoading', true);
  try {
    const result = await validateStudy(cloneJson(configuration)); pipelineRefLocalComponent.value = editableStudy(result.configuration); invalidNodes.value = new Set(); changed();
    if (!recipeAt(study.value, selectedPath.value)) selectedPath.value = [section.value === 'options' ? 'pipeline' : section.value];
    feedback.value = 'La configuration est valide. DrIA peut explorer ces pipelines.'; return study.value;
  } catch (exception) { error.value = apiError(exception); return undefined; }
  finally { loading.value = false; emits('isPipelineLoading', false); }
}
async function loadCatalogue() {
  catalogueLoading.value = true; catalogueError.value = '';
  try { catalogue.value = await getCatalogue(); } catch (exception) { catalogueError.value = apiError(exception); } finally { catalogueLoading.value = false; }
}
watch(configurationValid, (valid) => emits('validity-change', valid), { immediate: true });
watch(section, (value) => { memberSearch.value = ''; if (value !== 'options') selectedPath.value = [value]; });
watch(() => props.pipeline, (value) => { originalSnapshot.value = JSON.stringify(editableStudy(value)); restoreSnapshot(originalSnapshot.value); history.value = []; });
onMounted(() => { emits('isPipelineLoading', false); loadCatalogue(); });
defineExpose({ pipelineRefLocalComponent, validateConfiguration, getConfiguration });
</script>

<template>
  <div class="study-editor" :inert="loading ? '' : undefined">
    <div class="pipeline-toolbar">
      <div><span class="pipeline-eyebrow">Configuration</span><strong>{{ modified ? 'Vos modifications' : 'Configuration de départ' }}</strong><span class="pipeline-status">{{ feedback || (modified ? 'Modifications appliquées à cette configuration.' : 'Sélectionnez un groupe ou une étape pour le configurer.') }}</span></div>
      <div class="pipeline-toolbar-actions"><q-btn flat no-caps icon="undo" label="Annuler" :disable="!history.length || loading || jsonDirty" @click="undo" /><q-btn flat no-caps icon="restart_alt" label="Rétablir" :disable="!modified || loading" @click="resetDialog = true" /><q-btn outline no-caps color="primary" icon="check" label="Vérifier" :loading="loading" :disable="!configurationValid" @click="validateConfiguration" /></div>
    </div>
    <q-tabs v-model="section" align="left" no-caps active-color="primary" indicator-color="primary" class="pipeline-tabs" outside-arrows mobile-arrows><q-tab v-for="item in sections" :key="item.key" :name="item.key" :label="item.label" :icon="item.icon" /></q-tabs>
    <div v-if="catalogueError" class="pipeline-notice"><q-icon name="cloud_off" /><div>Le catalogue n’a pas pu être chargé. Les étapes existantes restent consultables.<small>{{ catalogueError }}</small></div><q-btn flat no-caps label="Réessayer" @click="loadCatalogue" /></div>
    <div v-if="checks.length || settingsErrors.length || activeInvalidNodes.length || jsonDirty" class="pipeline-notice pipeline-notice-warning" role="status"><q-icon name="edit_note" /><div><strong>Quelques réglages restent à compléter</strong><ul><li v-for="message in [...checks, ...settingsErrors]" :key="message">{{ message }}</li><li v-if="activeInvalidNodes.length">Paramètres à corriger : <q-btn v-for="node in activeInvalidNodes" :key="recipeLabel(node, components)" flat dense no-caps color="primary" :label="recipeLabel(node, components)" @click="revealInvalid(node)" /></li><li v-if="jsonDirty">Appliquez ou abandonnez les modifications de la configuration JSON.</li></ul></div></div>
    <div v-if="error" class="pipeline-notice pipeline-notice-error" role="alert"><q-icon name="info_outline" /><div>{{ error }}</div></div>

    <div v-show="section !== 'options'" class="pipeline-workbench">
      <section v-if="section === 'pipeline'" class="pipeline-canvas" aria-label="Étapes et alternatives du pipeline">
        <div class="pipeline-section-heading"><div><h2>Le pipeline que DrIA explorera</h2><p>Les choix regroupent les alternatives possibles. Cliquez sur un groupe ou une étape pour modifier son fonctionnement et ses paramètres.</p></div></div>
        <div class="pipeline-flow-end"><q-icon name="table_chart" size="17px" /><span>Données du projet</span></div>
        <RecipeFlow v-if="pipelineRoot" :node="pipelineRoot" :path="pipelinePath" :selected-path="selectedPath" :components="components" :disabled="loading" @select="select" @add="openAdd" @move="move" @remove="requestRemove" />
        <div v-else class="pipeline-empty"><q-icon name="account_tree" size="32px" /><p>Créez le pipeline pour composer ses étapes et alternatives.</p><q-btn outline no-caps label="Créer le pipeline" :disable="loading" @click="createPipeline" /></div>
        <div class="pipeline-flow-end"><q-icon name="flag" size="17px" /><span>Modèle évalué</span></div>
        <div class="pipeline-flow-legend"><span><q-icon name="south" />À la suite</span><span><q-icon name="alt_route" />Un choix parmi plusieurs</span><span><q-icon name="tune" />Selon les données</span></div>
      </section>
      <section v-else-if="section !== 'options'" class="pipeline-analysis-list">
        <div class="pipeline-section-heading"><div><h2>{{ collectionTitles[section].title }}</h2><p>{{ collectionTitles[section].description }}</p></div><q-btn flat no-caps color="primary" icon="add" label="Ajouter" :disable="loading || !catalogue || !!collectionNode && !['collection', 'family'].includes(collectionNode.type)" @click="openAdd([section])" /></div>
        <div v-for="entry in collectionEntries" :key="JSON.stringify(entry.path)" class="pipeline-analysis-item" :class="{ 'pipeline-analysis-selected': JSON.stringify(selectedPath) === JSON.stringify(entry.path), 'pipeline-member-disabled': entry.removed }">
          <q-toggle v-if="entry.source === 'members'" :model-value="!entry.removed" :disable="loading || components.find(item => item.id === entry.node.component)?.available === false" :aria-label="`Inclure ${recipeLabel(entry.node, components)}`" @update:model-value="value => toggleCollectionMember(entry, value)" /><q-icon v-else name="extension" class="q-mx-sm" size="20px" />
          <button :disabled="loading || entry.removed" @click="select(entry.path)"><strong>{{ recipeLabel(entry.node, components) }}</strong><small>{{ displayServiceName(entry.node.component) || recipeSummary(entry.node) }}<template v-if="(entry.node.alias || entry.node.component) === options.main_metric"> · Critère principal</template></small></button>
          <q-btn v-if="entry.source === 'children'" flat round dense icon="close" :disable="loading" :aria-label="`Retirer ${recipeLabel(entry.node, components)}`" @click="requestRemove(entry.path)" /><q-btn v-else flat round dense icon="tune" :disable="loading || entry.removed" :aria-label="`Configurer ${recipeLabel(entry.node, components)}`" @click="select(entry.path)" />
        </div><p v-if="!collectionEntries.length" class="pipeline-help">Aucun composant explicite dans cette collection.</p>
      </section>

      <aside ref="inspectorElement" class="pipeline-inspector" aria-label="Réglages de l’étape">
        <template v-if="selectedNode && selectedNode !== collectionNode">
          <div class="pipeline-inspector-heading"><span class="pipeline-eyebrow">{{ section === 'pipeline' ? 'Étape sélectionnée' : 'Analyse sélectionnée' }}</span><h3>{{ recipeLabel(selectedNode, components) }}</h3><p>{{ recipeSummary(selectedNode) }}</p></div>
          <q-input :model-value="selectedNode.alias || ''" outlined dense label="Nom (facultatif)" :disable="loading" @update:model-value="rename" />
          <q-btn v-if="canDuplicate" flat no-caps color="primary" icon="content_copy" label="Dupliquer cette alternative" :disable="loading" class="q-mt-sm" @click="duplicateSelected" />
          <div v-if="section === 'pipeline' && (selectedNode.type === 'family' || ['choice', 'sequence', 'adaptive'].includes(selectedNode.type) && (selectedNode.children || selectedNode.recipe))" class="pipeline-inspector-section"><h4>Articulation des étapes</h4><q-select :model-value="selectedNode.type === 'family' ? selectedNode.mode || 'choice' : selectedNode.type" :options="modeChoices" emit-value map-options outlined label="Fonctionnement" :disable="loading" @update:model-value="setMode" /><p class="pipeline-help">{{ modeHelp[selectedNode.type === 'family' ? selectedNode.mode || 'choice' : selectedNode.type] }}</p></div>
          <q-toggle v-if="section === 'pipeline' && (selectedNode.type === 'choice' || selectedNode.type === 'family' && (selectedNode.mode || 'choice') === 'choice')" :model-value="selectedNode.optional" label="Autoriser un pipeline sans cette étape" :disable="loading" @update:model-value="setOptional" />
          <q-select v-if="section === 'pipeline' && selectedNode.type === 'use' && !selectedComponent?.capabilities?.includes('predict')" :model-value="selectedNode.condition" :options="selectedNode.condition && !conditionChoices.some(option => option.value === selectedNode.condition) ? [...conditionChoices, { label: String(selectedNode.condition), value: selectedNode.condition }] : conditionChoices" emit-value map-options outlined label="Quand proposer cette étape ?" :disable="loading" class="q-mt-md" @update:model-value="setCondition" />
          <div v-if="memberEntries.length" class="pipeline-inspector-section">
            <div class="pipeline-section-heading"><div><h4>{{ (selectedNode.mode || 'choice') === 'choice' ? 'Alternatives proposées' : 'Composants proposés' }}</h4><p>{{ memberEntries.filter(entry => !entry.removed).length }} sur {{ memberEntries.length }} inclus. DrIA filtre les composants compatibles avec votre objectif.</p></div></div>
            <q-input v-if="memberEntries.length > 5" v-model="memberSearch" outlined dense clearable label="Rechercher dans cette étape"><template #prepend><q-icon name="search" size="18px" /></template></q-input>
            <div class="pipeline-member-list"><div v-for="entry in shownMembers" :key="entry.node.component" class="pipeline-member" :class="{ 'pipeline-member-disabled': entry.removed }"><q-toggle :model-value="!entry.removed" :disable="loading || components.find(item => item.id === entry.node.component)?.available === false" :aria-label="`Inclure ${recipeLabel(entry.node, components)}`" @update:model-value="value => toggleMember(entry.index, value)" /><div><strong>{{ recipeLabel(entry.node, components) }}</strong><small>{{ components.find(item => item.id === entry.node.component)?.available === false ? 'Indisponible sur ce service' : displayServiceName(entry.node.component) }}</small></div><q-btn flat round dense icon="tune" :disable="loading || entry.removed" :aria-label="`Configurer ${recipeLabel(entry.node, components)}`" @click="select(entry.path)"><q-tooltip>Configurer ses paramètres</q-tooltip></q-btn></div><p v-if="!shownMembers.length" class="pipeline-help">Aucun composant ne correspond à cette recherche.</p></div>
          </div>
          <div v-if="selectedNode.type === 'family' && selectedNode.children?.length" class="pipeline-inspector-section"><h4>Alternatives ajoutées</h4><div v-for="(child, index) in selectedNode.children" :key="index" class="pipeline-extra"><button @click="select([...selectedPath, 'children', index])">{{ recipeLabel(child, components) }}<q-icon name="tune" /></button><q-btn flat round dense icon="close" :aria-label="`Retirer ${recipeLabel(child, components)}`" @click="requestRemove([...selectedPath, 'children', index])" /></div></div>
          <q-btn v-if="selectedNode.type === 'family'" flat no-caps color="primary" icon="add" label="Ajouter un composant à cette étape" :disable="loading || catalogueLoading || !catalogue" @click="openAdd(selectedPath)" />
          <div v-show="selectedNode.type === 'use' || selectedNode.params && Object.keys(selectedNode.params).length" class="pipeline-inspector-section"></div>
          <p v-if="selectedNode.type !== 'use' && !memberEntries.length" class="pipeline-help">Sélectionnez un composant pour modifier ses paramètres. Les flèches à côté des étapes changent leur ordre.</p>
        </template>
        <div v-else class="pipeline-empty"><q-icon name="touch_app" size="32px" /><h3>{{ section === 'pipeline' ? 'Choisissez une étape' : 'Choisissez une analyse' }}</h3><p>Sélectionnez un élément pour voir et ajuster ses paramètres.</p></div>
        <!-- Keep the editor mounted: invalid drafts must survive navigation. -->
        <RecipeParameters v-if="selectedNode" v-show="selectedNode.type === 'use' || selectedNode.params && Object.keys(selectedNode.params).length" ref="parameterEditor" :node="selectedNode" :catalogue-component="selectedComponent" :disabled="loading" @changed="changed()" @validity="parameterValidity" />
        <q-btn v-if="selectedNode && selectedPath.length > 2" flat no-caps color="primary" icon="arrow_back" label="Revenir au groupe" class="q-mt-md" @click="selectParent" />
      </aside>
    </div>

    <section v-if="section === 'options'" class="pipeline-settings">
      <div class="pipeline-section-heading"><div><h2>Guider la recherche et l’évaluation</h2><p>Ces réglages s’appliquent à l’ensemble du pipeline et de ses alternatives.</p></div></div>
      <div class="pipeline-settings-grid">
        <section class="pipeline-settings-card"><h3><q-icon name="timer" />Temps et objectif</h3><p v-if="guidedSearch" class="pipeline-help">La durée et le critère principal restent définis dans le questionnaire de création.</p><q-input v-if="!guidedSearch" :model-value="options.max_duration" outlined type="number" label="Budget de recherche (secondes)" :min="catalogue?.limits?.min_duration || 60" :max="catalogue?.limits?.max_duration || 3600" hint="La préparation et l’évaluation finale peuvent ajouter du temps." @update:model-value="updateBudget" /><q-select v-if="!guidedSearch" v-model="options.main_metric" :options="metricChoices" emit-value map-options clearable outlined label="Critère principal" hint="Vide : choix automatique parmi les scores configurés." @update:model-value="changed()" /><q-input v-model.number="options.max_stage_duration" outlined type="number" label="Temps maximal par étape (secondes)" min="1" :max="options.max_duration" hint="Doit rester inférieur ou égal au budget de recherche." @update:model-value="changed()" /></section>
        <section class="pipeline-settings-card"><h3><q-icon name="verified" />Modèles conservés</h3><p class="pipeline-help">DrIA réentraîne les meilleurs candidats et évalue leurs résultats.</p><q-input v-model.number="fit.n_candidates" outlined type="number" label="Nombre de modèles à conserver" min="1" max="100" @update:model-value="changed()" /><q-input v-model.number="fit.patience" outlined type="number" label="Générations sans amélioration avant l’arrêt" min="-1" max="10000" hint="Vide ou −1 : automatique. 0 : évaluation initiale uniquement." @update:model-value="optionalFitChanged" /><q-input v-model.number="fit.generation_sample_size" outlined type="number" label="Taille d’échantillon par génération" min="1" max="1000000" hint="Vide : réglage du service." @update:model-value="optionalFitChanged" /><q-toggle v-model="options.keep_training_history" label="Conserver l’historique de recherche" @update:model-value="changed()" /></section>
      </div>
      <q-expansion-item icon="settings" label="Méthode de recherche et validation interne" caption="Réglages avancés de l’optimisation et des partitions" class="pipeline-settings-advanced"><div class="pipeline-settings-grid"><section v-for="key in (['optimizer', 'splitter'] as const)" :key="key" class="pipeline-settings-card"><h3>{{ key === 'optimizer' ? 'Méthode de recherche' : 'Validation pendant la recherche' }}</h3><p v-if="key === 'splitter'" class="pipeline-help">Ces partitions comparent les candidats sur les données d’entraînement. Le jeu de test final reste réservé à son évaluation.</p><q-select :model-value="registryId(key)" :options="registryChoices(key)" emit-value map-options outlined :label="key === 'optimizer' ? 'Optimiseur' : 'Validation interne'" @update:model-value="id => updateRegistry(key, id)" /><q-input v-for="name in registryParams(key)" :key="name" :model-value="registryValue(key, name)" :type="typeof registryValue(key, name) === 'number' ? 'number' : 'text'" step="any" outlined :label="registryLabels[name] || displayServiceName(name)" @update:model-value="value => updateRegistryParam(key, name, value)" /></section></div></q-expansion-item>
      <q-expansion-item icon="data_object" label="Configuration complète" caption="Édition JSON pour les réglages spécifiques" class="pipeline-settings-advanced"><div class="pipeline-json-panel"><p class="pipeline-help">Les changements ne sont pris en compte qu’après leur application. Les ressources de calcul sont gérées par DrIA.</p><q-input v-model="jsonText" outlined type="textarea" input-class="pipeline-json-input" label="Configuration JSON" :error="!!jsonError" :error-message="jsonError" @update:model-value="jsonDirty = true" /><div class="pipeline-json-actions"><q-btn outline no-caps color="primary" label="Appliquer la configuration" :disable="!jsonDirty" @click="applyJson" /><q-btn flat no-caps label="Abandonner les modifications JSON" :disable="!jsonDirty && !jsonError" @click="discardJson" /></div></div></q-expansion-item>
    </section>

    <q-dialog v-model="addDialog"><q-card class="pipeline-dialog project-workspace-colors"><q-card-section class="pipeline-dialog-heading"><div><span class="pipeline-eyebrow">Composer le parcours</span><h3>{{ addKind === 'pipeline' ? 'Ajouter une étape' : 'Ajouter une analyse' }}</h3></div><q-btn flat round dense icon="close" v-close-popup aria-label="Fermer" /></q-card-section><q-card-section class="q-pt-none"><q-btn-toggle v-if="addKind === 'pipeline'" v-model="addType" no-caps unelevated spread toggle-color="primary" :options="[{ label: 'Famille', value: 'family' }, { label: 'Composant', value: 'component' }, { label: 'Groupe', value: 'group' }]" @update:model-value="addSelected = undefined" /><p class="pipeline-help">{{ addType === 'family' ? 'Une famille regroupe des alternatives que DrIA pourra comparer.' : addType === 'group' ? 'Un groupe définit la façon d’articuler plusieurs étapes. Vous pourrez ensuite le compléter.' : 'Un composant ajoute une méthode précise au parcours.' }}</p><q-input v-model="addSearch" outlined dense clearable label="Rechercher une étape"><template #prepend><q-icon name="search" /></template></q-input><div class="pipeline-add-results"><button v-for="item in addItems" :key="item.id" :class="{ 'pipeline-add-selected': addSelected === item.id }" @click="addSelected = item.id"><q-icon :name="addSelected === item.id ? 'radio_button_checked' : 'radio_button_unchecked'" /><span><strong>{{ item.label }}</strong><small>{{ item.caption }}</small></span></button><p v-if="!addItems.length" class="pipeline-help">Aucune étape ne correspond à cette recherche.</p></div></q-card-section><q-card-actions align="right"><q-btn flat no-caps label="Annuler" v-close-popup /><q-btn unelevated no-caps color="primary" label="Ajouter" :disable="!addSelected" @click="addNode" /></q-card-actions></q-card></q-dialog>
    <q-dialog v-model="removeDialog"><q-card class="pipeline-dialog pipeline-dialog-small project-workspace-colors"><q-card-section><h3>Retirer {{ removalLabel }} ?</h3><p>Cette étape et ses réglages seront retirés de la configuration. Vous pourrez annuler cette modification.</p></q-card-section><q-card-actions align="right"><q-btn flat no-caps label="Conserver" v-close-popup /><q-btn unelevated no-caps color="primary" label="Retirer" @click="removeNode" /></q-card-actions></q-card></q-dialog>
    <q-dialog v-model="resetDialog"><q-card class="pipeline-dialog pipeline-dialog-small project-workspace-colors"><q-card-section><h3>Rétablir la configuration de départ ?</h3><p>Les modifications de cette session seront abandonnées.</p></q-card-section><q-card-actions align="right"><q-btn flat no-caps label="Conserver mes réglages" v-close-popup /><q-btn unelevated no-caps color="primary" label="Rétablir" @click="reset" /></q-card-actions></q-card></q-dialog>
  </div>
</template>
