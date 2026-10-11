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

const decimalNumber = new Intl.NumberFormat('fr-FR', { maximumSignificantDigits: 6 });
const scientificNumber = new Intl.NumberFormat('fr-FR', { notation: 'scientific', maximumSignificantDigits: 6 });
const priorities = [
  ['count'], ['null_count'], ['missing_rate'], ['mean'], ['median', '50%', 'quantile_0.5'],
  ['std', 'stdev', 'standard_deviation'], ['min'], ['max'], ['25%', 'quantile_0.25'],
  ['75%', 'quantile_0.75'], ['unique', 'nunique', 'unique_count'], ['top', 'mode'],
  ['freq', 'most_frequent_ratio'], ['top_5_value_counts'],
];
const labels: Record<string, string> = {
  count: 'Valeurs renseignées',
  null_count: 'Valeurs manquantes',
  missing_rate: 'Proportion de valeurs manquantes (0 à 1)',
  mean: 'Moyenne',
  std: 'Écart type',
  stdev: 'Écart type',
  standard_deviation: 'Écart type',
  variance: 'Variance',
  median: 'Médiane',
  min: 'Minimum',
  '25%': 'Premier quartile (25 %)',
  '50%': 'Médiane (50 %)',
  '75%': 'Troisième quartile (75 %)',
  max: 'Maximum',
  range: 'Étendue (maximum − minimum)',
  unique: 'Valeurs distinctes',
  nunique: 'Valeurs distinctes',
  unique_count: 'Valeurs distinctes',
  value_counts: 'Effectifs de chaque valeur',
  top: 'Valeur la plus fréquente',
  mode: 'Valeur la plus fréquente',
  freq: 'Occurrences de la valeur la plus fréquente',
  most_frequent_ratio: 'Proportion de la valeur la plus fréquente (0 à 1)',
  rare_category_rate: 'Proportion de catégories rares (0 à 1)',
  cardinality_ratio: 'Ratio valeurs distinctes / observations',
  coef_variation: 'Coefficient de variation',
  iqr: 'Écart interquartile',
  mad: 'Écart absolu médian',
  median_absolute_deviation: 'Écart absolu médian',
  skewness: 'Asymétrie de la distribution',
  kurtosis: 'Aplatissement de la distribution',
  entropy: 'Entropie',
  outlier_count_iqr: 'Valeurs atypiques selon les quartiles',
  chi_square: 'Statistique du khi-deux',
  anova: 'Statistique ANOVA',
  correlation_with_target: 'Corrélation avec la cible',
  effect_size: 'Taille d’effet',
  grouped_mean: 'Moyenne par groupe',
  category_cooccurrence: 'Effectifs des associations de catégories',
  summary_table: 'Résumé des données',
  duplicate_rows: 'Observations dupliquées',
  data_type_summary: 'Répartition des types de colonnes',
  event_rate: 'Fréquence des événements',
  time_summary: 'Résumé des durées de suivi',
  time_by_group: 'Durées de suivi par groupe',
};

function statisticKey(label: unknown): string {
  return typeof label === 'string' ? label.trim().toLowerCase().replace(/[\s-]+/g, '_') : jsonLabel(label);
}

function jsonLabel(value: unknown): string {
  return typeof value === 'string' ? value : JSON.stringify(value) ?? 'Indisponible';
}

function statisticLabel(label: unknown): string {
  if (typeof label !== 'string') return label === null || label === undefined ? 'Statistique' : jsonLabel(label);
  const key = statisticKey(label);
  if (Object.prototype.hasOwnProperty.call(labels, key)) return labels[key];
  const quantile = key.match(/^quantile_(0(?:\.\d+)?|1(?:\.0+)?)$/);
  if (quantile) {
    const percentile = Number(quantile[1]) * 100;
    return labels[`${percentile}%`] ?? `Quantile à ${decimalNumber.format(percentile)} %`;
  }
  const topValues = key.match(/^top_(\d+)_value_counts$/);
  return topValues ? `Effectifs des ${topValues[1]} valeurs les plus fréquentes` : displayServiceName(label);
}

function columnReference(column: string, subset: unknown, aggregate: boolean): ColumnReference {
  return { column, aggregate, subset, label: aggregate ? 'Toutes les observations' : `Sous-ensemble : ${jsonLabel(subset)}` };
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
  if (!projectStatisticHasValue(value)) return 'Indisponible';
  if (typeof value === 'number') {
    const magnitude = Math.abs(value);
    return (magnitude > 0 && magnitude < 0.0001) || magnitude >= 1e9
      ? scientificNumber.format(value)
      : decimalNumber.format(value);
  }
  // Keep arrays, objects, boolean values and their JSON types intact.
  return jsonLabel(value);
}
