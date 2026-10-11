<script setup lang="ts">
import { computed, ref, watch } from 'vue';
import type { InsightBar, InsightBox, InsightProportionGroup, ProjectInsightChartData } from 'src/utils/project-plots';

const props = withDefaults(defineProps<{
  chart: ProjectInsightChartData;
  compact?: boolean;
}>(), { compact: false });

interface Inspection {
  label: string;
  detail: string;
}

const inspection = ref<Inspection>();
const bars = computed(() => props.chart.type === 'bars' ? props.chart : undefined);
const boxes = computed(() => props.chart.type === 'boxes' ? props.chart.boxes : []);
const proportions = computed(() => props.chart.type === 'proportions' ? props.chart : undefined);
const countFormatter = new Intl.NumberFormat('fr-FR', { maximumFractionDigits: 0 });

function formatNumber(value: number, precise = false): string {
  const magnitude = Math.abs(value);
  return new Intl.NumberFormat('fr-FR', {
    notation: magnitude > 0 && (magnitude < .001 || magnitude >= 1000000) ? 'scientific' : 'standard',
    maximumFractionDigits: precise ? 6 : 2,
  }).format(value);
}

function formatPercent(value: number, precise = false): string {
  return `${new Intl.NumberFormat('fr-FR', { maximumFractionDigits: precise ? 6 : 1 }).format(value)} %`;
}

function barValue(value: number, precise = false): string {
  if (bars.value?.unit === 'percent') return formatPercent(value, precise);
  const sign = (bars.value?.min ?? 0) < 0 && value > 0 ? '+' : '';
  return `${sign}${formatNumber(value, precise)}`;
}

function clamp(value: number, min: number, max: number): number {
  return Math.min(max, Math.max(min, value));
}

function barPosition(value: number): number {
  const chart = bars.value;
  if (!chart) return 0;
  const range = chart.max - chart.min;
  return range > 0 ? clamp((value - chart.min) / range * 100, 0, 100) : 0;
}

const barTicks = computed(() => {
  const chart = bars.value;
  return chart ? Array.from({ length: 5 }, (_, index) => chart.min + (chart.max - chart.min) * index / 4) : [];
});
const barZero = computed(() => bars.value ? barPosition(clamp(0, bars.value.min, bars.value.max)) : 0);

function barStyle(value: number) {
  const position = barPosition(value);
  return { left: `${Math.min(barZero.value, position)}%`, width: `${Math.abs(position - barZero.value)}%` };
}

function barDescription(row: InsightBar): string {
  return `${row.label} : ${barValue(row.value, true)}${row.detail ? `. ${row.detail}` : ''}`;
}

function barInspection(row: InsightBar): Inspection {
  return { label: row.label, detail: `${barValue(row.value, true)}${row.detail ? ` · ${row.detail}` : ''}` };
}

const boxDomain = computed(() => {
  if (!boxes.value.length) return { min: 0, max: 1 };
  const min = Math.min(...boxes.value.map((box) => box.min));
  const max = Math.max(...boxes.value.map((box) => box.max));
  if (min !== max) return { min, max };
  const margin = Math.abs(min) * .05 || 1;
  return { min: min - margin, max: max + margin };
});
const boxTicks = computed(() => Array.from({ length: 5 }, (_, index) => boxDomain.value.min + (boxDomain.value.max - boxDomain.value.min) * index / 4));

function boxPosition(value: number): number {
  const domain = boxDomain.value;
  return 12 + clamp((value - domain.min) / (domain.max - domain.min), 0, 1) * 476;
}

function boxInspection(box: InsightBox): Inspection {
  return {
    label: box.label,
    detail: `Minimum ${formatNumber(box.min, true)} · Quartile à 25 % ${formatNumber(box.q1, true)} · Médiane ${formatNumber(box.median, true)} · Quartile à 75 % ${formatNumber(box.q3, true)} · Maximum ${formatNumber(box.max, true)}${box.count !== undefined ? ` · ${countFormatter.format(box.count)} valeurs renseignées` : ''}`,
  };
}

function boxDescription(box: InsightBox): string {
  const detail = boxInspection(box);
  return `${detail.label} : ${detail.detail}`;
}

function proportionPercent(count: number, group: InsightProportionGroup): number {
  return group.present > 0 ? count / group.present * 100 : 0;
}

function proportionInspection(group: InsightProportionGroup, categoryIndex: number): Inspection {
  const count = group.counts[categoryIndex] ?? 0;
  const category = proportions.value?.categories[categoryIndex] ?? '';
  return {
    label: `${group.label} · ${category}`,
    detail: `${countFormatter.format(count)} sur ${countFormatter.format(group.present)} valeurs renseignées · ${formatPercent(proportionPercent(count, group), true)}`,
  };
}

function proportionDescription(group: InsightProportionGroup, categoryIndex: number): string {
  const detail = proportionInspection(group, categoryIndex);
  return `${detail.label} : ${detail.detail}`;
}

const defaultInspection = computed<Inspection | undefined>(() => {
  if (bars.value?.rows.length) return barInspection(bars.value.rows[0]);
  if (boxes.value.length) return boxInspection(boxes.value[0]);
  const group = proportions.value?.groups.find((entry) => entry.present > 0);
  if (group && proportions.value?.categories.length) return proportionInspection(group, 0);
  return undefined;
});
const displayedInspection = computed(() => inspection.value ?? defaultInspection.value);
const hasData = computed(() => Boolean(bars.value?.rows.length || boxes.value.length || proportions.value?.groups.length));

watch(() => props.chart, () => { inspection.value = undefined; });
</script>

<template>
  <div class="insight-chart" :class="{ 'insight-chart-compact': compact }">
    <p v-if="!hasData" class="chart-empty">Aucune valeur disponible pour cette représentation.</p>

    <section v-else-if="bars" class="bars-chart" :aria-label="bars.unit === 'percent' ? 'Comparaison des proportions' : 'Comparaison des coefficients'">
      <div class="bar-axis-row" aria-hidden="true">
        <span />
        <div class="bar-axis">
          <span v-for="(tick, index) in barTicks" :key="index" :style="{ left: `${barPosition(tick)}%` }" :class="{ 'axis-first': index === 0, 'axis-last': index === barTicks.length - 1 }">{{ barValue(tick) }}</span>
        </div>
        <span />
      </div>
      <button
        v-for="(row, index) in bars.rows"
        :key="`${index}-${row.label}`"
        type="button"
        class="bar-row chart-row"
        :data-label="row.label"
        :data-value="row.value"
        :aria-label="barDescription(row)"
        :title="barDescription(row)"
        @mouseenter="inspection = barInspection(row)"
        @focus="inspection = barInspection(row)"
        @click="inspection = barInspection(row)"
      >
        <span class="chart-label"><span>{{ row.label }}</span><small v-if="row.detail">{{ row.detail }}</small></span>
        <span class="bar-track" aria-hidden="true">
          <span v-for="(tick, tickIndex) in barTicks" :key="tickIndex" class="chart-gridline" :style="{ left: `${barPosition(tick)}%` }" />
          <span class="bar-zero" :style="{ left: `${barZero}%` }" />
          <span v-if="row.value !== 0" class="bar-fill" :data-value="row.value" :class="{ 'bar-fill-negative': row.value < 0 }" :style="barStyle(row.value)" />
        </span>
        <span class="bar-value">{{ barValue(row.value) }}</span>
      </button>
    </section>

    <section v-else-if="chart.type === 'boxes'" class="boxes-chart" aria-label="Comparaison des distributions, des valeurs minimales et maximales et des quartiles">
      <div class="box-axis-row" aria-hidden="true">
        <span />
        <div class="bar-axis box-axis">
          <template v-for="(tick, index) in boxTicks" :key="index">
            <span :style="{ left: `${boxPosition(tick) / 5}%` }" :class="{ 'axis-first': index === 0, 'axis-last': index === boxTicks.length - 1 }">{{ formatNumber(tick) }}</span>
            <i class="box-axis-mark" :style="{ left: `${boxPosition(tick) / 5}%` }" />
          </template>
        </div>
      </div>
      <button
        v-for="(box, index) in boxes"
        :key="`${index}-${box.label}`"
        type="button"
        class="box-row chart-row"
        :data-group="box.label"
        :aria-label="boxDescription(box)"
        :title="boxDescription(box)"
        @mouseenter="inspection = boxInspection(box)"
        @focus="inspection = boxInspection(box)"
        @click="inspection = boxInspection(box)"
      >
        <span class="chart-label"><span>{{ box.label }}</span><small v-if="box.count !== undefined">{{ countFormatter.format(box.count) }} valeurs renseignées</small></span>
        <svg class="box-svg" viewBox="0 0 500 52" preserveAspectRatio="none" aria-hidden="true">
          <line v-for="(tick, tickIndex) in boxTicks" :key="tickIndex" :x1="boxPosition(tick)" :x2="boxPosition(tick)" y1="4" y2="48" class="svg-gridline" />
          <line :x1="boxPosition(box.min)" :x2="boxPosition(box.max)" y1="26" y2="26" class="box-whisker" />
          <line :x1="boxPosition(box.min)" :x2="boxPosition(box.min)" y1="17" y2="35" class="box-whisker" />
          <line :x1="boxPosition(box.max)" :x2="boxPosition(box.max)" y1="17" y2="35" class="box-whisker" />
          <rect :x="boxPosition(box.q1)" y="12" :width="boxPosition(box.q3) - boxPosition(box.q1)" height="28" class="box-quartiles" />
          <line :x1="boxPosition(box.median)" :x2="boxPosition(box.median)" y1="10" y2="42" class="box-median" />
        </svg>
      </button>
    </section>

    <section v-else-if="proportions" class="proportions-chart" aria-label="Répartition des catégories dans chaque groupe, parmi les valeurs renseignées">
      <div class="proportion-axis-row" aria-hidden="true">
        <span />
        <div class="bar-axis">
          <span v-for="(tick, index) in [0, 25, 50, 75, 100]" :key="tick" :style="{ left: `${tick}%` }" :class="{ 'axis-first': index === 0, 'axis-last': index === 4 }">{{ tick }} %</span>
        </div>
      </div>
      <div v-for="(group, index) in proportions.groups" :key="`${index}-${group.label}`" class="proportion-row" :data-group="group.label">
        <div class="chart-label">
          <span>{{ group.label }}</span>
          <small>{{ countFormatter.format(group.present) }} renseignées<span v-if="group.missing !== undefined"> · {{ countFormatter.format(group.missing) }} manquantes</span></small>
        </div>
        <div v-if="group.present > 0" class="proportion-stack">
          <template v-for="(category, categoryIndex) in proportions.categories" :key="`${categoryIndex}-${category}`">
            <button
              v-if="(group.counts[categoryIndex] ?? 0) > 0"
              type="button"
              class="proportion-segment"
              :class="`category-${categoryIndex % 6}`"
              :data-category="category"
              :data-percentage="proportionPercent(group.counts[categoryIndex], group)"
              :style="{ width: `${proportionPercent(group.counts[categoryIndex], group)}%` }"
              :aria-label="proportionDescription(group, categoryIndex)"
              :title="proportionDescription(group, categoryIndex)"
              @mouseenter="inspection = proportionInspection(group, categoryIndex)"
              @focus="inspection = proportionInspection(group, categoryIndex)"
              @click="inspection = proportionInspection(group, categoryIndex)"
            >
              <span v-if="proportionPercent(group.counts[categoryIndex], group) >= 18" aria-hidden="true">{{ formatPercent(proportionPercent(group.counts[categoryIndex], group)) }}</span>
            </button>
          </template>
        </div>
        <div v-else class="proportion-no-values">Aucune valeur renseignée</div>
      </div>
      <ul class="proportion-legend" aria-label="Légende des catégories">
        <li v-for="(category, index) in proportions.categories" :key="`${index}-${category}`"><span class="category-swatch" :class="`category-${index % 6}`" aria-hidden="true" /><span>{{ category }}</span></li>
      </ul>
    </section>

    <div v-if="hasData && displayedInspection" class="chart-inspection">
      <span class="inspection-label">{{ displayedInspection.label }}</span>
      <span>{{ displayedInspection.detail }}</span>
    </div>
  </div>
</template>

<style scoped>
.insight-chart { width: 100%; min-width: 0; color: var(--project-ink); }
.chart-empty { margin: 12px 0; color: var(--project-muted); font-size: 12px; line-height: 1.6; }
.chart-row { display: grid; align-items: center; gap: 12px; width: 100%; min-width: 0; padding: 9px 0; border: 0; border-bottom: 1px solid var(--project-border); background: transparent; color: inherit; font: inherit; text-align: left; cursor: pointer; }
.chart-row:last-child { border-bottom: 0; }
.chart-row:hover { background: var(--project-paper); }
.chart-row:focus-visible { outline: 2px solid var(--project-ink); outline-offset: 3px; border-radius: 2px; }
.chart-label { display: flex; flex-direction: column; gap: 4px; min-width: 0; font-size: 12px; line-height: 1.45; overflow-wrap: anywhere; }
.chart-label small { color: var(--project-muted); font-size: 10px; line-height: 1.5; }
.bar-axis-row, .bar-row { grid-template-columns: minmax(0, 150px) minmax(0, 1fr) 70px; }
.bar-axis-row, .box-axis-row, .proportion-axis-row { display: grid; gap: 12px; align-items: end; min-width: 0; margin-bottom: 5px; }
.bar-axis { position: relative; height: 21px; color: var(--project-muted); font-size: 10px; font-variant-numeric: tabular-nums; }
.bar-axis > span { position: absolute; top: 0; white-space: nowrap; transform: translateX(-50%); }
.bar-axis > .axis-first { transform: none; }
.bar-axis > .axis-last { transform: translateX(-100%); }
.bar-track { position: relative; display: block; width: 100%; height: 25px; }
.chart-gridline, .bar-zero { position: absolute; top: -4px; bottom: -4px; width: 1px; background: var(--project-border); }
.bar-zero { z-index: 1; background: var(--project-muted); }
.bar-fill { position: absolute; top: 3px; bottom: 3px; background: var(--project-brand); border: 1px solid var(--q-secondary); }
.bar-fill-negative { background: var(--project-ink); border-color: var(--project-ink); }
.bar-value { text-align: right; font-size: 12px; font-variant-numeric: tabular-nums; white-space: nowrap; }
.box-axis-row, .box-row { grid-template-columns: minmax(0, 150px) minmax(0, 1fr); }
.box-axis { height: 27px; }
.box-axis::after { position: absolute; bottom: 0; left: 2.4%; right: 2.4%; height: 1px; background: var(--project-muted); content: ''; }
.box-axis-mark { position: absolute; bottom: -3px; width: 1px; height: 7px; background: var(--project-muted); }
.box-svg { display: block; width: 100%; height: 52px; overflow: visible; }
.svg-gridline { stroke: var(--project-border); stroke-width: 1; vector-effect: non-scaling-stroke; }
.box-whisker { stroke: var(--project-ink); stroke-width: 1.2; vector-effect: non-scaling-stroke; }
.box-quartiles { fill: var(--project-brand); fill-opacity: .7; stroke: var(--q-secondary); stroke-width: 1; vector-effect: non-scaling-stroke; }
.box-median { stroke: var(--project-ink); stroke-width: 2; vector-effect: non-scaling-stroke; }
.proportion-axis-row, .proportion-row { grid-template-columns: minmax(0, 150px) minmax(0, 1fr); }
.proportion-row { display: grid; align-items: center; gap: 12px; min-width: 0; padding: 10px 0; }
.proportion-stack { display: flex; width: 100%; min-width: 0; height: 32px; border: 1px solid var(--project-ink); background: var(--project-paper); }
.proportion-segment { display: flex; flex-shrink: 0; align-items: center; justify-content: center; box-sizing: border-box; min-width: 0; padding: 0; border: 0; border-right: 1px solid rgba(32, 59, 60, .3); font: inherit; font-size: 10px; font-weight: 500; white-space: nowrap; overflow: hidden; cursor: pointer; }
.proportion-segment:last-child { border-right: 0; }
.proportion-segment:hover { filter: brightness(.95); }
.proportion-segment:focus-visible { position: relative; z-index: 1; outline: 2px solid var(--project-ink); outline-offset: 3px; }
.proportion-no-values { padding: 7px 10px; border: 1px dashed var(--project-border); color: var(--project-muted); font-size: 11px; }
.proportion-legend { display: flex; flex-wrap: wrap; gap: 10px 18px; margin: 15px 0 0; padding: 0; list-style: none; }
.proportion-legend li { display: flex; align-items: flex-start; gap: 7px; max-width: 100%; min-width: 0; font-size: 11px; line-height: 1.45; overflow-wrap: anywhere; }
.category-swatch { display: block; flex-shrink: 0; width: 15px; height: 15px; border: 1px solid var(--project-ink); }
.category-0 { color: var(--project-ink); background-color: var(--project-brand); }
.category-1 { color: var(--project-paper); background-color: var(--project-ink); }
.category-2 { color: var(--project-ink); background-color: #d3d7cc; background-image: repeating-linear-gradient(135deg, transparent, transparent 4px, rgba(32, 59, 60, .18) 4px, rgba(32, 59, 60, .18) 5px); }
.category-3 { color: var(--project-ink); background-color: var(--project-tint); background-image: radial-gradient(var(--project-muted) .65px, transparent .85px); background-size: 7px 7px; }
.category-4 { color: var(--project-ink); background-color: #c9b98f; background-image: repeating-linear-gradient(90deg, transparent, transparent 6px, rgba(32, 59, 60, .16) 6px, rgba(32, 59, 60, .16) 7px); }
.category-5 { color: var(--project-paper); background-color: var(--q-secondary); background-image: repeating-linear-gradient(0deg, transparent, transparent 5px, rgba(255, 255, 255, .13) 5px, rgba(255, 255, 255, .13) 6px); }
.chart-inspection { display: flex; flex-direction: column; gap: 4px; min-height: 50px; margin-top: 15px; padding-top: 12px; border-top: 1px solid var(--project-border); color: var(--project-muted); font-size: 11px; line-height: 1.7; overflow-wrap: anywhere; }
.inspection-label { color: var(--project-ink); font-weight: 500; }
.insight-chart-compact .chart-row { padding-top: 6px; padding-bottom: 6px; }
.insight-chart-compact .box-svg { height: 44px; }
@media (max-width: 599px) {
  .bar-axis-row, .bar-row { grid-template-columns: minmax(0, 1fr) 65px; }
  .bar-axis-row > :first-child { display: none; }
  .bar-row > .chart-label { grid-column: 1 / -1; }
  .bar-row { row-gap: 7px; padding-top: 12px; padding-bottom: 12px; }
  .box-axis-row, .box-row, .proportion-axis-row, .proportion-row { grid-template-columns: minmax(0, 1fr); }
  .box-axis-row > :first-child, .proportion-axis-row > :first-child { display: none; }
  .box-row { row-gap: 4px; }
  .proportion-row { gap: 7px; padding-top: 12px; padding-bottom: 12px; }
  .chart-label { font-size: 12px; }
  .chart-label small { font-size: 11px; }
  .proportion-legend { gap: 10px 14px; }
}
</style>
