import { IStatistics } from 'src/components/models';
import { ColumnProfile, ProjectDataset } from './project-data';
import { PredictionKind } from './project-study';
import { getProjectColumnStatistics, ProjectColumnStatistics } from './project-statistics';

export type ProjectAnalysisKind = 'missing' | 'comparison' | 'association';

export interface InsightBar {
  label: string;
  value: number;
  detail: string;
}

export interface InsightBox {
  label: string;
  min: number;
  q1: number;
  median: number;
  q3: number;
  max: number;
  count?: number;
}

export interface InsightProportionGroup {
  label: string;
  counts: number[];
  present: number;
  missing?: number;
}

export type ProjectInsightChartData =
  | { type: 'bars'; unit: 'percent' | 'coefficient'; min: number; max: number; rows: InsightBar[] }
  | { type: 'boxes'; boxes: InsightBox[] }
  | { type: 'proportions'; categories: string[]; groups: InsightProportionGroup[] };

export interface ProjectInsight {
  title: string;
  description: string;
  source: string;
  note: string;
  chart: ProjectInsightChartData;
  omittedCount?: number;
}

const MAX_COLUMNS = 12;
const MAX_CLASSES = 6;
const MAX_CATEGORIES = 6;
const integer = new Intl.NumberFormat('fr-FR');

function finite(value: unknown): value is number {
  return typeof value === 'number' && Number.isFinite(value);
}

function count(value: unknown): value is number {
  return finite(value) && Number.isInteger(value) && value >= 0;
}

/** Recipe aliases may prefix the native key, e.g. "count : null_count". */
function nativeKey(key: string): string {
  return key.split(':').pop()?.replace(/^_+|_+$/g, '') ?? '';
}

function statistic(analysis: ProjectColumnStatistics, key: string): unknown {
  const matches = analysis.statistics.filter((entry) => nativeKey(entry.key) === key);
  if (!matches.length) return undefined;
  // Multiple recipe variants must not silently become a single statistic.
  const serialized = JSON.stringify(matches[0].value);
  return matches.every((entry) => JSON.stringify(entry.value) === serialized) ? matches[0].value : undefined;
}

function varying(profile: ColumnProfile | undefined): boolean {
  return Boolean(profile && profile.populatedCount >= 2 && profile.nonFiniteCount === 0
    && (profile.distinctValuesTruncated || profile.distinctValues.length >= 2));
}

function numericFeature(profile: ColumnProfile | undefined): boolean {
  return varying(profile) && profile?.type === 'numeric'
    && finite(profile.min) && finite(profile.max) && profile.min < profile.max;
}

function subsetLabel(subset: unknown): string | undefined {
  if (typeof subset === 'string') return subset;
  if (typeof subset === 'boolean' || finite(subset)) return String(subset);
  return undefined;
}

function classAnalyses(analyses: ProjectColumnStatistics[]): ProjectColumnStatistics[] {
  if (analyses.filter((analysis) => analysis.aggregate).length > 1) return [];
  const classes = analyses.filter((analysis) => !analysis.aggregate && subsetLabel(analysis.subset) !== undefined);
  const labels = classes.map((analysis) => subsetLabel(analysis.subset));
  // Flattened class names have already been checked against the source schema.
  // Duplicate class labels are still ambiguous and cannot be combined safely.
  return new Set(labels).size === labels.length ? classes : [];
}

function box(analysis: ProjectColumnStatistics): InsightBox | undefined {
  const label = subsetLabel(analysis.subset);
  const values = ['min', 'quantile_0.25', 'quantile_0.5', 'quantile_0.75', 'max']
    .map((key) => statistic(analysis, key));
  if (label === undefined || !values.every(finite)) return undefined;
  const [min, q1, median, q3, max] = values as number[];
  if (min > q1 || q1 > median || median > q3 || q3 > max) return undefined;
  const present = statistic(analysis, 'count');
  if (present !== undefined && (!count(present) || present === 0)) return undefined;
  return { label, min, q1, median, q3, max, ...(count(present) ? { count: present } : {}) };
}

interface CategoryCount {
  key: string;
  label: string;
  count: number;
}

interface CategoryGroup {
  label: string;
  values: CategoryCount[];
  present: number;
  missing?: number;
}

function categoryGroup(analysis: ProjectColumnStatistics): CategoryGroup | undefined {
  const label = subsetLabel(analysis.subset);
  const pairs = statistic(analysis, 'value_counts');
  if (label === undefined || !Array.isArray(pairs) || !pairs.length) return undefined;
  const values: CategoryCount[] = [];
  for (const pair of pairs) {
    if (!Array.isArray(pair) || pair.length !== 2 || !count(pair[1])) return undefined;
    const category = subsetLabel(pair[0]);
    if (category === undefined) return undefined;
    values.push({ key: `${typeof pair[0]}:${JSON.stringify(pair[0])}`, label: category, count: pair[1] });
  }
  if (new Set(values.map((value) => value.key)).size !== values.length) return undefined;
  const present = values.reduce((sum, value) => sum + value.count, 0);
  if (present <= 0) return undefined;
  const reportedPresent = statistic(analysis, 'count');
  if (reportedPresent !== undefined && (!count(reportedPresent) || reportedPresent !== present)) return undefined;
  const missing = statistic(analysis, 'null_count');
  return { label, values, present, ...(count(missing) ? { missing } : {}) };
}

interface ComparisonData {
  chart: Extract<ProjectInsightChartData, { type: 'boxes' | 'proportions' }>;
  combinedCategories: boolean;
}

function comparisonData(analyses: ProjectColumnStatistics[], profile: ColumnProfile | undefined): ComparisonData | undefined {
  if (!varying(profile)) return undefined;
  const classes = classAnalyses(analyses);
  if (classes.length < 2 || classes.length > MAX_CLASSES) return undefined;
  const boxes = numericFeature(profile) ? classes.map(box).filter((value): value is InsightBox => Boolean(value)) : [];
  if (boxes.length === classes.length) {
    return {
      chart: { type: 'boxes', boxes },
      combinedCategories: false,
    };
  }

  const groups = classes.map(categoryGroup).filter((value): value is CategoryGroup => Boolean(value));
  if (groups.length !== classes.length) return undefined;
  const displayedGroups = groups;
  const catalogue = new Map<string, CategoryCount>();
  displayedGroups.forEach((group) => group.values.forEach((value) => {
    const existing = catalogue.get(value.key);
    catalogue.set(value.key, { ...value, count: (existing?.count ?? 0) + value.count });
  }));
  const categories = [...catalogue.values()].filter((value) => value.count > 0).sort((a, b) => b.count - a.count);
  if (categories.length < 2 || new Set(categories.map((value) => value.label)).size !== categories.length) return undefined;
  const combinedCategories = categories.length > MAX_CATEGORIES;
  const selected = categories.slice(0, combinedCategories ? MAX_CATEGORIES - 1 : MAX_CATEGORIES);
  let otherLabel = 'Autres catégories';
  while (categories.some((category) => category.label === otherLabel)) otherLabel = `${otherLabel} (regroupées)`;
  return {
    chart: {
      type: 'proportions',
      categories: [...selected.map((category) => category.label), ...(combinedCategories ? [otherLabel] : [])],
      groups: displayedGroups.map((group) => {
        const values = new Map(group.values.map((value) => [value.key, value.count]));
        const counts = selected.map((category) => values.get(category.key) ?? 0);
        if (combinedCategories) counts.push(group.present - counts.reduce((sum, value) => sum + value, 0));
        return { label: group.label, counts, present: group.present, ...(group.missing !== undefined ? { missing: group.missing } : {}) };
      }),
    },
    combinedCategories,
  };
}

/** Full-file profiles, rather than the bounded table preview, define coverage. */
export function buildMissingInsight(dataset: ProjectDataset, columns: string[]): ProjectInsight | null {
  if (!dataset.isComplete || !count(dataset.rowCount) || dataset.rowCount === 0) return null;
  const rows: InsightBar[] = [...new Set(columns)].flatMap((column) => {
    const profile = dataset.profiles[column];
    if (!profile || !count(profile.missingCount) || profile.missingCount === 0
      || profile.missingCount > dataset.rowCount || profile.populatedCount + profile.missingCount !== dataset.rowCount) return [];
    return [{ label: column, value: 100 * profile.missingCount / dataset.rowCount,
      detail: `${integer.format(profile.missingCount)} valeurs manquantes sur ${integer.format(dataset.rowCount)} observations` }];
  }).sort((a, b) => b.value - a.value);
  if (!rows.length) return null;
  return {
    title: 'Où manque-t-il des données ?',
    description: 'Comparez les colonnes à compléter ou à prendre en compte lors de la préparation des données.',
    source: 'Profil du fichier complet',
    note: 'Chaque barre représente la proportion de cellules vides dans une colonne. Seules les colonnes sélectionnées contenant des valeurs manquantes sont affichées.',
    chart: { type: 'bars', unit: 'percent', min: 0, max: 100, rows: rows.slice(0, MAX_COLUMNS) },
    omittedCount: Math.max(0, rows.length - MAX_COLUMNS),
  };
}

export function comparisonColumns(
  statistics: IStatistics | undefined,
  dataset: ProjectDataset,
  columns: string[],
  predictionKind: PredictionKind,
): string[] {
  if (!dataset.isComplete || predictionKind !== 'category') return [];
  const mapped = getProjectColumnStatistics(statistics, dataset.columns, predictionKind);
  return [...new Set(columns)].filter((column) => comparisonData(mapped[column] ?? [], dataset.profiles[column]) !== undefined);
}

export function buildComparisonInsight(
  statistics: IStatistics | undefined,
  dataset: ProjectDataset,
  column: string,
  targetColumn: string,
  predictionKind: PredictionKind,
): ProjectInsight | null {
  if (!dataset.isComplete || predictionKind !== 'category' || column === targetColumn) return null;
  const mapped = getProjectColumnStatistics(statistics, dataset.columns, predictionKind);
  const comparison = comparisonData(mapped[column] ?? [], dataset.profiles[column]);
  if (!comparison) return null;
  const note = comparison.chart.type === 'boxes'
    ? 'La boîte va du premier au troisième quartile et le trait central indique la médiane. Les extrémités indiquent le minimum et le maximum, pas les seuils de valeurs atypiques. Les valeurs manquantes sont exclues.'
    : `Chaque groupe totalise 100 % des valeurs renseignées : les proportions permettent de comparer des groupes de tailles différentes. Les valeurs manquantes sont exclues du calcul.${comparison.combinedCategories ? ' Les catégories les moins fréquentes sont regroupées dans « Autres catégories ».' : ''}`;
  return {
    title: `${column} selon ${targetColumn}`,
    description: comparison.chart.type === 'boxes'
      ? 'Comparez les valeurs de cette information entre les catégories du résultat à prédire.'
      : 'Comparez la répartition des catégories de cette information selon le résultat à prédire.',
    source: 'Statistiques descriptives DrIA',
    note,
    chart: comparison.chart,
  };
}

export function buildAssociationInsight(
  statistics: IStatistics | undefined,
  dataset: ProjectDataset,
  columns: string[],
  targetColumn: string,
  predictionKind: PredictionKind,
): ProjectInsight | null {
  if (!dataset.isComplete || predictionKind === 'survival') return null;
  const mapped = getProjectColumnStatistics(statistics, dataset.columns, predictionKind);
  const rows: InsightBar[] = [...new Set(columns)].flatMap((column) => {
    if (column === targetColumn || !numericFeature(dataset.profiles[column])) return [];
    const aggregates = (mapped[column] ?? []).filter((analysis) => analysis.aggregate);
    if (aggregates.length !== 1) return [];
    const value = statistic(aggregates[0], 'correlation_with_target');
    if (!finite(value) || value > 1 || value < (predictionKind === 'category' ? 0 : -1)) return [];
    return [{ label: column, value, detail: predictionKind === 'category'
      ? 'Rapport de corrélation eta : 0 à 1'
      : 'Corrélation de Pearson : −1 à 1' }];
  }).sort((a, b) => Math.abs(b.value) - Math.abs(a.value));
  if (rows.length < 2) return null;
  return {
    title: `Quelles informations sont associées à ${targetColumn} ?`,
    description: predictionKind === 'category'
      ? 'Ce coefficient résume les différences de valeurs numériques entre les catégories du résultat.'
      : 'Ce coefficient résume le lien linéaire entre chaque information numérique et le résultat à prédire.',
    source: 'Statistiques descriptives DrIA',
    note: predictionKind === 'category'
      ? 'Le rapport eta va de 0 à 1 : une valeur plus élevée indique davantage de différences entre les catégories. Il ne donne pas de direction. Ce lien descriptif ne mesure ni la performance d’un modèle ni une relation de cause à effet.'
      : 'Pearson va de −1 à 1 : le signe indique le sens du lien et la proximité de −1 ou 1 un lien linéaire plus marqué. Un coefficient proche de 0 n’exclut pas un autre type de lien. Ce résultat descriptif ne mesure ni l’importance dans un modèle ni une relation de cause à effet.',
    chart: { type: 'bars', unit: 'coefficient', min: predictionKind === 'category' ? 0 : -1, max: 1, rows: rows.slice(0, MAX_COLUMNS) },
    omittedCount: Math.max(0, rows.length - MAX_COLUMNS),
  };
}
