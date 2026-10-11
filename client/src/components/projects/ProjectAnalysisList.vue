<script setup lang="ts">
import { computed } from 'vue';
import type { IRecipe } from '../models';
import { recipeEntries, recipeLabel } from 'src/utils/pipeline-editor';
import type { RecipePath } from 'src/utils/pipeline-editor';
import { displayServiceName } from 'src/utils/service-label';

const props = defineProps<{ node: IRecipe }>();

interface AnalysisEntry {
  key: string;
  node: IRecipe;
  label: string;
  parameters: [string, unknown][];
  context: string[];
  removed: boolean;
  group: boolean;
}

function display(value: unknown): string {
  if (value === undefined) return 'Non renseigné';
  if (value === null) return 'Aucune valeur';
  if (typeof value === 'boolean') return value ? 'Oui' : 'Non';
  return typeof value === 'object' ? JSON.stringify(value, null, 2) : String(value);
}

function condition(value: unknown): string {
  return typeof value === 'string' ? displayServiceName(value) : display(value);
}

function parameterValue(parameter: unknown): unknown {
  if (!parameter || typeof parameter !== 'object' || Array.isArray(parameter)) return parameter;
  const value = parameter as Record<string, unknown>;
  if (['parameter', 'const'].includes(String(value.type))) return value.value;
  if (['int', 'float', 'categorical'].includes(String(value.type))) return value.initial;
  return parameter;
}

function parameterScope(parameter: unknown): string {
  if (!parameter || typeof parameter !== 'object' || Array.isArray(parameter)) return '';
  const value = parameter as Record<string, unknown>;
  const categorical = value.categorical ?? (value.type === 'categorical' ? value.values : undefined);
  const domain = value.domain ?? (['int', 'float'].includes(String(value.type)) ? [value.low, value.high] : undefined);
  if (Array.isArray(categorical) && categorical.length) return `Choix possibles : ${categorical.map(display).join(', ')}`;
  if (Array.isArray(domain) && domain.length) return `Intervalle : ${domain.map(display).join(' à ')}`;
  if (value.fixed === true || value.type === 'const') return 'Valeur fixée';
  if (value.optimizable === true) return 'Paramètre ajusté pendant la recherche';
  return '';
}

const analyses = computed(() => {
  const result: AnalysisEntry[] = [];
  const ancestors = new Set<IRecipe>();

  function visit(node: IRecipe, path: RecipePath, inherited: string[], inheritedRemoved: boolean): void {
    if (!node || typeof node !== 'object' || ancestors.has(node)) return;
    ancestors.add(node);

    const label = recipeLabel(node);
    const entries = recipeEntries(node, path);
    const parameters = Object.entries(node.params ?? {});
    const mode = node.type === 'family' ? node.mode ?? 'choice' : node.type;
    const context = [...inherited];
    if (node.optional || mode === 'optional') context.push(`Facultatif · ${label}`);
    if (mode === 'choice') context.push(`Choix automatique · ${label}`);
    if (mode === 'adaptive') context.push(`Selon les données · ${label}`);
    if (node.condition !== undefined && node.condition !== null && node.condition !== '') {
      context.push(`Condition : ${condition(node.condition)}`);
    }
    if (Array.isArray(node.exclude) && node.exclude.length) {
      // Resolved exclusions already have their own removed item. Retain only
      // exclusions absent from the stored members, without repeating all of them.
      const unresolved = node.exclude.filter((id: unknown) => !entries.some((entry) =>
        (entry.member?.component ?? entry.node.component) === id));
      if (unresolved.length) context.push(`Composants exclus : ${unresolved.map((id: unknown) => typeof id === 'string' ? displayServiceName(id) : display(id)).join(', ')}`);
    }

    const removed = inheritedRemoved || node.removed === true;
    // Linear wrappers have no separate card. Preserve groups with their own
    // settings, unresolved families, and every component in traversal order.
    if (node.component || node.type === 'use' || parameters.length || !entries.length) {
      result.push({
        key: JSON.stringify(path), node, label, parameters,
        context: [...new Set(context)], removed,
        group: Boolean(entries.length && !node.component && node.type !== 'use'),
      });
    }
    entries.forEach((entry) => visit(entry.node, entry.path, context, removed || entry.removed));
    ancestors.delete(node);
  }

  visit(props.node, [], [], false);
  return result;
});
</script>

<template>
  <ul class="analysis-list" aria-label="Composants et paramètres de l’analyse">
    <li
      v-for="analysis in analyses"
      :key="analysis.key"
      class="analysis-item"
      :class="{ 'analysis-item--removed': analysis.removed }"
    >
      <div class="analysis-heading">
        <span class="analysis-icon" aria-hidden="true"><q-icon :name="analysis.group ? 'tune' : 'extension'" size="18px" /></span>
        <div class="analysis-heading-copy">
          <h3 class="analysis-title">{{ analysis.label }}</h3>
          <p v-if="typeof analysis.node.description === 'string' && analysis.node.description.trim()" class="analysis-description">{{ analysis.node.description }}</p>
        </div>
        <span v-if="analysis.removed" class="analysis-status">Retiré de la recherche</span>
        <span v-else-if="analysis.group" class="analysis-status">Réglages du groupe</span>
      </div>

      <div v-if="analysis.context.length" class="analysis-context">
        <span v-for="context in analysis.context" :key="context" class="analysis-context-tag">{{ context }}</span>
      </div>

      <dl v-if="analysis.parameters.length" class="analysis-parameters">
        <div v-for="[key, parameter] in analysis.parameters" :key="key" class="analysis-parameter">
          <dt>{{ displayServiceName(key) }}</dt>
          <dd>
            <span class="analysis-parameter-value">{{ display(parameterValue(parameter)) }}</span>
            <small v-if="parameterScope(parameter)" class="analysis-parameter-scope">{{ parameterScope(parameter) }}</small>
          </dd>
        </div>
      </dl>
    </li>
  </ul>
</template>

<style scoped>
.analysis-list { display: grid; gap: 12px; padding: 0; margin: 0; list-style: none; min-width: 0; }
.analysis-item { padding: 15px 17px; border: 1px solid var(--project-border); border-radius: 10px; background: var(--project-surface); color: var(--project-ink); min-width: 0; }
.analysis-item--removed { background: var(--project-paper); }
.analysis-item--removed .analysis-title, .analysis-item--removed .analysis-icon { color: var(--project-muted); }
.analysis-heading { display: flex; align-items: flex-start; gap: 10px; min-width: 0; }
.analysis-icon { display: flex; align-items: center; justify-content: center; flex-shrink: 0; width: 30px; height: 30px; border-radius: 8px; color: var(--q-primary); background: var(--project-tint); }
.analysis-heading-copy { flex: 1; min-width: 0; padding-top: 4px; }
.analysis-title { margin: 0; font-size: 13px; line-height: 1.65; font-weight: 600; overflow-wrap: anywhere; }
.analysis-description { margin: 4px 0 0; color: var(--project-muted); font-size: 12px; line-height: 1.65; overflow-wrap: anywhere; }
.analysis-status { margin-top: 4px; padding: 4px 7px; border-radius: 5px; background: var(--project-paper); color: var(--project-muted); font-size: 10px; line-height: 1.5; max-width: 45%; overflow-wrap: anywhere; }
.analysis-context { display: flex; flex-wrap: wrap; gap: 6px; margin: 10px 0 0; min-width: 0; }
.analysis-context-tag { padding: 4px 7px; border: 1px solid var(--project-border); border-radius: 5px; color: var(--project-muted); font-size: 11px; line-height: 1.6; white-space: pre-wrap; overflow-wrap: anywhere; max-width: 100%; }
.analysis-parameters { margin: 12px 0 0; border-top: 1px solid var(--project-border); }
.analysis-parameter { display: grid; grid-template-columns: minmax(0, 1fr) minmax(0, 1.6fr); gap: 18px; padding: 9px 0; border-bottom: 1px solid var(--project-border); }
.analysis-parameter:last-child { border-bottom: 0; padding-bottom: 0; }
.analysis-parameter dt { color: var(--project-muted); font-size: 12px; line-height: 1.7; overflow-wrap: anywhere; }
.analysis-parameter dd { margin: 0; min-width: 0; font-size: 13px; line-height: 1.6; }
.analysis-parameter-value { white-space: pre-wrap; overflow-wrap: anywhere; }
.analysis-parameter-scope { display: block; margin-top: 3px; color: var(--project-muted); font-size: 11px; white-space: pre-wrap; overflow-wrap: anywhere; }
@media (max-width: 599px) {
  .analysis-item { padding: 12px; }
  .analysis-heading { flex-wrap: wrap; gap: 8px; }
  .analysis-status { margin: 0 0 0 38px; max-width: 100%; }
  .analysis-parameter { grid-template-columns: minmax(0, 1fr); gap: 3px; }
}
</style>
