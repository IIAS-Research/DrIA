<script setup lang="ts">
import { computed, ref } from 'vue';
import { formatScore } from 'src/utils/iaml';

const props = defineProps<{
  features: { name: string; value: unknown }[];
  explainedClass?: unknown;
}>();

const search = ref<string | null>('');
const expanded = ref(false);
const previewSize = 10;
const hasNegativeValues = computed(() => props.features.some((feature) =>
  typeof feature.value === 'number' && Number.isFinite(feature.value) && feature.value < 0));
const hasScalarValues = computed(() => props.features.some((feature) =>
  typeof feature.value === 'number' && Number.isFinite(feature.value)));
const rankedFeatures = computed(() => props.features.map((feature, sourceIndex) => ({
  name: feature.name,
  value: typeof feature.value === 'number' && Number.isFinite(feature.value) ? feature.value : null,
  values: Array.isArray(feature.value) && feature.value.length ? feature.value.map((value: unknown) =>
    typeof value === 'number' && Number.isFinite(value) ? value : null) : undefined,
  sourceIndex,
})).sort((a, b) => {
  if (a.value === null) return b.value === null
    ? Number(Boolean(b.values)) - Number(Boolean(a.values)) || a.sourceIndex - b.sourceIndex : 1;
  if (b.value === null) return -1;
  return Math.abs(b.value) - Math.abs(a.value) || a.sourceIndex - b.sourceIndex;
}).map((feature, index) => ({ ...feature, rank: feature.value === null ? undefined : index + 1 })));
const maximumAmplitude = computed(() => rankedFeatures.value.reduce((maximum, feature) =>
  Math.max(maximum, Math.abs(feature.value ?? 0)), 0));
const filteredFeatures = computed(() => {
  const query = (search.value ?? '').trim().toLocaleLowerCase('en-US');
  return query ? rankedFeatures.value.filter((feature) => feature.name.toLocaleLowerCase('en-US').includes(query))
    : rankedFeatures.value;
});
const visibleFeatures = computed(() => expanded.value ? filteredFeatures.value : filteredFeatures.value.slice(0, previewSize));
const unavailableCount = computed(() => rankedFeatures.value.filter((feature) =>
  feature.value === null && !feature.values?.some((value) => value !== null)).length);
const hasMultipleValues = computed(() => rankedFeatures.value.some((feature) => feature.values));
const explainedClassLabel = computed(() => {
  const value = props.explainedClass;
  if (typeof value === 'string') return value;
  if (typeof value === 'number' && Number.isFinite(value) || typeof value === 'boolean') return String(value);
  return '';
});

function barStyle(value: number) {
  const width = maximumAmplitude.value ? Math.abs(value) / maximumAmplitude.value * (hasNegativeValues.value ? 50 : 100) : 0;
  return {
    width: `${width}%`,
    left: `${hasNegativeValues.value ? value < 0 ? 50 - width : 50 : 0}%`,
  };
}
</script>

<template>
  <section class="feature-ranking" aria-label="Feature importance">
    <header class="ranking-heading">
      <div>
        <span class="ranking-eyebrow">Understand the model</span>
        <h2>Feature importance</h2>
        <p v-if="hasNegativeValues">Features are ranked by magnitude. Bars start at zero, extending left for negative values and right for positive values.</p>
        <p v-else-if="hasScalarValues">Features with a single importance value are ranked from highest to lowest. Bars compare their values on the same scale.</p>
        <p v-else>The values returned for each feature are shown below.</p>
      </div>
      <span class="ranking-count">{{ features.length }} feature{{ features.length !== 1 ? 's' : '' }}</span>
    </header>

    <p v-if="explainedClassLabel" class="explained-class">Explained class: <strong>{{ explainedClassLabel }}</strong></p>

    <div v-if="features.length > previewSize" class="ranking-search">
      <q-input
        v-model="search"
        outlined
        dense
        clearable
        label="Search for a feature"
        class="feature-search"
      >
        <template #prepend><q-icon name="search" size="20px" /></template>
      </q-input>
      <span v-if="search?.trim()" class="search-count" aria-live="polite">{{ filteredFeatures.length }} result{{ filteredFeatures.length !== 1 ? 's' : '' }}</span>
    </div>

    <div v-if="visibleFeatures.length" class="ranking-table">
      <div class="ranking-labels" aria-hidden="true">
        <span>Rank</span><span>Feature</span><span>Comparison</span><span>Importance</span>
      </div>
      <ol class="ranking-list">
        <li
          v-for="feature in visibleFeatures"
          :key="feature.sourceIndex"
          class="ranking-row"
          :class="{ 'value-unavailable': feature.value === null && !feature.values, 'multiple-values-row': feature.values }"
        >
          <span class="feature-rank" aria-hidden="true">{{ feature.rank === undefined ? '—' : String(feature.rank).padStart(2, '0') }}</span>
          <span class="feature-name">{{ feature.name }}</span>
          <div v-if="feature.value !== null" class="feature-track" :class="{ 'diverging-track': hasNegativeValues }" aria-hidden="true">
            <span class="feature-bar" :class="{ 'negative-bar': feature.value < 0 }" :style="barStyle(feature.value)" />
            <span v-if="hasNegativeValues" class="zero-axis" />
          </div>
          <div v-else-if="feature.values" class="feature-values-block">
            <span class="multiple-values-label">{{ feature.values.length > 1 ? 'Multiple values returned for this feature' : 'Value returned for this feature' }}</span>
            <div class="feature-values">
              <span v-for="(value, index) in feature.values" :key="index" class="array-value">{{ formatScore(value) }}</span>
            </div>
          </div>
          <span v-else class="empty-track" aria-hidden="true" />
          <span v-if="!feature.values" class="feature-value">{{ formatScore(feature.value) }}</span>
        </li>
      </ol>
      <div v-if="hasNegativeValues" class="axis-legend" aria-hidden="true"><span>Negative values</span><span>0</span><span>Positive values</span></div>
    </div>
    <p v-else class="ranking-empty">{{ features.length ? 'No features match this search.' : 'No feature importance values are available.' }}</p>

    <footer v-if="features.length" class="ranking-footer">
      <div class="ranking-notes">
        <span>{{ visibleFeatures.length }} of {{ filteredFeatures.length }} feature{{ filteredFeatures.length !== 1 ? 's' : '' }}</span>
        <p>Values are rounded to at most 4 decimal places; bars use the unrounded values.</p>
        <p v-if="hasMultipleValues">Arrays of values are shown separately and are not ranked with single-value importances.</p>
        <p v-if="unavailableCount">{{ unavailableCount }} importance value{{ unavailableCount !== 1 ? 's' : '' }} unavailable.</p>
      </div>
      <q-btn
        v-if="filteredFeatures.length > previewSize"
        flat
        no-caps
        color="primary"
        :icon-right="expanded ? 'expand_less' : 'expand_more'"
        :label="expanded ? 'Show the first 10' : `Show all ${filteredFeatures.length} features`"
        :aria-expanded="expanded"
        @click="expanded = !expanded"
      />
    </footer>
  </section>
</template>

<style scoped>
.feature-ranking { container: feature-importance / inline-size; min-width: 0; overflow: hidden; color: var(--project-ink); background: var(--project-surface); border: 1px solid var(--project-border); border-radius: 14px; }
.ranking-heading { display: flex; align-items: flex-start; justify-content: space-between; gap: 20px; padding: 24px 26px 18px; }
.ranking-heading > div { min-width: 0; }
.ranking-eyebrow { display: block; color: var(--q-primary); font-size: 11px; font-weight: 700; letter-spacing: .08em; text-transform: uppercase; }
.ranking-heading h2 { margin: 7px 0 8px; font-size: 22px; font-weight: 600; line-height: 1.3; }
.ranking-heading p { max-width: 700px; margin: 0; color: var(--project-muted); font-size: 13px; line-height: 1.6; }
.ranking-count { flex-shrink: 0; padding-top: 3px; color: var(--project-muted); font-size: 12px; white-space: nowrap; }
.explained-class { margin: 0 26px 18px; color: var(--project-muted); font-size: 12px; overflow-wrap: anywhere; }
.explained-class strong { color: var(--project-ink); font-weight: 600; }
.ranking-search { display: flex; align-items: center; gap: 14px; margin: 0 26px 20px; }
.feature-search { width: 100%; max-width: 380px; min-width: 0; }
.search-count { color: var(--project-muted); font-size: 12px; flex-shrink: 0; }
.ranking-table { padding: 0 26px; }
.ranking-labels, .ranking-row { display: grid; grid-template-columns: 34px minmax(140px, 1fr) minmax(110px, 1.3fr) minmax(95px, auto); column-gap: 16px; align-items: center; }
.ranking-labels { padding: 11px 0; border-bottom: 1px solid var(--project-border); color: var(--project-muted); font-size: 11px; }
.ranking-labels > :last-child { text-align: right; }
.ranking-list { margin: 0; padding: 0; list-style: none; }
.ranking-row { min-height: 60px; padding: 14px 0; border-bottom: 1px solid var(--project-border); }
.ranking-row:last-child { border-bottom: 0; }
.feature-rank { align-self: start; padding-top: 1px; color: var(--project-muted); font-size: 12px; font-variant-numeric: tabular-nums; }
.feature-name { color: var(--project-ink); font-size: 14px; font-weight: 500; line-height: 1.45; overflow-wrap: anywhere; min-width: 0; }
.feature-track { position: relative; height: 11px; background: var(--project-paper); border-radius: 3px; }
.feature-bar { position: absolute; top: 0; height: 100%; border-radius: 3px; background: var(--project-brand); }
.negative-bar { background: var(--q-secondary); }
.diverging-track .feature-bar { border-radius: 2px; }
.zero-axis { position: absolute; z-index: 1; left: 50%; top: -4px; bottom: -4px; width: 1px; background: var(--project-muted); opacity: .55; }
.feature-value { text-align: right; color: var(--project-ink); font-size: 15px; font-weight: 600; font-variant-numeric: tabular-nums; overflow-wrap: anywhere; }
.feature-values-block { grid-column: 3 / 5; min-width: 0; }
.multiple-values-label { display: block; color: var(--project-muted); font-size: 11px; line-height: 1.5; }
.feature-values { display: flex; flex-wrap: wrap; gap: 6px 14px; margin-top: 6px; }
.array-value { color: var(--project-ink); font-size: 14px; font-weight: 600; font-variant-numeric: tabular-nums; overflow-wrap: anywhere; }
.value-unavailable .feature-value { color: var(--project-muted); font-size: 12px; font-weight: 400; }
.axis-legend { display: flex; justify-content: space-between; gap: 12px; padding: 12px 0 16px; color: var(--project-muted); font-size: 10px; }
.ranking-empty { margin: 0; padding: 22px 26px; color: var(--project-muted); font-size: 13px; border-top: 1px solid var(--project-border); }
.ranking-footer { display: flex; align-items: center; justify-content: space-between; gap: 16px; padding: 16px 26px; border-top: 1px solid var(--project-border); background: var(--project-paper); }
.ranking-notes { min-width: 0; color: var(--project-muted); font-size: 11px; line-height: 1.5; }
.ranking-notes > span { font-size: 12px; color: var(--project-ink); }
.ranking-notes p { margin: 4px 0 0; }
.ranking-footer > .q-btn { flex-shrink: 0; }
@container feature-importance (max-width: 650px) {
  .ranking-heading { padding: 20px; gap: 12px; }
  .ranking-heading h2 { font-size: 20px; }
  .explained-class, .ranking-search { margin-left: 20px; margin-right: 20px; }
  .ranking-table { padding: 0 20px; }
  .ranking-labels { display: none; }
  .ranking-row { grid-template-columns: 26px minmax(0, 1fr) minmax(75px, auto); column-gap: 12px; row-gap: 10px; }
  .feature-rank { grid-column: 1; grid-row: 1 / 3; }
  .feature-name { grid-column: 2; grid-row: 1; }
  .feature-value { grid-column: 3; grid-row: 1; }
  .feature-track { grid-column: 2 / 4; grid-row: 2; height: 9px; }
  .multiple-values-row .feature-name { grid-column: 2 / 4; }
  .feature-values-block { grid-column: 2 / 4; grid-row: 2; }
  .empty-track { display: none; }
  .value-unavailable .feature-rank { grid-row: 1; }
  .ranking-footer { padding: 16px 20px; align-items: flex-start; flex-direction: column; gap: 10px; }
  .ranking-footer > .q-btn { margin-left: -10px; }
}
@media (max-width: 800px) {
  .ranking-heading { padding: 20px; gap: 12px; }
  .ranking-heading h2 { font-size: 20px; }
  .explained-class { margin-left: 20px; margin-right: 20px; }
  .ranking-search { margin-left: 20px; margin-right: 20px; }
  .ranking-table { padding: 0 20px; }
  .ranking-labels { display: none; }
  .ranking-row { grid-template-columns: 26px minmax(0, 1fr) minmax(75px, auto); column-gap: 12px; row-gap: 10px; }
  .feature-rank { grid-column: 1; grid-row: 1 / 3; }
  .feature-name { grid-column: 2; grid-row: 1; }
  .feature-value { grid-column: 3; grid-row: 1; }
  .feature-track { grid-column: 2 / 4; grid-row: 2; height: 9px; }
  .multiple-values-row .feature-name { grid-column: 2 / 4; }
  .feature-values-block { grid-column: 2 / 4; grid-row: 2; }
  .empty-track { display: none; }
  .value-unavailable .feature-rank { grid-row: 1; }
  .ranking-footer { padding: 16px 20px; align-items: flex-start; flex-direction: column; gap: 10px; }
  .ranking-footer > .q-btn { margin-left: -10px; }
}
@media (max-width: 480px) {
  .ranking-heading { flex-direction: column; gap: 8px; }
  .ranking-heading, .ranking-empty { padding-left: 16px; padding-right: 16px; }
  .ranking-count { padding-top: 0; }
  .explained-class, .ranking-search { margin-left: 16px; margin-right: 16px; }
  .ranking-search { align-items: stretch; flex-direction: column; gap: 7px; }
  .ranking-table { padding: 0 16px; }
  .ranking-footer { padding-left: 16px; padding-right: 16px; }
  .ranking-row { column-gap: 8px; }
  .feature-name { font-size: 13px; }
  .feature-value { font-size: 14px; }
}
</style>
