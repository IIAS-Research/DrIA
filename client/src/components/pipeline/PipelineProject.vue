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
  { key: 'metrics', label: 'Evaluation', icon: 'speed' },
  { key: 'statistics', label: 'Statistics', icon: 'table_chart' },
  { key: 'explanations', label: 'Explainability', icon: 'insights' },
  { key: 'options', label: 'Search', icon: 'tune' },
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
  if (!integer(options.value.max_duration, catalogue.value?.limits?.min_duration ?? 60, catalogue.value?.limits?.max_duration ?? 3600)) problems.push('The search budget must be between 60 and 3,600 seconds.');
  if (options.value.max_stage_duration != null && !integer(options.value.max_stage_duration, 1, options.value.max_duration)) problems.push('The time limit per step must not exceed the search budget.');
  if (fit.value.n_candidates != null && !integer(fit.value.n_candidates, 1, 100)) problems.push('Keep between 1 and 100 models.');
  if (fit.value.patience != null && !integer(fit.value.patience, -1, 10000)) problems.push('Patience must be an integer between −1 and 10,000.');
  if (fit.value.generation_sample_size != null && !integer(fit.value.generation_sample_size, 1, 1000000)) problems.push('Enter a positive integer sample size.');
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
  metrics: { title: 'How should models be compared?', description: 'These metrics evaluate models. Choose the primary metric in “Search”.' },
  statistics: { title: 'Which statistics should be computed?', description: 'Choose analyses for the data summary. DrIA applies those suited to each column type.' },
  explanations: { title: 'How can the results be understood?', description: 'These analyses explain predictions and help assess model performance.' },
};
const collectionNode = computed(() => section.value === 'options' || section.value === 'pipeline' ? undefined : study.value[section.value]);
const collectionEntries = computed(() => collectionNode.value ? recipeEntries(collectionNode.value, [section.value]) : []);
const conditionChoices = [
  { label: 'Always offer this step', value: undefined },
  { label: 'When values are missing', value: 'missing_values' },
];
const modeChoices = [
  { label: 'One of the alternatives', value: 'choice' },
  { label: 'Sequentially, in this order', value: 'sequence' },
  { label: 'Based on the data', value: 'adaptive' },
];
const modeHelp: Record<string, string> = {
  choice: 'DrIA compares alternatives and selects one for each pipeline.',
  sequence: 'All steps run in the displayed order. Use the arrows to change it.',
  adaptive: 'DrIA applies treatments compatible with the data. Steps in this group can run together.',
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
const registryLabels: Record<string, string> = { nb_folds: 'Number of folds', ratio: 'Validation fraction', random_state: 'Random seed', nb_candidate: 'Candidates per generation', mutation_power: 'Mutation strength', initial_modifier: 'Initial variations', max_iterations: 'Maximum iterations' };
function registry(key: 'optimizer' | 'splitter') { return catalogue.value?.[key === 'optimizer' ? 'optimizers' : 'splitters'] ?? {}; }
function registryChoices(key: 'optimizer' | 'splitter') {
  const names: Record<string, string> = { genetic: 'Genetic algorithm', bayesian: 'Bayesian optimization', random: key === 'optimizer' ? 'Random search' : 'Random split', kfold: 'Cross-validation' };
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
  else if (addType.value === 'family') items = (catalogue.value?.families ?? []).map((family: string) => ({ id: family, label: recipeLabel({ type: 'family', family }, components.value), caption: `${components.value.filter((component) => component.kind === addKind.value && component.available && component.families?.includes(family)).length} available components` })).filter((item: any) => !item.caption.startsWith('0 '));
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
const removalLabel = computed(() => { const node = recipeAt(study.value, removePath.value); return node ? recipeLabel(node, components.value) : 'this step'; });
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
  } catch { jsonError.value = 'This JSON configuration is invalid. Correct it or discard these changes.'; return false; }
}
function discardJson() { jsonDirty.value = false; jsonError.value = ''; jsonText.value = JSON.stringify(study.value, null, 2); }
function getConfiguration(): IPipeline | undefined {
  if (jsonDirty.value && !applyJson()) return undefined;
  if (!configurationValid.value) { error.value = 'Complete the highlighted settings before continuing.'; return undefined; }
  return study.value;
}
async function validateConfiguration(): Promise<IPipeline | undefined> {
  if (loading.value || parameterEditor.value && !await parameterEditor.value.validate()) return undefined;
  const configuration = getConfiguration(); if (!configuration) return undefined;
  loading.value = true; error.value = ''; emits('isPipelineLoading', true);
  try {
    const result = await validateStudy(cloneJson(configuration)); pipelineRefLocalComponent.value = editableStudy(result.configuration); invalidNodes.value = new Set(); changed();
    if (!recipeAt(study.value, selectedPath.value)) selectedPath.value = [section.value === 'options' ? 'pipeline' : section.value];
    feedback.value = 'The configuration is valid. DrIA can explore these pipelines.'; return study.value;
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
      <div><span class="pipeline-eyebrow">Configuration</span><strong>{{ modified ? 'Your changes' : 'Original configuration' }}</strong><span class="pipeline-status">{{ feedback || (modified ? 'Changes applied to this configuration.' : 'Select a group or step to configure it.') }}</span></div>
      <div class="pipeline-toolbar-actions"><q-btn flat no-caps icon="undo" label="Cancel" :disable="!history.length || loading || jsonDirty" @click="undo" /><q-btn flat no-caps icon="restart_alt" label="Restore" :disable="!modified || loading" @click="resetDialog = true" /><q-btn outline no-caps color="primary" icon="check" label="Validate" :loading="loading" :disable="!configurationValid" @click="validateConfiguration" /></div>
    </div>
    <q-tabs v-model="section" align="left" no-caps active-color="primary" indicator-color="primary" class="pipeline-tabs" outside-arrows mobile-arrows><q-tab v-for="item in sections" :key="item.key" :name="item.key" :label="item.label" :icon="item.icon" /></q-tabs>
    <div v-if="catalogueError" class="pipeline-notice"><q-icon name="cloud_off" /><div>Could not load the catalog. Existing steps are still available to view.<small>{{ catalogueError }}</small></div><q-btn flat no-caps label="Retry" @click="loadCatalogue" /></div>
    <div v-if="checks.length || settingsErrors.length || activeInvalidNodes.length || jsonDirty" class="pipeline-notice pipeline-notice-warning" role="status"><q-icon name="edit_note" /><div><strong>Some settings need attention</strong><ul><li v-for="message in [...checks, ...settingsErrors]" :key="message">{{ message }}</li><li v-if="activeInvalidNodes.length">Parameters to correct: <q-btn v-for="node in activeInvalidNodes" :key="recipeLabel(node, components)" flat dense no-caps color="primary" :label="recipeLabel(node, components)" @click="revealInvalid(node)" /></li><li v-if="jsonDirty">Apply or discard your JSON configuration changes.</li></ul></div></div>
    <div v-if="error" class="pipeline-notice pipeline-notice-error" role="alert"><q-icon name="info_outline" /><div>{{ error }}</div></div>

    <div v-show="section !== 'options'" class="pipeline-workbench">
      <section v-if="section === 'pipeline'" class="pipeline-canvas" aria-label="Pipeline steps and alternatives">
        <div class="pipeline-section-heading"><div><h2>The pipeline DrIA will explore</h2><p>Choices group possible alternatives. Select a group or step to change how it works and adjust its parameters.</p></div></div>
        <div class="pipeline-flow-end"><q-icon name="table_chart" size="17px" /><span>Project data</span></div>
        <RecipeFlow v-if="pipelineRoot" :node="pipelineRoot" :path="pipelinePath" :selected-path="selectedPath" :components="components" :disabled="loading" @select="select" @add="openAdd" @move="move" @remove="requestRemove" />
        <div v-else class="pipeline-empty"><q-icon name="account_tree" size="32px" /><p>Create a pipeline to build its steps and alternatives.</p><q-btn outline no-caps label="Create pipeline" :disable="loading" @click="createPipeline" /></div>
        <div class="pipeline-flow-end"><q-icon name="flag" size="17px" /><span>Evaluated model</span></div>
        <div class="pipeline-flow-legend"><span><q-icon name="south" />Sequentially</span><span><q-icon name="alt_route" />One of several options</span><span><q-icon name="tune" />Based on the data</span></div>
      </section>
      <section v-else-if="section !== 'options'" class="pipeline-analysis-list">
        <div class="pipeline-section-heading"><div><h2>{{ collectionTitles[section].title }}</h2><p>{{ collectionTitles[section].description }}</p></div><q-btn flat no-caps color="primary" icon="add" label="Add" :disable="loading || !catalogue || !!collectionNode && !['collection', 'family'].includes(collectionNode.type)" @click="openAdd([section])" /></div>
        <div v-for="entry in collectionEntries" :key="JSON.stringify(entry.path)" class="pipeline-analysis-item" :class="{ 'pipeline-analysis-selected': JSON.stringify(selectedPath) === JSON.stringify(entry.path), 'pipeline-member-disabled': entry.removed }">
          <q-toggle v-if="entry.source === 'members'" :model-value="!entry.removed" :disable="loading || components.find(item => item.id === entry.node.component)?.available === false" :aria-label="`Include ${recipeLabel(entry.node, components)}`" @update:model-value="value => toggleCollectionMember(entry, value)" /><q-icon v-else name="extension" class="q-mx-sm" size="20px" />
          <button :disabled="loading || entry.removed" @click="select(entry.path)"><strong>{{ recipeLabel(entry.node, components) }}</strong><small>{{ displayServiceName(entry.node.component) || recipeSummary(entry.node) }}<template v-if="(entry.node.alias || entry.node.component) === options.main_metric"> · Primary metric</template></small></button>
          <q-btn v-if="entry.source === 'children'" flat round dense icon="close" :disable="loading" :aria-label="`Retirer ${recipeLabel(entry.node, components)}`" @click="requestRemove(entry.path)" /><q-btn v-else flat round dense icon="tune" :disable="loading || entry.removed" :aria-label="`Configurer ${recipeLabel(entry.node, components)}`" @click="select(entry.path)" />
        </div><p v-if="!collectionEntries.length" class="pipeline-help">No explicit components in this collection.</p>
      </section>

      <aside ref="inspectorElement" class="pipeline-inspector" aria-label="Step settings">
        <template v-if="selectedNode && selectedNode !== collectionNode">
          <div class="pipeline-inspector-heading"><span class="pipeline-eyebrow">{{ section === 'pipeline' ? 'Selected step' : 'Selected analysis' }}</span><h3>{{ recipeLabel(selectedNode, components) }}</h3><p>{{ recipeSummary(selectedNode) }}</p></div>
          <q-input :model-value="selectedNode.alias || ''" outlined dense label="Name (optional)" :disable="loading" @update:model-value="rename" />
          <q-btn v-if="canDuplicate" flat no-caps color="primary" icon="content_copy" label="Duplicate this alternative" :disable="loading" class="q-mt-sm" @click="duplicateSelected" />
          <div v-if="section === 'pipeline' && (selectedNode.type === 'family' || ['choice', 'sequence', 'adaptive'].includes(selectedNode.type) && (selectedNode.children || selectedNode.recipe))" class="pipeline-inspector-section"><h4>How steps connect</h4><q-select :model-value="selectedNode.type === 'family' ? selectedNode.mode || 'choice' : selectedNode.type" :options="modeChoices" emit-value map-options outlined label="Behavior" :disable="loading" @update:model-value="setMode" /><p class="pipeline-help">{{ modeHelp[selectedNode.type === 'family' ? selectedNode.mode || 'choice' : selectedNode.type] }}</p></div>
          <q-toggle v-if="section === 'pipeline' && (selectedNode.type === 'choice' || selectedNode.type === 'family' && (selectedNode.mode || 'choice') === 'choice')" :model-value="selectedNode.optional" label="Allow a pipeline without this step" :disable="loading" @update:model-value="setOptional" />
          <q-select v-if="section === 'pipeline' && selectedNode.type === 'use' && !selectedComponent?.capabilities?.includes('predict')" :model-value="selectedNode.condition" :options="selectedNode.condition && !conditionChoices.some(option => option.value === selectedNode.condition) ? [...conditionChoices, { label: String(selectedNode.condition), value: selectedNode.condition }] : conditionChoices" emit-value map-options outlined label="When should this step be offered?" :disable="loading" class="q-mt-md" @update:model-value="setCondition" />
          <div v-if="memberEntries.length" class="pipeline-inspector-section">
            <div class="pipeline-section-heading"><div><h4>{{ (selectedNode.mode || 'choice') === 'choice' ? 'Available alternatives' : 'Available components' }}</h4><p>{{ memberEntries.filter(entry => !entry.removed).length }} of {{ memberEntries.length }} included. DrIA filters components compatible with your task.</p></div></div>
            <q-input v-if="memberEntries.length > 5" v-model="memberSearch" outlined dense clearable label="Search this step"><template #prepend><q-icon name="search" size="18px" /></template></q-input>
            <div class="pipeline-member-list"><div v-for="entry in shownMembers" :key="entry.node.component" class="pipeline-member" :class="{ 'pipeline-member-disabled': entry.removed }"><q-toggle :model-value="!entry.removed" :disable="loading || components.find(item => item.id === entry.node.component)?.available === false" :aria-label="`Include ${recipeLabel(entry.node, components)}`" @update:model-value="value => toggleMember(entry.index, value)" /><div><strong>{{ recipeLabel(entry.node, components) }}</strong><small>{{ components.find(item => item.id === entry.node.component)?.available === false ? 'Unavailable on this service' : displayServiceName(entry.node.component) }}</small></div><q-btn flat round dense icon="tune" :disable="loading || entry.removed" :aria-label="`Configurer ${recipeLabel(entry.node, components)}`" @click="select(entry.path)"><q-tooltip>Configure parameters</q-tooltip></q-btn></div><p v-if="!shownMembers.length" class="pipeline-help">No components match this search.</p></div>
          </div>
          <div v-if="selectedNode.type === 'family' && selectedNode.children?.length" class="pipeline-inspector-section"><h4>Added alternatives</h4><div v-for="(child, index) in selectedNode.children" :key="index" class="pipeline-extra"><button @click="select([...selectedPath, 'children', index])">{{ recipeLabel(child, components) }}<q-icon name="tune" /></button><q-btn flat round dense icon="close" :aria-label="`Retirer ${recipeLabel(child, components)}`" @click="requestRemove([...selectedPath, 'children', index])" /></div></div>
          <q-btn v-if="selectedNode.type === 'family'" flat no-caps color="primary" icon="add" label="Add a component to this step" :disable="loading || catalogueLoading || !catalogue" @click="openAdd(selectedPath)" />
          <div v-show="selectedNode.type === 'use' || selectedNode.params && Object.keys(selectedNode.params).length" class="pipeline-inspector-section"></div>
          <p v-if="selectedNode.type !== 'use' && !memberEntries.length" class="pipeline-help">Select a component to edit its parameters. Use the arrows beside the steps to change their order.</p>
        </template>
        <div v-else class="pipeline-empty"><q-icon name="touch_app" size="32px" /><h3>{{ section === 'pipeline' ? 'Choose a step' : 'Choose an analysis' }}</h3><p>Select an item to view and adjust its parameters.</p></div>
        <!-- Keep the editor mounted: invalid drafts must survive navigation. -->
        <RecipeParameters v-if="selectedNode" v-show="selectedNode.type === 'use' || selectedNode.params && Object.keys(selectedNode.params).length" ref="parameterEditor" :node="selectedNode" :catalogue-component="selectedComponent" :disabled="loading" @changed="changed()" @validity="parameterValidity" />
        <q-btn v-if="selectedNode && selectedPath.length > 2" flat no-caps color="primary" icon="arrow_back" label="Back to group" class="q-mt-md" @click="selectParent" />
      </aside>
    </div>

    <section v-if="section === 'options'" class="pipeline-settings">
      <div class="pipeline-section-heading"><div><h2>Guide the search and evaluation</h2><p>These settings apply to the entire pipeline and its alternatives.</p></div></div>
      <div class="pipeline-settings-grid">
        <section class="pipeline-settings-card"><h3><q-icon name="timer" />Time and objective</h3><p v-if="guidedSearch" class="pipeline-help">Duration and primary metric are set in the project creation questions.</p><q-input v-if="!guidedSearch" :model-value="options.max_duration" outlined type="number" label="Search budget (seconds)" :min="catalogue?.limits?.min_duration || 60" :max="catalogue?.limits?.max_duration || 3600" hint="Preparation and final evaluation may take additional time." @update:model-value="updateBudget" /><q-select v-if="!guidedSearch" v-model="options.main_metric" :options="metricChoices" emit-value map-options clearable outlined label="Primary metric" hint="Leave empty to choose automatically from the configured metrics." @update:model-value="changed()" /><q-input v-model.number="options.max_stage_duration" outlined type="number" label="Time limit per step (seconds)" min="1" :max="options.max_duration" hint="Must not exceed the search budget." @update:model-value="changed()" /></section>
        <section class="pipeline-settings-card"><h3><q-icon name="verified" />Models to keep</h3><p class="pipeline-help">DrIA retrains the best candidates and evaluates their results.</p><q-input v-model.number="fit.n_candidates" outlined type="number" label="Number of models to keep" min="1" max="100" @update:model-value="changed()" /><q-input v-model.number="fit.patience" outlined type="number" label="Generations without improvement before stopping" min="-1" max="10000" hint="Empty or −1: automatic. 0: initial evaluation only." @update:model-value="optionalFitChanged" /><q-input v-model.number="fit.generation_sample_size" outlined type="number" label="Sample size per generation" min="1" max="1000000" hint="Empty: service default." @update:model-value="optionalFitChanged" /><q-toggle v-model="options.keep_training_history" label="Keep search history" @update:model-value="changed()" /></section>
      </div>
      <q-expansion-item icon="settings" label="Search method and internal validation" caption="Advanced optimization and splitting settings" class="pipeline-settings-advanced"><div class="pipeline-settings-grid"><section v-for="key in (['optimizer', 'splitter'] as const)" :key="key" class="pipeline-settings-card"><h3>{{ key === 'optimizer' ? 'Search method' : 'Validation during search' }}</h3><p v-if="key === 'splitter'" class="pipeline-help">These splits compare candidates on the training data. The final test set is reserved for evaluation.</p><q-select :model-value="registryId(key)" :options="registryChoices(key)" emit-value map-options outlined :label="key === 'optimizer' ? 'Optimizer' : 'Internal validation'" @update:model-value="id => updateRegistry(key, id)" /><q-input v-for="name in registryParams(key)" :key="name" :model-value="registryValue(key, name)" :type="typeof registryValue(key, name) === 'number' ? 'number' : 'text'" step="any" outlined :label="registryLabels[name] || displayServiceName(name)" @update:model-value="value => updateRegistryParam(key, name, value)" /></section></div></q-expansion-item>
      <q-expansion-item icon="data_object" label="Full configuration" caption="JSON editing for specific settings" class="pipeline-settings-advanced"><div class="pipeline-json-panel"><p class="pipeline-help">Changes take effect only after they are applied. DrIA manages computing resources.</p><q-input v-model="jsonText" outlined type="textarea" input-class="pipeline-json-input" label="Configuration JSON" :error="!!jsonError" :error-message="jsonError" @update:model-value="jsonDirty = true" /><div class="pipeline-json-actions"><q-btn outline no-caps color="primary" label="Apply configuration" :disable="!jsonDirty" @click="applyJson" /><q-btn flat no-caps label="Discard JSON changes" :disable="!jsonDirty && !jsonError" @click="discardJson" /></div></div></q-expansion-item>
    </section>

    <q-dialog v-model="addDialog"><q-card class="pipeline-dialog project-workspace-colors"><q-card-section class="pipeline-dialog-heading"><div><span class="pipeline-eyebrow">Build the pipeline</span><h3>{{ addKind === 'pipeline' ? 'Add step' : 'Add analysis' }}</h3></div><q-btn flat round dense icon="close" v-close-popup aria-label="Close" /></q-card-section><q-card-section class="q-pt-none"><q-btn-toggle v-if="addKind === 'pipeline'" v-model="addType" no-caps unelevated spread toggle-color="primary" :options="[{ label: 'Family', value: 'family' }, { label: 'Component', value: 'component' }, { label: 'Group', value: 'group' }]" @update:model-value="addSelected = undefined" /><p class="pipeline-help">{{ addType === 'family' ? 'A family groups alternatives that DrIA can compare.' : addType === 'group' ? 'A group defines how several steps connect. You can add steps to it afterward.' : 'A component adds a specific method to the pipeline.' }}</p><q-input v-model="addSearch" outlined dense clearable label="Search steps"><template #prepend><q-icon name="search" /></template></q-input><div class="pipeline-add-results"><button v-for="item in addItems" :key="item.id" :class="{ 'pipeline-add-selected': addSelected === item.id }" @click="addSelected = item.id"><q-icon :name="addSelected === item.id ? 'radio_button_checked' : 'radio_button_unchecked'" /><span><strong>{{ item.label }}</strong><small>{{ item.caption }}</small></span></button><p v-if="!addItems.length" class="pipeline-help">No steps match this search.</p></div></q-card-section><q-card-actions align="right"><q-btn flat no-caps label="Cancel" v-close-popup /><q-btn unelevated no-caps color="primary" label="Add" :disable="!addSelected" @click="addNode" /></q-card-actions></q-card></q-dialog>
    <q-dialog v-model="removeDialog"><q-card class="pipeline-dialog pipeline-dialog-small project-workspace-colors"><q-card-section><h3>Remove {{ removalLabel }}?</h3><p>This step and its settings will be removed from the configuration. You can undo this change.</p></q-card-section><q-card-actions align="right"><q-btn flat no-caps label="Keep" v-close-popup /><q-btn unelevated no-caps color="primary" label="Remove" @click="removeNode" /></q-card-actions></q-card></q-dialog>
    <q-dialog v-model="resetDialog"><q-card class="pipeline-dialog pipeline-dialog-small project-workspace-colors"><q-card-section><h3>Restore the original configuration?</h3><p>Changes made in this session will be discarded.</p></q-card-section><q-card-actions align="right"><q-btn flat no-caps label="Keep my settings" v-close-popup /><q-btn unelevated no-caps color="primary" label="Restore" @click="reset" /></q-card-actions></q-card></q-dialog>
  </div>
</template>
