<script setup lang="ts">
import { computed, ref } from 'vue';
import { IStatistics } from 'src/components/models';
import { ColumnProfile, ProjectColumnAnnotation, ProjectDataset, ProjectScalar } from 'src/utils/project-data';
import { formatProjectStatistic, getProjectColumnStatistics, projectStatisticHasValue, ProjectStatistic } from 'src/utils/project-statistics';

const props = withDefaults(defineProps<{
  dataset: ProjectDataset;
  roles: Record<string, ProjectColumnAnnotation>;
  targetColumn?: string;
  predictionKind?: 'category' | 'quantity' | 'survival';
  filename?: string;
  groupColumns?: string[];
  timeUnit?: string;
  eventValue?: string;
  statistics?: IStatistics;
  statisticsLoading?: boolean;
  statisticsError?: string;
  statisticsUnavailableReason?: string;
  context?: 'creation' | 'project';
}>(), {
  targetColumn: '',
  predictionKind: 'category',
  filename: '',
  groupColumns: () => [],
  timeUnit: '',
  eventValue: '',
  statisticsLoading: false,
  statisticsError: '',
  statisticsUnavailableReason: '',
  context: 'creation',
});

const emit = defineEmits<{ (event: 'retry-statistics'): void }>();
interface DataCheck { key: string; blocking: boolean; title: string; detail: string }
interface StatisticColumn { key: string; label: string; priority: number; sourceIndex: number }
const search = ref('');
const measureSearch = ref('');
const chosenMeasures = ref<string[] | null>(null);
const analysisSelections = ref<Record<string, string>>({});
const statisticAliases = new Map<string, string>([
  ['50%', 'median'], ['quantile_0.5', 'median'],
  ['std', 'stdev'], ['standard_deviation', 'stdev'],
  ['25%', 'quantile_0.25'], ['75%', 'quantile_0.75'],
  ['unique', 'nunique'], ['unique_count', 'nunique'],
  ['top', 'mode'], ['median_absolute_deviation', 'mad'],
]);
const mainMeasures = ['mean', 'median', 'stdev', 'mode', 'most_frequent_ratio'];
const number = new Intl.NumberFormat('en-US', { maximumFractionDigits: 3 });
const numericNumber = new Intl.NumberFormat('en-US', { maximumSignificantDigits: 6 });
const scientificNumber = new Intl.NumberFormat('en-US', { notation: 'scientific', maximumSignificantDigits: 6 });
const percent = new Intl.NumberFormat('en-US', { maximumFractionDigits: 1 });
const predictors = computed(() => props.dataset.columns.filter((name) => role(name) === 'predictor'));
const exclusions = computed(() => props.dataset.columns.filter((name) => ['identifier', 'unavailable'].includes(role(name))));
const groups = computed(() => props.dataset.columns.filter((name) => role(name) === 'group' || props.groupColumns.includes(name)));
const retained = computed(() => props.dataset.columns.filter((name) => !exclusions.value.includes(name)));
const targetName = computed(() => props.targetColumn || props.dataset.columns.find((name) => ['target', 'event'].includes(role(name))) || '');
const target = computed(() => props.dataset.profiles[targetName.value]);
const timeName = computed(() => props.dataset.columns.find((name) => role(name) === 'time') || '');
const time = computed(() => props.dataset.profiles[timeName.value]);
const totalCells = computed(() => props.dataset.rowCount * props.dataset.columns.length);
const populatedCells = computed(() => props.dataset.columns.reduce((sum, name) => sum + (props.dataset.profiles[name]?.populatedCount ?? 0), 0));
const predictorMissing = computed(() => predictors.value.reduce((sum, name) => sum + (props.dataset.profiles[name]?.missingCount ?? 0), 0));
const predictorCells = computed(() => props.dataset.rowCount * predictors.value.length);
const categoryCounts = computed(() => target.value && !target.value.distinctValuesTruncated && target.value.distinctValues.length <= 12
  ? [...target.value.valueCounts].sort((a, b) => b.count - a.count) : []);
const eventCount = computed(() => {
  if (!props.eventValue || !target.value || target.value.distinctValuesTruncated) return undefined;
  try {
    const selected: unknown = JSON.parse(props.eventValue);
    return target.value.valueCounts.find((entry) => entry.value === selected)?.count;
  } catch { return undefined; }
});
const filteredColumns = computed(() => {
  const query = (search.value ?? '').trim().toLocaleLowerCase('en-US');
  return props.dataset.columns.filter((name) => `${name} ${roleLabel(name)} ${typeLabel(props.dataset.profiles[name])}`.toLocaleLowerCase('en-US').includes(query));
});
const columnStatistics = computed(() => getProjectColumnStatistics(props.statistics, props.dataset.columns, props.predictionKind));
const hasColumnStatistics = computed(() => props.dataset.columns.some((name) => analyses(name).length > 0));
const statisticColumns = computed(() => {
  const measures = new Map<string, StatisticColumn>();
  props.dataset.columns.forEach((name) => analyses(name).forEach((analysis) => analysis.statistics.forEach((statistic) => {
    if (!projectStatisticHasValue(statistic.value)) return;
    const key = canonicalStatisticKey(statistic.key);
    if (!measures.has(key)) measures.set(key, { key, label: statisticLabel(statistic), priority: statistic.priority, sourceIndex: statistic.sourceIndex });
  })));
  return [...measures.values()].sort((a, b) => a.priority - b.priority || a.sourceIndex - b.sourceIndex);
});
const defaultMeasures = computed(() => {
  const available = statisticColumns.value.filter((measure) => mainMeasures.includes(measure.key));
  return (available.length ? available : statisticColumns.value.slice(0, 5)).map((measure) => measure.key);
});
const displayedStatisticColumns = computed(() => statisticColumns.value.filter((measure) => (chosenMeasures.value ?? defaultMeasures.value).includes(measure.key)));
const filteredStatisticColumns = computed(() => {
  const query = (measureSearch.value ?? '').trim().toLocaleLowerCase('en-US');
  return statisticColumns.value.filter((measure) => measure.label.toLocaleLowerCase('en-US').includes(query));
});
const selectedStatistics = computed(() => Object.fromEntries(props.dataset.columns.map((name) => {
  const measures = new Map<string, ProjectStatistic>();
  availableStatistics(name).forEach((statistic) => {
    const key = canonicalStatisticKey(statistic.key);
    // Prefer the native key when an alias supplies the same measure.
    if (!measures.has(key) || statistic.key === key) measures.set(key, statistic);
  });
  return [name, measures];
})));
const checks = computed<DataCheck[]>(() => {
  if (!props.dataset.isComplete) return [];
  const result: DataCheck[] = [];
  const missingOutcome = [targetName.value, ...(props.predictionKind === 'survival' ? [timeName.value] : [])]
    .filter((name) => name && props.dataset.profiles[name]?.missingCount > 0);
  if (missingOutcome.length) result.push({
    key: 'outcome-missing', blocking: true,
    title: props.context === 'project' ? 'Missing target values' : 'Complete the target column',
    detail: `Missing values in ${names(missingOutcome)}. These columns must be filled in for every observation.`,
  });
  if (target.value && !target.value.distinctValuesTruncated && target.value.distinctValues.length === 1) result.push({
    key: 'outcome-constant', blocking: true,
    title: props.context === 'project' ? 'The target column is constant' : 'The target column must vary',
    detail: `“${targetName.value}” contains only one distinct value. The target column must contain at least two different values.`,
  });
  const missingGroups = groups.value.filter((name) => props.dataset.profiles[name]?.missingCount > 0);
  if (missingGroups.length) result.push({
    key: 'group-missing', blocking: true,
    title: props.context === 'project' ? 'Incomplete grouping information' : 'Complete the grouping information',
    detail: `Missing values in ${names(missingGroups)}. Group identifiers are needed to keep related observations together.`,
  });
  const nonFinite = retained.value.filter((name) => props.dataset.profiles[name]?.nonFiniteCount > 0);
  if (nonFinite.length) result.push({
    key: 'non-finite', blocking: true,
    title: props.context === 'project' ? 'Some values are nonfinite' : 'Correct nonfinite values',
    detail: props.context === 'project'
      ? `Nonfinite values in ${names(nonFinite)}, such as NaN or Infinity. Models cannot use these values as they are.`
      : `Nonfinite values in ${names(nonFinite)}, such as NaN or Infinity. Correct them in the file, or exclude the affected columns if they do not define the outcome or groups.`,
  });
  // Empty durations and non-finite values are already reported above.
  if (time.value && time.value.populatedCount > 0 && !time.value.nonFiniteCount
    && (time.value.type !== 'numeric' || (time.value.min ?? 0) < 0)) result.push({
    key: 'followup-invalid', blocking: true,
    title: props.context === 'project' ? 'Invalid follow-up durations' : 'Correct the follow-up duration',
    detail: `“${timeName.value}” ${time.value.type !== 'numeric' ? 'contains nonnumerical values' : 'contains negative durations'}. Follow-up duration must be a nonnegative number.`,
  });
  const empty = predictors.value.filter((name) => props.dataset.profiles[name]?.populatedCount === 0);
  if (empty.length) result.push({
    key: 'empty', blocking: false, title: 'Some features are entirely empty',
    detail: props.context === 'project'
      ? `${names(empty)}: no non-missing values. These columns provide no information for training.`
      : `${names(empty)}: no non-missing values. You can exclude these columns in the “Features” step.`,
  });
  const constant = predictors.value.filter((name) => {
    const profile = props.dataset.profiles[name];
    return profile && profile.populatedCount > 0 && !profile.distinctValuesTruncated && profile.distinctValues.length === 1;
  });
  if (constant.length) result.push({
    key: 'constant', blocking: false, title: 'Some features are constant',
    detail: `${names(constant)}: only one distinct non-missing value. These features do not distinguish observations.`,
  });
  if (predictorMissing.value > 0 && predictorCells.value > 0) result.push({
    key: 'predictor-missing', blocking: false,
    title: props.context === 'project' ? 'Missing values in features' : 'Plan how to handle missing values',
    detail: `${number.format(predictorMissing.value)} missing cells out of ${number.format(predictorCells.value)} in the features (${percentage(predictorMissing.value, predictorCells.value)}%). Handling them is part of data preparation.`,
  });
  return result;
});

function role(name: string): string { return props.roles[name]?.role ?? 'predictor'; }
function roleLabel(name: string): string { return props.roles[name]?.label ?? 'Feature'; }
function roleColor(name: string): string {
  if (['identifier', 'unavailable'].includes(role(name))) return 'grey-7';
  return ['group', 'predictor'].includes(role(name)) ? 'secondary' : 'accent';
}
function typeLabel(profile?: ColumnProfile): string {
  return profile ? { numeric: 'Number', boolean: 'Yes / no', text: 'Text or category', empty: 'Empty' }[profile.type] : 'In progress';
}
function percentage(count: number, total: number): string { return percent.format(total > 0 ? 100 * count / total : 0); }
function ratio(count: number, total: number): number { return total > 0 ? count / total : 0; }
function names(columns: string[]): string {
  return `${columns.slice(0, 4).map((name) => `“${name}”`).join(', ')}${columns.length > 4 ? ` and ${columns.length - 4} other column${columns.length > 5 ? 's' : ''}` : ''}`;
}
function scalarLabel(value: ProjectScalar): string { return typeof value === 'boolean' ? (value ? 'True' : 'False') : String(value); }
function numericLabel(value: number): string {
  const magnitude = Math.abs(value);
  return (magnitude > 0 && (magnitude < 0.001 || magnitude >= 1000000) ? scientificNumber : numericNumber).format(value);
}
function numericRange(profile?: ColumnProfile): string {
  if (profile?.type !== 'numeric' || !Number.isFinite(profile.min) || !Number.isFinite(profile.max)) return '';
  return `${numericLabel(profile.min as number)} to ${numericLabel(profile.max as number)}`;
}
function distinctLabel(profile?: ColumnProfile): string {
  if (!profile) return '—';
  return `${profile.distinctValuesTruncated ? 'At least ' : ''}${number.format(profile.distinctValues.length)}`;
}
function analyses(name: string) {
  return (columnStatistics.value[name] ?? []).filter((analysis) => analysis.statistics.some((statistic) => projectStatisticHasValue(statistic.value)));
}
function analysisKey(analysis: ReturnType<typeof analyses>[number]): string {
  return JSON.stringify([analysis.sourceIndex, analysis.label]);
}
function analysisOptions(name: string) {
  return analyses(name).map((analysis) => ({ label: analysis.label, value: analysisKey(analysis) }));
}
function selectedAnalysis(name: string) {
  const available = analyses(name);
  const selection = analysisSelections.value[name];
  return (selection ? available.find((analysis) => analysisKey(analysis) === selection) : undefined)
    ?? available.find((analysis) => analysis.aggregate);
}
function canChooseAnalysis(name: string): boolean {
  const available = analyses(name);
  return available.length > 1 || available.some((analysis) => !analysis.aggregate);
}
function selectedAnalysisKey(name: string): string | null {
  const analysis = selectedAnalysis(name);
  return analysis ? analysisKey(analysis) : null;
}
function selectAnalysis(name: string, value: string): void {
  analysisSelections.value[name] = value;
}
function availableStatistics(name: string) {
  return (selectedAnalysis(name)?.statistics ?? [])
    .filter((statistic) => projectStatisticHasValue(statistic.value))
    .sort((a, b) => a.priority - b.priority || a.sourceIndex - b.sourceIndex);
}
function canonicalStatisticKey(key: string): string { return statisticAliases.get(key) ?? key; }
function setMeasureVisibility(key: string, visible: boolean) {
  const selected = chosenMeasures.value ?? defaultMeasures.value;
  chosenMeasures.value = visible ? [...new Set([...selected, key])] : selected.filter((measure) => measure !== key);
}
function statisticCell(name: string, key: string): string {
  const statistic = selectedStatistics.value[name]?.get(key);
  return statistic ? statisticValue(statistic) : '—';
}
function statisticValue(statistic: ReturnType<typeof availableStatistics>[number]): string {
  if (statistic.key === 'most_frequent_ratio' && typeof statistic.value === 'number' && Number.isFinite(statistic.value)) return `${percentage(statistic.value, 1)} %`;
  return formatProjectStatistic(statistic.value);
}
function statisticLabel(statistic: ReturnType<typeof availableStatistics>[number]): string {
  const key = canonicalStatisticKey(statistic.key);
  if (key === 'median') return 'Median';
  if (key === 'nunique') return 'Distinct values (DrIA)';
  if (key === 'count') return 'Non-missing values (DrIA)';
  return key === 'most_frequent_ratio' ? 'Most frequent share (%)' : statistic.label;
}
</script>

<template>
  <div class="data-summary">
    <div class="summary-heading">
      <h2 class="text-subtitle1 text-weight-medium q-my-none">Understand your data</h2>
      <p class="text-caption text-grey-7 q-mt-xs q-mb-none">
        <template v-if="dataset.isComplete">
          Summary calculated from all observations in the file{{ filename ? ` “${filename}”` : '' }}.
          <template v-if="context === 'project'">Column roles are those assigned in the project.</template>
          <template v-else>It adapts to the role of each column.</template>
        </template>
        <template v-else>The summary will be available once the full file has been read.</template>
      </p>
    </div>

    <div v-if="!dataset.isComplete" class="summary-loading" role="status" aria-live="polite">
      <q-spinner color="primary" size="32px" aria-hidden="true" />
      <span>Analyzing the entire file…</span>
    </div>

    <template v-else>
      <div class="summary-metrics" aria-label="File overview">
        <div class="summary-metric"><strong>{{ number.format(dataset.rowCount) }}</strong><span>observations</span></div>
        <div class="summary-metric"><strong>{{ number.format(dataset.columns.length) }}</strong><span>imported columns</span></div>
        <div class="summary-metric"><strong>{{ number.format(predictors.length) }}</strong><span>features</span></div>
        <div class="summary-metric"><strong>{{ percentage(populatedCells, totalCells) }}<small> %</small></strong><span>non-missing cells</span></div>
      </div>
      <p class="text-caption text-grey-7 q-mt-sm q-mb-md">
        {{ number.format(populatedCells) }} non-missing cells out of {{ number.format(totalCells) }} in the full file, including excluded columns.
        <template v-if="exclusions.length">{{ exclusions.length }} column{{ exclusions.length > 1 ? 's are' : ' is' }} excluded from training.</template>
        <template v-if="groups.length">{{ groups.length }} column{{ groups.length > 1 ? 's are' : ' is' }} used for grouping, without being used for prediction.</template>
      </p>

      <section class="summary-section" aria-labelledby="summary-checks-title">
        <div class="section-title">
          <q-icon name="fact_check" color="primary" size="21px" aria-hidden="true" />
          <h3 id="summary-checks-title" class="text-subtitle2 q-my-none">{{ context === 'project' ? 'Points to review' : 'Things to check' }}</h3>
        </div>
        <ul v-if="checks.length" class="checks-list">
          <li v-for="check in checks" :key="check.key" class="data-check" :class="{ 'blocking-check': check.blocking && context === 'creation' }">
            <q-icon :name="check.blocking && context === 'creation' ? 'error_outline' : 'info_outline'" :color="check.blocking && context === 'creation' ? 'negative' : 'primary'" size="20px" aria-hidden="true" />
            <div>
              <div class="check-title">{{ check.title }} <span v-if="check.blocking && context === 'creation'" class="blocking-label">Before starting</span></div>
              <p class="text-caption text-grey-8 q-my-none">{{ check.detail }}</p>
            </div>
          </li>
        </ul>
        <p v-else class="text-caption text-grey-7 q-mt-sm q-mb-none">
          No missing or nonfinite values were detected in the selected columns, and no features are empty or constant.
        </p>
      </section>

      <section class="summary-section" aria-labelledby="summary-outcome-title">
        <div class="section-title">
          <q-icon name="flag" color="primary" size="21px" aria-hidden="true" />
          <h3 id="summary-outcome-title" class="text-subtitle2 q-my-none">The target column</h3>
        </div>
        <p v-if="!target" class="text-body2 text-grey-7 q-mt-sm q-mb-none">{{ context === 'project' ? 'The target column is not specified in this project configuration.' : 'Choose the target column in the “Predict” step to see its distribution here.' }}</p>
        <template v-else>
          <div class="outcome-column q-mt-sm">{{ targetName }}</div>
          <div class="text-caption text-grey-7 q-mb-sm">{{ number.format(target.populatedCount) }} observations with values · {{ number.format(target.missingCount) }} missing values</div>

          <template v-if="predictionKind === 'survival'">
            <div v-if="eventCount !== undefined" class="survival-counts">
              <div><strong>{{ number.format(eventCount) }}</strong><span>with an observed event</span><small>{{ percentage(eventCount, target.populatedCount) }} % of observations with values</small></div>
              <div><strong>{{ number.format(target.populatedCount - eventCount) }}</strong><span>without an observed event</span><small>The event was not observed during follow-up.</small></div>
            </div>
            <p v-else class="text-caption text-grey-7 q-my-sm">{{ context === 'project' ? 'Event counts are unavailable for the value configured in this project.' : 'Choose the observed-event value in the “Predict” step to display the counts.' }}</p>
            <div class="followup-details q-mt-sm">
              <template v-if="time">
                <div class="text-body2 text-weight-medium">Follow-up duration · {{ timeName }}</div>
                <div v-if="numericRange(time)" class="text-body2">From {{ numericRange(time) }} {{ timeUnit }}</div>
                <div v-else class="text-caption text-grey-7">Duration must be a finite numerical value.</div>
                <div class="text-caption text-grey-7">{{ number.format(time.missingCount) }} missing duration{{ time.missingCount > 1 ? 's' : '' }}</div>
              </template>
              <span v-else class="text-caption text-grey-7">{{ context === 'project' ? 'The follow-up duration column is unavailable in this file.' : 'The follow-up duration will appear here once you choose its column.' }}</span>
            </div>
          </template>

          <template v-else-if="predictionKind === 'quantity'">
            <div v-if="numericRange(target)" class="quantity-range"><span>Observed values</span><strong>{{ numericRange(target) }}</strong><small>Minimum and maximum across all non-missing values.</small></div>
            <p v-else class="text-caption text-grey-7 q-my-none">The outcome must be a finite numerical quantity.</p>
          </template>

          <template v-else-if="categoryCounts.length">
            <p class="text-caption text-grey-7 q-mt-none q-mb-sm">Distribution among the {{ number.format(target.populatedCount) }} observations with a recorded outcome.</p>
            <div v-for="(entry, index) in categoryCounts" :key="JSON.stringify(entry.value)" class="category-row">
              <div class="category-label"><span>{{ scalarLabel(entry.value) }}</span><span>{{ number.format(entry.count) }} · {{ percentage(entry.count, target.populatedCount) }} %</span></div>
              <q-linear-progress rounded size="8px" :value="ratio(entry.count, target.populatedCount)" :color="index % 2 ? 'accent' : 'primary'" track-color="grey-3" aria-hidden="true" />
            </div>
          </template>
          <p v-else class="text-caption text-grey-7 q-my-none">{{ distinctLabel(target) }} distinct values. The distribution is shown only when all categories are known and there are no more than 12.</p>
        </template>
      </section>

      <section class="summary-section" aria-labelledby="summary-columns-title">
        <div class="section-title q-mb-sm">
          <q-icon name="view_column" color="primary" size="21px" aria-hidden="true" />
          <h3 id="summary-columns-title" class="text-subtitle2 q-my-none">Column summary</h3>
        </div>
        <div v-if="statisticsLoading" class="statistics-status" role="status" aria-live="polite">
          <q-spinner color="primary" size="20px" aria-hidden="true" />
          <span>Calculating DrIA statistics automatically… The file summary remains available.</span>
        </div>
        <div v-else-if="statisticsError" class="statistics-status statistics-status-error" role="status">
          <q-icon name="error_outline" color="negative" size="20px" aria-hidden="true" />
          <div class="statistics-status-copy"><strong>DrIA statistics could not be calculated.</strong><span>{{ statisticsError }}</span></div>
          <q-btn flat dense no-caps color="primary" label="Try again" @click="emit('retry-statistics')" />
        </div>
        <div v-else-if="statisticsUnavailableReason" class="statistics-status" role="status">
          <q-icon name="info_outline" color="primary" size="20px" aria-hidden="true" />
          <span>{{ statisticsUnavailableReason }}</span>
        </div>
        <p v-else-if="hasColumnStatistics" class="text-caption text-grey-7 q-mt-none q-mb-sm">
          DrIA statistics add columns to the summary. Measures cover all observations by default; choose a subset under a feature name to view its results separately.
        </p>
        <p v-else-if="statistics" class="text-caption text-grey-7 q-mt-none q-mb-sm">DrIA did not return statistics for the columns in this file. The file summary remains available.</p>
        <div class="summary-table-tools q-mb-sm">
          <q-input v-model="search" outlined dense clearable label="Search the column summary" class="summary-search">
            <template #prepend><q-icon name="search" aria-hidden="true" /></template>
          </q-input>
          <q-btn-dropdown
            v-if="statisticColumns.length"
            outline dense no-caps color="primary" icon="view_column"
            :label="`DrIA measures: ${displayedStatisticColumns.length} / ${statisticColumns.length}`"
            :content-class="context === 'project' ? 'project-workspace-colors' : 'project-creation-colors'"
            @show="measureSearch = ''"
          >
            <div class="measure-menu q-pa-md">
              <div class="text-subtitle2">Visible measures</div>
              <p class="text-caption text-grey-7 q-mt-xs q-mb-sm">Each measure adds a column to the table.</p>
              <div class="row q-gutter-x-xs q-mb-sm">
                <q-btn flat dense no-caps color="primary" label="Main measures" @click="chosenMeasures = null" />
                <q-btn flat dense no-caps color="primary" label="Show all" @click="chosenMeasures = statisticColumns.map(measure => measure.key)" />
              </div>
              <q-input v-model="measureSearch" outlined dense clearable label="Search measures" class="q-mb-sm" />
              <div class="measure-list" role="group" aria-label="DrIA statistics to display">
                <q-checkbox
                  v-for="measure in filteredStatisticColumns"
                  :key="measure.key"
                  :model-value="displayedStatisticColumns.some(column => column.key === measure.key)"
                  dense color="primary" :label="measure.label" class="measure-option"
                  @update:model-value="setMeasureVisibility(measure.key, $event)"
                />
                <p v-if="!filteredStatisticColumns.length" class="text-caption text-grey-7 q-my-sm">No measures match your search.</p>
              </div>
            </div>
          </q-btn-dropdown>
        </div>
        <div v-if="filteredColumns.length" class="summary-table-scroll" tabindex="0" role="region" aria-label="Column summary with scrolling">
          <table class="summary-table" :style="{ '--summary-statistic-count': displayedStatisticColumns.length }">
            <caption class="sr-only">Role, type, completeness, distinct values, and DrIA statistics for all columns in the file</caption>
            <colgroup>
              <col class="variable-width">
              <col class="completeness-width">
              <col class="distinct-width">
              <col v-for="measure in displayedStatisticColumns" :key="measure.key" class="statistic-width">
            </colgroup>
            <thead>
              <tr>
                <th scope="col">Column and role</th>
                <th scope="col">Non-missing values</th>
                <th scope="col">Distinct values</th>
                <th v-for="measure in displayedStatisticColumns" :key="measure.key" scope="col" class="statistic-heading">{{ measure.label }}</th>
              </tr>
            </thead>
            <tbody>
              <tr v-for="name in filteredColumns" :key="name" :class="{ 'excluded-row': exclusions.includes(name) }">
                <th scope="row">
                  <div class="summary-column-name">{{ name }}</div>
                  <q-badge :color="roleColor(name)" :text-color="roleColor(name) === 'accent' ? 'dark' : 'white'" class="summary-role">{{ roleLabel(name) }}<q-tooltip v-if="roles[name]?.reason">{{ roles[name].reason }}</q-tooltip></q-badge>
                  <div class="text-caption text-grey-7 q-mt-xs">{{ typeLabel(dataset.profiles[name]) }}</div>
                  <q-select
                    v-if="canChooseAnalysis(name)"
                    :model-value="selectedAnalysisKey(name)"
                    :options="analysisOptions(name)"
                    emit-value map-options outlined dense
                    :label="`Observations for ${name}`"
                    class="statistics-subset q-mt-sm"
                    :popup-content-class="context === 'project' ? 'project-workspace-colors' : 'project-creation-colors'"
                    @update:model-value="selectAnalysis(name, $event)"
                  />
                  <p v-if="analyses(name).length && !selectedAnalysis(name)" class="text-caption text-grey-7 q-mt-xs q-mb-none">Choose a subset to view its statistics.</p>
                  <p v-else-if="statistics && !statisticsLoading && !analyses(name).length" class="text-caption text-grey-7 q-mt-xs q-mb-none">{{ exclusions.includes(name) ? 'Excluded from analysis' : 'Not analyzed by DrIA' }}</p>
                </th>
                <td>
                  <strong>{{ percentage(dataset.profiles[name]?.populatedCount ?? 0, dataset.rowCount) }} %</strong>
                  <div class="text-caption text-grey-7">{{ number.format(dataset.profiles[name]?.populatedCount ?? 0) }} / {{ number.format(dataset.rowCount) }} observations</div>
                  <div v-if="dataset.profiles[name]?.nonFiniteCount" class="text-caption text-negative">{{ number.format(dataset.profiles[name].nonFiniteCount) }} nonfinite value{{ dataset.profiles[name].nonFiniteCount > 1 ? 's' : '' }}</div>
                </td>
                <td>
                  <strong>{{ distinctLabel(dataset.profiles[name]) }}</strong>
                  <div v-if="numericRange(dataset.profiles[name])" class="text-caption text-grey-7">From {{ numericRange(dataset.profiles[name]) }}</div>
                </td>
                <td v-for="measure in displayedStatisticColumns" :key="measure.key" class="statistic-cell" :class="{ 'missing-statistic': !selectedStatistics[name]?.has(measure.key), 'numeric-statistic': typeof selectedStatistics[name]?.get(measure.key)?.value === 'number' }" :data-statistic="measure.key">
                  <div class="statistic-value" :title="statisticCell(name, measure.key)">{{ statisticCell(name, measure.key) }}</div>
                </td>
              </tr>
            </tbody>
          </table>
        </div>
        <p v-else class="text-caption text-grey-7 q-my-sm">No columns match your search.</p>
        <p v-if="displayedStatisticColumns.length" class="text-caption text-grey-7 q-mt-sm q-mb-none">Scroll horizontally to browse the measures. Feature names remain visible on the left.</p>
        <p class="text-caption text-grey-7 q-mt-sm q-mb-none">Distinct value counts exclude empty cells. Beyond the 100-value catalogue, the count shown is a lower bound.</p>
      </section>

      <slot />
    </template>
  </div>
</template>

<style scoped lang="scss">
.data-summary { min-width: 0; padding: 20px; background: var(--project-surface); border: 1px solid var(--project-border); border-radius: 8px; }
.summary-heading { overflow-wrap: anywhere; }
.summary-loading { display: flex; align-items: center; justify-content: center; gap: 14px; min-height: 180px; color: var(--project-muted); font-size: 14px; }
.summary-metrics { display: grid; grid-template-columns: repeat(4, minmax(0, 1fr)); gap: 10px; margin-top: 20px; }
.summary-metric { display: flex; flex-direction: column; padding: 14px 10px; background: var(--project-paper); border: 1px solid var(--project-border); border-radius: 12px; }
.summary-metric strong { color: var(--q-primary); font-size: 25px; line-height: 1.2; font-weight: 600; overflow-wrap: anywhere; }
.summary-metric small { font-size: 15px; }
.summary-metric span { margin-top: 5px; color: var(--project-muted); font-size: 12px; line-height: 1.4; }
.summary-section { border-top: 1px solid var(--project-border); padding-top: 18px; margin-top: 18px; min-width: 0; }
.section-title { display: flex; align-items: center; gap: 8px; }
.checks-list { list-style: none; padding: 0; margin: 10px 0 0; }
.data-check { display: flex; align-items: flex-start; gap: 10px; padding: 11px 12px; margin-top: 8px; border-radius: 10px; background: var(--project-paper); overflow-wrap: anywhere; }
.data-check > .q-icon { flex-shrink: 0; margin-top: 1px; }
.data-check > div { min-width: 0; }
.blocking-check { background: #fff3f2; }
.check-title { font-size: 13px; font-weight: 500; margin-bottom: 3px; }
.blocking-label { display: inline-block; color: #ab3340; font-size: 11px; font-weight: 500; margin-left: 5px; }
.outcome-column { font-size: 14px; font-weight: 500; color: var(--q-primary); overflow-wrap: anywhere; }
.category-row { margin: 11px 0; }
.category-label { display: flex; justify-content: space-between; align-items: flex-start; gap: 12px; font-size: 12px; margin-bottom: 5px; }
.category-label span:first-child { min-width: 0; overflow-wrap: anywhere; }
.category-label span:last-child { white-space: nowrap; color: var(--project-muted); }
.quantity-range, .survival-counts > div { display: flex; flex-direction: column; gap: 4px; padding: 12px; background: var(--project-paper); border-radius: 10px; }
.quantity-range span, .survival-counts span { font-size: 12px; }
.quantity-range strong, .survival-counts strong { color: var(--q-primary); font-size: 21px; font-weight: 500; overflow-wrap: anywhere; }
.quantity-range small, .survival-counts small { font-size: 11px; color: var(--project-muted); }
.survival-counts { display: grid; grid-template-columns: repeat(2, minmax(0, 1fr)); gap: 10px; }
.followup-details { overflow-wrap: anywhere; }
.summary-table-tools { display: flex; align-items: stretch; gap: 10px; }
.summary-search { flex: 1; min-width: 0; }
.summary-table-tools > .q-btn { flex-shrink: 0; font-size: 12px; }
.measure-menu { width: 340px; max-width: calc(100vw - 24px); }
.measure-list { display: flex; flex-direction: column; max-height: 300px; overflow-y: auto; }
.measure-option { min-width: 0; padding: 6px 2px; font-size: 12px; }
.measure-option :deep(.q-checkbox__label) { min-width: 0; overflow-wrap: anywhere; }
.summary-table-scroll { --summary-variable-width: 200px; overflow: auto; max-height: 430px; border: 1px solid var(--project-border); border-radius: 10px; }
.summary-table-scroll:focus-visible { outline: 2px solid var(--q-primary); outline-offset: 2px; }
.summary-table { width: 100%; min-width: calc(var(--summary-variable-width) + 272px + var(--summary-statistic-count) * 132px); border-collapse: separate; border-spacing: 0; text-align: left; font-size: 12px; table-layout: fixed; }
.variable-width { width: var(--summary-variable-width); }
.completeness-width { width: 140px; }
.distinct-width, .statistic-width { width: 132px; }
.summary-table th, .summary-table td { padding: 12px; border-bottom: 1px solid var(--project-border); vertical-align: top; overflow-wrap: anywhere; }
.summary-table thead th { position: sticky; top: 0; z-index: 2; background: var(--project-paper); color: var(--project-muted); font-weight: 500; }
.summary-table thead th:first-child { left: 0; z-index: 3; }
.summary-table thead .statistic-heading { color: var(--q-primary); box-shadow: inset 0 -2px var(--project-brand); }
.summary-table tbody th { position: sticky; left: 0; z-index: 1; background: var(--project-surface); font-weight: 400; }
.summary-table tr:last-child > * { border-bottom: 0; }
.summary-column-name { font-weight: 500; color: var(--project-ink); margin-bottom: 5px; }
.summary-role { max-width: 100%; white-space: normal; line-height: 1.3; }
.excluded-row { background: var(--project-paper); }
.excluded-row > th { background: var(--project-paper); }
.statistics-status { display: flex; align-items: center; flex-wrap: wrap; gap: 9px; padding: 11px 12px; margin-bottom: 12px; background: var(--project-tint); border-radius: 8px; color: var(--project-muted); font-size: 12px; overflow-wrap: anywhere; }
.statistics-status > .q-icon, .statistics-status > .q-spinner { flex-shrink: 0; }
.statistics-status > span, .statistics-status-copy { flex: 1; min-width: 140px; }
.statistics-status-copy { display: flex; flex-direction: column; gap: 3px; }
.statistics-status-copy strong { font-weight: 500; }
.statistics-status-error { background: #fff3f2; }
.statistic-cell { color: var(--project-ink); font-weight: 500; font-variant-numeric: tabular-nums; }
.numeric-statistic { text-align: right; }
.missing-statistic { color: var(--project-muted); font-weight: 400; }
.statistic-value { max-height: 112px; overflow: auto; white-space: pre-wrap; overscroll-behavior: contain; }
.statistics-subset :deep(.q-field__control) { min-height: 40px; height: auto; }
.statistics-subset :deep(.q-field__native) { min-width: 0; white-space: normal; overflow-wrap: anywhere; font-size: 12px; }
.sr-only { position: absolute; width: 1px; height: 1px; padding: 0; overflow: hidden; clip: rect(0, 0, 0, 0); white-space: nowrap; border: 0; }
@media (max-width: 900px) { .summary-metrics { grid-template-columns: repeat(2, minmax(0, 1fr)); } }
@media (max-width: 599px) {
  .data-summary { padding: 14px; }
  .summary-metric strong { font-size: 23px; }
  .survival-counts { grid-template-columns: minmax(0, 1fr); }
  .summary-table-tools { flex-direction: column; }
  .summary-table-tools > .q-btn { align-self: flex-start; }
  .summary-table-scroll { --summary-variable-width: 160px; }
}
</style>
