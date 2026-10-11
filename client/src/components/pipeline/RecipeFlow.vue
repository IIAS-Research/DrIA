<script setup lang="ts">
import { computed, ref, watch } from 'vue';
import { IRecipe } from '../models';
import { CatalogueComponent, RecipePath, recipeLabel, recipeSummary } from 'src/utils/pipeline-editor';

const props = withDefaults(defineProps<{
  node: IRecipe;
  path: RecipePath;
  selectedPath: RecipePath;
  components: CatalogueComponent[];
  disabled?: boolean;
  readonly?: boolean;
  retained?: boolean;
  depth?: number;
  initialExpandedDepth?: number;
}>(), { depth: 0, disabled: false, readonly: false, retained: false });
const emit = defineEmits<{
  (event: 'select', path: RecipePath): void;
  (event: 'add', path: RecipePath): void;
  (event: 'move', path: RecipePath, direction: number): void;
  (event: 'remove', path: RecipePath): void;
}>();
const selected = computed(() => JSON.stringify(props.path) === JSON.stringify(props.selectedPath));
const mode = computed(() => props.node.type === 'family' ? props.node.mode || 'choice' : props.node.type);
const relation = computed(() => ({ sequence: 'Puis', choice: 'Ou', adaptive: 'Selon les données', collection: 'Et' }[mode.value] || 'Puis'));
const hasChildren = computed(() => ['sequence', 'choice', 'adaptive', 'collection', 'optional'].includes(props.node.type) || Boolean(props.node.children?.length));
const collapsible = computed(() => Boolean(props.node.children?.length || props.node.recipe));
const expanded = ref(props.initialExpandedDepth === undefined || props.depth < props.initialExpandedDepth);
const childKeys = new WeakMap<IRecipe, number>();
let nextChildKey = 0;
function childKey(node: IRecipe): number {
  if (!childKeys.has(node)) childKeys.set(node, nextChildKey++);
  return childKeys.get(node)!;
}
const summary = computed(() => {
  if (!props.retained || props.node.type !== 'use') return recipeSummary(props.node);
  const count = Object.keys(props.node.params ?? {}).length;
  return count ? `${count} paramètre${count > 1 ? 's retenus' : ' retenu'}` : 'Paramètres non renseignés';
});
const icons: Record<string, string> = { sequence: 'account_tree', choice: 'alt_route', adaptive: 'tune', family: 'layers', use: 'extension', collection: 'checklist' };
watch(() => JSON.stringify(props.selectedPath), () => {
  if (props.selectedPath.length > props.path.length
    && props.path.every((part, index) => part === props.selectedPath[index])) expanded.value = true;
});
</script>

<template>
  <div class="recipe-flow" :class="{ 'recipe-flow-nested': depth > 0 }">
    <div class="flow-node-heading">
      <button class="flow-node" :class="{ 'flow-node-selected': selected, 'flow-node-collapsible': collapsible }" :disabled="disabled" :aria-pressed="selected" @click="emit('select', path)">
        <span class="flow-node-icon"><q-icon :name="icons[node.type] || 'extension'" size="20px" /></span>
        <span class="flow-node-copy">
          <strong>{{ recipeLabel(node, components) }}</strong>
          <small>{{ summary }}</small>
        </span>
        <span v-if="node.optional" class="flow-tag">Facultatif</span>
        <q-icon v-if="!collapsible" name="chevron_right" size="19px" class="flow-node-chevron" />
      </button>
      <q-btn v-if="collapsible" flat round dense type="button" class="flow-fold" :icon="expanded ? 'expand_less' : 'expand_more'" :aria-expanded="expanded" :aria-label="`${expanded ? 'Replier' : 'Déplier'} ${recipeLabel(node, components)}`" @click.stop="expanded = !expanded"><q-tooltip>{{ expanded ? 'Replier les sous-étapes' : 'Déplier les sous-étapes' }}</q-tooltip></q-btn>
    </div>
    <div v-if="hasChildren" v-show="expanded" class="flow-children" :class="`flow-children-${mode}`">
      <div v-for="(child, index) in node.children || []" :key="childKey(child)" class="flow-child">
        <div class="flow-connector"><span>{{ mode === 'sequence' ? `${index + 1}` : relation }}</span></div>
        <div class="flow-child-body">
          <RecipeFlow :node="child" :path="[...path, 'children', index]" :selected-path="selectedPath" :components="components" :disabled="disabled" :readonly="readonly" :retained="retained" :depth="depth + 1" :initial-expanded-depth="initialExpandedDepth"
            @select="emit('select', $event)" @add="emit('add', $event)" @move="(p, direction) => emit('move', p, direction)" @remove="emit('remove', $event)" />
          <div v-if="!readonly" class="flow-child-actions">
            <q-btn v-if="mode === 'sequence' || mode === 'adaptive'" flat round dense size="sm" icon="arrow_upward" :disable="disabled || index === 0" :aria-label="`Monter ${recipeLabel(child, components)}`" @click="emit('move', [...path, 'children', index], -1)"><q-tooltip>Monter l’étape</q-tooltip></q-btn>
            <q-btn v-if="mode === 'sequence' || mode === 'adaptive'" flat round dense size="sm" icon="arrow_downward" :disable="disabled || index === (node.children?.length || 0) - 1" :aria-label="`Descendre ${recipeLabel(child, components)}`" @click="emit('move', [...path, 'children', index], 1)"><q-tooltip>Descendre l’étape</q-tooltip></q-btn>
            <q-btn flat round dense size="sm" icon="close" :disable="disabled" :aria-label="`Retirer ${recipeLabel(child, components)}`" @click="emit('remove', [...path, 'children', index])"><q-tooltip>Retirer l’étape</q-tooltip></q-btn>
          </div>
        </div>
      </div>
      <q-btn v-if="!readonly" flat no-caps icon="add" :label="mode === 'choice' ? 'Ajouter une alternative' : 'Ajouter une étape'" :disable="disabled" class="flow-add" @click="emit('add', path)" />
    </div>
    <div v-if="node.recipe" v-show="expanded" class="flow-adaptive-recipe">
      <span class="flow-relation">Selon les données</span>
      <RecipeFlow :node="node.recipe" :path="[...path, 'recipe']" :selected-path="selectedPath" :components="components" :disabled="disabled" :readonly="readonly" :retained="retained" :depth="depth + 1" :initial-expanded-depth="initialExpandedDepth" @select="emit('select', $event)" @add="emit('add', $event)" @move="(p, direction) => emit('move', p, direction)" @remove="emit('remove', $event)" />
    </div>
  </div>
</template>

<style scoped>
.recipe-flow { min-width: 0; }
.flow-node-heading { position: relative; }
.flow-node { display: flex; width: 100%; align-items: center; gap: 12px; padding: 13px 14px; border: 1px solid var(--project-border); border-radius: 10px; background: var(--project-surface); color: var(--project-ink); text-align: left; font: inherit; cursor: pointer; transition: border-color .15s, background .15s; }
.flow-node:hover { border-color: var(--project-brand); }
.flow-node:focus-visible { outline: 2px solid var(--q-primary); outline-offset: 3px; }
.flow-node-selected { border-color: var(--q-primary); background: var(--project-selection-surface); }
.flow-node-collapsible { padding-right: 54px; }
.flow-fold { position: absolute; right: 9px; top: 50%; transform: translateY(-50%); color: var(--project-muted); }
.flow-node:disabled { cursor: default; opacity: .7; }
.flow-node-icon { display: flex; flex-shrink: 0; align-items: center; justify-content: center; width: 34px; height: 34px; border-radius: 9px; color: var(--q-primary); background: var(--project-tint); }
.flow-node-copy { display: flex; flex: 1; min-width: 0; flex-direction: column; gap: 4px; }
.flow-node-copy strong { font-size: 13px; font-weight: 600; overflow-wrap: anywhere; }
.flow-node-copy small { color: var(--project-muted); font-size: 11px; line-height: 1.5; overflow-wrap: anywhere; }
.flow-node-chevron { color: var(--project-muted); flex-shrink: 0; }
.flow-tag { font-size: 10px; color: var(--project-muted); background: var(--project-paper); padding: 4px 7px; border-radius: 5px; }
.flow-children { padding: 8px 0 0 12px; }
.flow-child { display: flex; gap: 10px; position: relative; }
.flow-connector { flex-shrink: 0; width: 26px; display: flex; justify-content: center; padding-top: 16px; position: relative; color: var(--project-muted); font-size: 10px; }
.flow-connector::before { content: ''; width: 1px; background: var(--project-border); position: absolute; top: 0; bottom: 0; left: 50%; }
.flow-connector span { z-index: 1; background: var(--project-paper); border: 1px solid var(--project-border); min-width: 22px; height: 22px; display: flex; align-items: center; justify-content: center; border-radius: 50%; }
.flow-children-choice .flow-connector, .flow-children-adaptive .flow-connector { width: 48px; }
.flow-children-choice .flow-connector span, .flow-children-adaptive .flow-connector span { border-radius: 5px; height: auto; padding: 3px 5px; text-align: center; line-height: 1.4; }
.flow-child-body { flex: 1; min-width: 0; padding: 5px 0 8px; }
.flow-child-actions { display: flex; justify-content: flex-end; color: var(--project-muted); gap: 3px; margin: 4px 0 -3px; }
.flow-add { margin: 2px 0 0 36px; color: var(--q-primary); font-size: 12px; }
.flow-adaptive-recipe { margin: 10px 0 0 20px; padding-left: 12px; border-left: 1px solid var(--project-border); }
.flow-relation { display: block; color: var(--project-muted); margin-bottom: 7px; font-size: 11px; }
@media(max-width: 599px) { .flow-node { padding: 11px 10px; gap: 8px; } .flow-node-collapsible { padding-right: 48px; } .flow-fold { right: 5px; } .flow-node-icon { width: 28px; height: 28px; } .flow-tag { display: none; } .flow-children { padding-left: 0; } .flow-child { gap: 7px; } .flow-connector { width: 20px; } }
</style>
