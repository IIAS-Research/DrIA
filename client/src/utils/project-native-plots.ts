import { IPlot, IRecipe, IStatistics } from 'src/components/models';
import { ColumnProfile, ProjectDataset, ProjectScalar } from './project-data';
import { getProjectColumnStatistics, ProjectColumnStatistics } from './project-statistics';
import { PredictionKind } from './project-study';

export interface NativeDataPlotPlan {
  key: string;
  columns: string[];
  statistics: IRecipe;
  title: string;
  description: string;
  note: string;
  nativeTitle: 'Box plot' | 'Bar plot' | 'Missingness heatmap';
  /** Keep target interpretation explicit when validating the limited response. */
  predictionKind: PredictionKind;
  expectedGroups?: string[];
  /** Compare numeric/boolean identities despite Python's flattened spelling. */
  expectedGroupKind?: 'numeric' | 'boolean' | 'text';
  expectedGroupCount?: number;
  expectedMissingRates?: Record<string, number>;
}

const BOX_ROWS = ['min', 'quantile_0.25', 'quantile_0.5', 'quantile_0.75', 'max'];
const MAX_GROUPS = 6;
const MAX_CATEGORIES = 6;
const MAX_MISSING_COLUMNS = 8;
const NUMERIC_LABEL = /^[+-]?(?:\d+\.?\d*|\.\d+)(?:e[+-]?\d+)?$/i;

function finite(value: unknown): value is number {
  return typeof value === 'number' && Number.isFinite(value);
}

function natural(value: unknown): value is number {
  return finite(value) && Number.isInteger(value) && value >= 0;
}

function recipe(children: IRecipe[]): IRecipe {
  return { type: 'collection', kind: 'statistics', defaults: false, children };
}

function boxRecipe(): IRecipe {
  // PyIAML 1.1 computes both bounds and all five quantiles in each component.
  // Declaring one component per percentile would duplicate the native rows.
  return recipe([
    { type: 'use', component: 'BoundStatistic', params: { bound: { type: 'const', value: 'min' } } },
    { type: 'use', component: 'QuantileStatistic', params: { percentile: { type: 'const', value: 0.5 } } },
    { type: 'use', component: 'CountStatistic' },
  ]);
}

function scalarLabel(value: unknown): string | undefined {
  return typeof value === 'string' || typeof value === 'boolean' || finite(value) ? String(value) : undefined;
}

function value(analysis: ProjectColumnStatistics, key: string): unknown {
  const matches = analysis.statistics.filter((statistic) => statistic.key === key);
  return matches.length === 1 ? matches[0].value : undefined;
}

function categoryCounts(analysis: ProjectColumnStatistics): { values: ProjectScalar[]; present: number } | undefined {
  const pairs = value(analysis, 'value_counts');
  if (!Array.isArray(pairs) || !pairs.length) return;
  const values: ProjectScalar[] = [];
  const seen = new Set<string>();
  let present = 0;
  for (const pair of pairs) {
    if (!Array.isArray(pair) || pair.length !== 2 || scalarLabel(pair[0]) === undefined || !natural(pair[1])) return;
    const key = `${typeof pair[0]}:${JSON.stringify(pair[0])}`;
    if (seen.has(key)) return;
    seen.add(key);
    if (pair[1] > 0) values.push(pair[0] as ProjectScalar);
    present += pair[1];
  }
  const count = value(analysis, 'count');
  if (present <= 0 || (count !== undefined && (!natural(count) || count !== present))) return;
  return { values, present };
}

function smallCategoryCatalogue(analyses: ProjectColumnStatistics[]): boolean {
  const groups = analyses.filter((analysis) => !analysis.aggregate);
  const categories = new Set<string>();
  for (const group of groups) {
    const counts = categoryCounts(group);
    if (!counts) return false;
    counts.values.forEach((category) => categories.add(`${typeof category}:${JSON.stringify(category)}`));
  }
  return groups.length >= 2 && groups.length <= MAX_GROUPS && categories.size >= 2 && categories.size <= MAX_CATEGORIES;
}

function completeProfile(profile: ColumnProfile | undefined, rowCount: number): profile is ColumnProfile {
  return Boolean(profile && natural(profile.populatedCount) && natural(profile.missingCount)
    && profile.populatedCount + profile.missingCount === rowCount && profile.nonFiniteCount === 0);
}

function categoricalLabels(profile: ColumnProfile, maximum: number): string[] | undefined {
  if (profile.distinctValuesTruncated || !Array.isArray(profile.distinctValues)) return;
  const labels = profile.distinctValues.map(scalarLabel);
  if (labels.length < 2 || labels.length > maximum
    || labels.some((label) => label === undefined || !label.trim())
    || new Set(labels).size !== labels.length) return;
  return labels as string[];
}

function groupIdentity(label: string, kind: NativeDataPlotPlan['expectedGroupKind']): string | undefined {
  if (kind === 'numeric') {
    if (!NUMERIC_LABEL.test(label)) return;
    const numeric = Number(label);
    if (!finite(numeric) || (Number.isInteger(numeric) && !Number.isSafeInteger(numeric))) return;
    return `number:${numeric}`;
  }
  if (kind === 'boolean') {
    const boolean = label.toLowerCase();
    return boolean === 'true' || boolean === 'false' ? `boolean:${boolean}` : undefined;
  }
  return `string:${JSON.stringify(label)}`;
}

function comparisonProfile(
  dataset: ProjectDataset,
  column: string,
  targetColumn: string,
  predictionKind: PredictionKind,
): { nativeTitle: 'Box plot' | 'Bar plot'; groups: string[]; groupKind: NativeDataPlotPlan['expectedGroupKind'] } | undefined {
  if (!dataset.isComplete || !natural(dataset.rowCount) || !dataset.rowCount
    || predictionKind !== 'category' || column === targetColumn
    || !dataset.columns.includes(column) || !dataset.columns.includes(targetColumn)) return;
  const target = dataset.profiles[targetColumn];
  if (!completeProfile(target, dataset.rowCount) || target.missingCount !== 0) return;
  const groups = categoricalLabels(target, MAX_GROUPS);
  // "all" collides with the aggregate suffix; a leading underscore collides
  // with the native representation of pairs of features.
  if (!groups || groups.some((label) => label.toLowerCase() === 'all' || label.startsWith('_'))) return;
  const groupKind = target.type === 'numeric' ? 'numeric' : target.type === 'boolean' ? 'boolean' : 'text';
  const identities = groups.map((label) => groupIdentity(label, groupKind));
  if (identities.some((identity) => identity === undefined) || new Set(identities).size !== identities.length) return;
  const profile = dataset.profiles[column];
  if (!completeProfile(profile, dataset.rowCount) || profile.populatedCount < 2) return;
  if (profile.type === 'numeric') {
    if (!finite(profile.min) || !finite(profile.max) || profile.min >= profile.max) return;
    return { nativeTitle: 'Box plot', groups, groupKind };
  }
  if (!['text', 'boolean'].includes(profile.type) || !categoricalLabels(profile, MAX_CATEGORIES)) return;
  return { nativeTitle: 'Bar plot', groups, groupKind };
}

/** Eligibility comes from the full CSV; the limited request computes its own statistics. */
export function nativeComparisonColumns(
  dataset: ProjectDataset,
  columns: string[],
  targetColumn: string,
  predictionKind: PredictionKind,
): string[] {
  return [...new Set(columns)].filter((column) => comparisonProfile(dataset, column, targetColumn, predictionKind) !== undefined);
}

export function nativeComparisonPlan(
  dataset: ProjectDataset,
  column: string,
  targetColumn: string,
  predictionKind: PredictionKind,
): NativeDataPlotPlan | null {
  const eligible = comparisonProfile(dataset, column, targetColumn, predictionKind);
  if (!eligible) return null;
  if (eligible.nativeTitle === 'Box plot') {
    return {
      key: JSON.stringify(['comparison', 'box', column, targetColumn]),
      columns: [column], statistics: boxRecipe(), predictionKind,
      title: `${column} selon ${targetColumn}`,
      description: 'Le graphique original DrIA compare les distributions de cette information entre les catégories du résultat.',
      note: 'La boîte représente les quartiles, le trait central la médiane et les extrémités le minimum et le maximum, sans points atypiques. « all » rassemble toutes les observations ; les autres groupes correspondent au résultat à prédire. Les valeurs manquantes sont exclues.',
      nativeTitle: 'Box plot', expectedGroups: eligible.groups, expectedGroupKind: eligible.groupKind,
    };
  }
  return {
    key: JSON.stringify(['comparison', 'bar', column, targetColumn]),
    columns: [column], predictionKind,
    statistics: recipe([{ type: 'use', component: 'ValueCountsStatistic' }, { type: 'use', component: 'CountStatistic' }]),
    title: `${column} selon ${targetColumn}`,
    description: 'Le graphique original DrIA montre les effectifs de chaque catégorie selon le résultat à prédire.',
    note: 'Les barres montrent des effectifs, pas des proportions : les groupes peuvent avoir des tailles différentes. « all » rassemble toutes les observations ; les autres groupes correspondent au résultat à prédire. Les valeurs manquantes sont exclues. La vue guidée permet aussi de comparer les proportions.',
    nativeTitle: 'Bar plot', expectedGroups: eligible.groups, expectedGroupKind: eligible.groupKind,
  };
}

/** The native heatmap splits suffixes at the final underscore without Dataset. */
export function nativeMissingPlan(
  dataset: ProjectDataset,
  columns: string[],
  targetColumn: string,
  predictionKind: PredictionKind,
): NativeDataPlotPlan | null {
  if (!dataset.isComplete || !natural(dataset.rowCount) || dataset.rowCount === 0 || predictionKind === 'survival') return null;
  const target = dataset.profiles[targetColumn];
  if (!target || target.missingCount > 0 || target.nonFiniteCount > 0) return null;
  if (predictionKind === 'category') {
    const labels = target.distinctValues.map(String);
    if (target.distinctValuesTruncated || labels.length < 2 || labels.length > MAX_GROUPS
      || new Set(labels).size !== labels.length || labels.some((label) => label.includes('_') || label.toLowerCase() === 'all')) return null;
  }
  const candidates = [...new Set(columns)].filter((column) => {
    const profile = dataset.profiles[column];
    return column !== targetColumn && profile && ['numeric', 'boolean'].includes(profile.type)
      && natural(profile.missingCount) && profile.missingCount > 0 && profile.populatedCount > 0
      && profile.populatedCount + profile.missingCount === dataset.rowCount && profile.nonFiniteCount === 0;
  }).sort((a, b) => dataset.profiles[b].missingCount - dataset.profiles[a].missingCount);
  const safeCandidates = candidates.filter((column) => predictionKind === 'quantity' ? !column.includes('_')
    : !candidates.some((other) => other !== column && (other.startsWith(`${column}_`) || column.startsWith(`${other}_`))));
  const selected = safeCandidates.slice(0, MAX_MISSING_COLUMNS);
  if (!selected.length) return null;
  return {
    key: JSON.stringify(['missing', predictionKind, targetColumn, selected]),
    columns: selected, predictionKind,
    statistics: recipe([{ type: 'use', component: 'MissingRateStatistic' }]),
    title: 'Où manque-t-il des données ?',
    description: selected.length === 1
      ? `La carte DrIA montre le taux de valeurs manquantes dans « ${selected[0]} », dans l’ensemble du fichier et par groupe lorsque le résultat est une catégorie.`
      : `La carte DrIA montre les taux de valeurs manquantes dans les ${selected.length} informations numériques ou oui / non les plus incomplètes de la sélection.`,
    note: `La couleur représente un taux de 0 à 1 (0 à 100 %). « all » rassemble toutes les observations${predictionKind === 'category' ? ' ; les autres lignes correspondent aux catégories du résultat à prédire' : ''}. Au plus ${MAX_MISSING_COLUMNS} colonnes sont affichées. Les autres colonnes et les informations entièrement vides restent consultables dans le bilan.`,
    nativeTitle: 'Missingness heatmap',
    ...(predictionKind === 'category' ? { expectedGroupCount: target.distinctValues.length } : {}),
    expectedMissingRates: Object.fromEntries(selected.map((column) => [column, dataset.profiles[column].missingCount / dataset.rowCount])),
  };
}

function invalidStatistics(): never {
  throw new Error('DrIA n’a pas renvoyé les statistiques nécessaires à ce graphique pour la sélection actuelle. La vue guidée reste disponible.');
}

function validateBox(analysis: ProjectColumnStatistics): boolean {
  const values = BOX_ROWS.map((row) => value(analysis, row));
  const present = value(analysis, 'count');
  return values.every(finite) && values.every((entry, index) => index === 0 || (entry as number) >= (values[index - 1] as number))
    && natural(present) && present > 0;
}

function validateStatistics(result: IStatistics, plan: NativeDataPlotPlan): void {
  const table = result.statistics;
  if (!table || !Array.isArray(table.columns) || !table.columns.length || !Array.isArray(table.index)
    || !Array.isArray(table.data) || !table.data.length || !plan.columns.length) invalidStatistics();
  const mapped = getProjectColumnStatistics(result, plan.columns, plan.predictionKind);
  const allAnalyses = plan.columns.flatMap((column) => mapped[column] ?? []);
  // Limited requests must not contain another feature whose generic plot title
  // could be mistaken for the selected feature.
  if (allAnalyses.length !== table.columns.length) invalidStatistics();
  for (const column of plan.columns) {
    const analyses = mapped[column] ?? [];
    const globals = analyses.filter((analysis) => analysis.aggregate);
    const groups = analyses.filter((analysis) => !analysis.aggregate);
    if (globals.length !== 1 || !analyses.length) invalidStatistics();
    if (plan.expectedGroups) {
      const labels = groups.map((analysis) => scalarLabel(analysis.subset));
      const expected = plan.expectedGroups.map((label) => groupIdentity(label, plan.expectedGroupKind));
      const identities = labels.map((label) => label === undefined ? undefined : groupIdentity(label, plan.expectedGroupKind));
      if (identities.length !== expected.length || new Set(identities).size !== identities.length
        || expected.some((identity) => identity === undefined) || new Set(expected).size !== expected.length
        || identities.some((identity) => identity === undefined || !expected.includes(identity))) invalidStatistics();
    }
    if (plan.nativeTitle === 'Box plot') {
      if (!analyses.every(validateBox)) invalidStatistics();
    } else if (plan.nativeTitle === 'Bar plot') {
      if (!analyses.every((analysis) => categoryCounts(analysis) !== undefined) || !smallCategoryCatalogue(analyses)) invalidStatistics();
    } else {
      if (plan.predictionKind === 'category' && groups.length !== plan.expectedGroupCount) invalidStatistics();
      if (plan.predictionKind === 'quantity' && groups.length) invalidStatistics();
      if (!analyses.every((analysis) => {
        const rate = value(analysis, 'missing_rate');
        return finite(rate) && rate >= 0 && rate <= 1;
      })) invalidStatistics();
      const expected = plan.expectedMissingRates?.[column];
      const returned = value(globals[0], 'missing_rate');
      if (!finite(expected) || !finite(returned) || Math.abs(returned - expected) > 1e-9) invalidStatistics();
    }
  }
}

/** A generic native title is only safe when exactly one selected plot exists. */
export function selectNativeDataPlot(result: IStatistics, plan: NativeDataPlotPlan): IPlot {
  validateStatistics(result, plan);
  const matches = (Array.isArray(result.plots) ? result.plots : []).filter((plot) => plot?.title === plan.nativeTitle);
  if (matches.length !== 1) throw new Error(matches.length
    ? 'DrIA a renvoyé plusieurs graphiques pour cette sélection. Leur colonne ne peut pas être identifiée avec certitude.'
    : 'DrIA n’a pas renvoyé le graphique demandé. La vue guidée reste disponible.');
  const plot = matches[0];
  const image = typeof plot.image === 'string' ? plot.image.trim() : '';
  const base64 = image.replace(/^data:image\/png;base64,/i, '').replace(/\s/g, '');
  // Validate PNG identity without trying to identify content or placeholders
  // from the compressed image length. Statistics above establish usefulness.
  if (!base64.startsWith('iVBORw0KGgo') || !/^[A-Za-z0-9+/]+={0,2}$/.test(base64)) {
    throw new Error('L’image du graphique renvoyée par DrIA n’est pas un PNG valide.');
  }
  return { ...plot, image: base64 };
}
