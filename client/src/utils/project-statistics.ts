import { IStatistics } from 'src/components/models';
import { displayServiceName } from './service-label';

export interface ProjectStatistic {
  key: string;
  label: string;
  value: unknown;
  priority: number;
  sourceIndex: number;
}

export interface ProjectColumnStatistics {
  sourceIndex: number;
  label: string;
  aggregate: boolean;
  subset?: unknown;
  statistics: ProjectStatistic[];
}

interface ColumnReference {
  column: string;
  label: string;
  aggregate: boolean;
  subset?: unknown;
}

const decimalNumber = new Intl.NumberFormat('en-US', { maximumSignificantDigits: 6 });
const scientificNumber = new Intl.NumberFormat('en-US', { notation: 'scientific', maximumSignificantDigits: 6 });
const priorities = [
  ['count'], ['null_count'], ['missing_rate'], ['mean'], ['median', '50%', 'quantile_0.5'],
  ['std', 'stdev', 'standard_deviation'], ['min'], ['max'], ['25%', 'quantile_0.25'],
  ['75%', 'quantile_0.75'], ['unique', 'nunique', 'unique_count'], ['top', 'mode'],
  ['freq', 'most_frequent_ratio'], ['top_5_value_counts'],
];
const labels: Record<string, string> = {
  count: 'Non-missing values',
  null_count: 'Missing values',
  missing_rate: 'Missing value proportion (0 to 1)',
  mean: 'Mean',
  std: 'Standard deviation',
  stdev: 'Standard deviation',
  standard_deviation: 'Standard deviation',
  variance: 'Variance',
  median: 'Median',
  min: 'Minimum',
  '25%': 'First quartile (25%)',
  '50%': 'Median (50%)',
  '75%': 'Third quartile (75%)',
  max: 'Maximum',
  range: 'Range (maximum − minimum)',
  unique: 'Distinct values',
  nunique: 'Distinct values',
  unique_count: 'Distinct values',
  value_counts: 'Count of each value',
  top: 'Most frequent value',
  mode: 'Most frequent value',
  freq: 'Count of the most frequent value',
  most_frequent_ratio: 'Most frequent value proportion (0 to 1)',
  rare_category_rate: 'Rare category proportion (0 to 1)',
  cardinality_ratio: 'Distinct values / observations ratio',
  coef_variation: 'Coefficient of variation',
  iqr: 'Interquartile range',
  mad: 'Median absolute deviation',
  median_absolute_deviation: 'Median absolute deviation',
  skewness: 'Distribution skewness',
  kurtosis: 'Distribution kurtosis',
  entropy: 'Entropy',
  outlier_count_iqr: 'Outliers by quartiles',
  chi_square: 'Chi-square statistic',
  anova: 'ANOVA statistic',
  correlation_with_target: 'Correlation with the target',
  effect_size: 'Effect size',
  grouped_mean: 'Mean by group',
  category_cooccurrence: 'Category co-occurrence counts',
  summary_table: 'Data summary',
  duplicate_rows: 'Duplicate observations',
  data_type_summary: 'Column type distribution',
  event_rate: 'Event frequency',
  time_summary: 'Follow-up duration summary',
  time_by_group: 'Follow-up durations by group',
};

function statisticKey(label: unknown): string {
  return typeof label === 'string' ? label.trim().toLowerCase().replace(/[\s-]+/g, '_') : jsonLabel(label);
}

function jsonLabel(value: unknown): string {
  return typeof value === 'string' ? value : JSON.stringify(value) ?? 'Unavailable';
}

function statisticLabel(label: unknown): string {
  if (typeof label !== 'string') return label === null || label === undefined ? 'Statistic' : jsonLabel(label);
  const key = statisticKey(label);
  if (Object.prototype.hasOwnProperty.call(labels, key)) return labels[key];
  const quantile = key.match(/^quantile_(0(?:\.\d+)?|1(?:\.0+)?)$/);
  if (quantile) {
    const percentile = Number(quantile[1]) * 100;
    return labels[`${percentile}%`] ?? `${decimalNumber.format(percentile)}% quantile`;
  }
  const topValues = key.match(/^top_(\d+)_value_counts$/);
  return topValues ? `Counts of the ${topValues[1]} most frequent values` : displayServiceName(label);
}

function columnReference(column: string, subset: unknown, aggregate: boolean): ColumnReference {
  return { column, aggregate, subset, label: aggregate ? 'All observations' : `Subset: ${jsonLabel(subset)}` };
}

function structuredColumnReference(label: unknown, sourceColumns: Set<string>): ColumnReference | undefined {
  // A two-level JSON label retains the source column and subset separately.
  if (Array.isArray(label)) {
    if (label.length !== 2 || typeof label[0] !== 'string' || !sourceColumns.has(label[0])) return;
    return columnReference(label[0], label[1], label[1] === 'all');
  }
  if (!label || typeof label !== 'object') return;
  const reference = label as Record<string, unknown>;
  const column = reference.source_column ?? reference.column;
  if (typeof column !== 'string' || !sourceColumns.has(column)) return;
  const subsetKey = ['subset', 'group', 'class', 'scope'].find((key) => Object.prototype.hasOwnProperty.call(reference, key));
  const subset = subsetKey ? reference[subsetKey] : undefined;
  const aggregate = typeof reference.aggregate === 'boolean' ? reference.aggregate : !subsetKey || subset === 'all';
  return columnReference(column, subsetKey ? subset : label, aggregate);
}

/**
 * Attach IAML's split table to CSV columns, without combining subsets.
 * Native classification labels use `${column}_all` and `${column}_${class}`;
 * regression labels use the exact source name. Ambiguous flattened labels and
 * synthetic columns (dataset summaries or pairs of features) remain unattached.
 */
export function getProjectColumnStatistics(
  result: IStatistics | undefined,
  sourceColumns: string[],
  predictionKind?: 'category' | 'quantity' | 'survival',
): Record<string, ProjectColumnStatistics[]> {
  const mapped: Record<string, ProjectColumnStatistics[]> = Object.create(null);
  const sources = new Set(sourceColumns);
  sources.forEach((column) => { mapped[column] = []; });
  const table = result?.statistics;
  if (!table || !Array.isArray(table.columns) || !Array.isArray(table.index)) return mapped;

  const nativeLabels = new Set(table.columns.filter((label): label is string => typeof label === 'string'));
  const aggregateSources = new Set([...sources].filter((column) => nativeLabels.has(`${column}_all`)));
  // An output outside the source schema distinguishes native classification
  // suffixes from genuine CSV names such as "age_all" in regression data.
  const grouped = predictionKind === 'category'
    || (predictionKind !== 'quantity' && [...aggregateSources].some((column) => !sources.has(`${column}_all`)));
  const syntheticBases = [...nativeLabels]
    .filter((label) => label.endsWith('_all') && !sources.has(label.slice(0, -4)))
    .map((label) => label.slice(0, -4));

  table.columns.forEach((label: unknown, sourceIndex: number) => {
    let reference: ColumnReference | undefined;
    if (typeof label !== 'string') {
      reference = structuredColumnReference(label, sources);
    } else {
      const aggregateColumn = label.endsWith('_all') ? label.slice(0, -4) : undefined;
      if (predictionKind === 'quantity' && sources.has(label)) {
        reference = columnReference(label, undefined, true);
      } else if (aggregateColumn !== undefined && sources.has(aggregateColumn)) {
        // If this label is also a real column name, only an established native
        // grouped representation resolves whether it describes the base column.
        if (grouped || !sources.has(label)) reference = columnReference(aggregateColumn, undefined, true);
      } else if (!grouped && sources.has(label)) {
        reference = columnReference(label, undefined, true);
      } else if (!syntheticBases.some((column) => label.startsWith(`${column}_`))) {
        const candidates = [...sources].filter((column) => label.startsWith(`${column}_`));
        // "age_A_B" cannot safely distinguish (age, A_B) from (age_A, B).
        if (candidates.length === 1 && !label.startsWith(`${candidates[0]}__`)) {
          const column = candidates[0];
          reference = columnReference(column, label.slice(column.length + 1), false);
        }
      }
    }
    if (!reference) return;

    const statistics = table.index.map((label: unknown, rowIndex: number) => {
      const key = statisticKey(label);
      const priority = priorities.findIndex((group) => group.includes(key));
      return {
        key,
        label: statisticLabel(label),
        value: table.data?.[rowIndex]?.[sourceIndex],
        priority: priority < 0 ? 100 : priority,
        sourceIndex: rowIndex,
      };
    }).sort((a, b) => a.priority - b.priority || a.sourceIndex - b.sourceIndex);
    mapped[reference.column].push({ sourceIndex, label: reference.label, aggregate: reference.aggregate, subset: reference.subset, statistics });
  });
  Object.values(mapped).forEach((analyses) => analyses.sort((a, b) => Number(b.aggregate) - Number(a.aggregate) || a.sourceIndex - b.sourceIndex));
  return mapped;
}

export function projectStatisticHasValue(value: unknown): boolean {
  return value !== null && value !== undefined && (typeof value !== 'number' || Number.isFinite(value));
}

export function formatProjectStatistic(value: unknown): string {
  if (!projectStatisticHasValue(value)) return 'Unavailable';
  if (typeof value === 'number') {
    const magnitude = Math.abs(value);
    return (magnitude > 0 && magnitude < 0.0001) || magnitude >= 1e9
      ? scientificNumber.format(value)
      : decimalNumber.format(value);
  }
  // Keep arrays, objects, boolean values and their JSON types intact.
  return jsonLabel(value);
}
