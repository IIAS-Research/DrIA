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
const number = new Intl.NumberFormat('fr-FR', { maximumFractionDigits: 3 });
const numericNumber = new Intl.NumberFormat('fr-FR', { maximumSignificantDigits: 6 });
const scientificNumber = new Intl.NumberFormat('fr-FR', { notation: 'scientific', maximumSignificantDigits: 6 });
const percent = new Intl.NumberFormat('fr-FR', { maximumFractionDigits: 1 });
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
  const query = (search.value ?? '').trim().toLocaleLowerCase('fr');
  return props.dataset.columns.filter((name) => `${name} ${roleLabel(name)} ${typeLabel(props.dataset.profiles[name])}`.toLocaleLowerCase('fr').includes(query));
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
  const query = (measureSearch.value ?? '').trim().toLocaleLowerCase('fr');
  return statisticColumns.value.filter((measure) => measure.label.toLocaleLowerCase('fr').includes(query));
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
    title: props.context === 'project' ? 'Valeurs manquantes dans le résultat à prédire' : 'Compléter le résultat à prédire',
    detail: `Valeurs manquantes dans ${names(missingOutcome)}. Ces colonnes doivent être renseignées pour chaque observation.`,
  });
  if (target.value && !target.value.distinctValuesTruncated && target.value.distinctValues.length === 1) result.push({
    key: 'outcome-constant', blocking: true,
    title: props.context === 'project' ? 'Le résultat à prédire ne varie pas' : 'Le résultat à prédire doit varier',
    detail: `« ${targetName.value} » ne contient qu’une seule valeur distincte. Le résultat à prédire doit contenir au moins deux valeurs différentes.`,
  });
  const missingGroups = groups.value.filter((name) => props.dataset.profiles[name]?.missingCount > 0);
  if (missingGroups.length) result.push({
    key: 'group-missing', blocking: true,
    title: props.context === 'project' ? 'Informations de regroupement incomplètes' : 'Compléter les informations de regroupement',
    detail: `Valeurs manquantes dans ${names(missingGroups)}. Le regroupement doit être connu pour garder les observations liées ensemble.`,
  });
  const nonFinite = retained.value.filter((name) => props.dataset.profiles[name]?.nonFiniteCount > 0);
  if (nonFinite.length) result.push({
    key: 'non-finite', blocking: true,
    title: props.context === 'project' ? 'Des valeurs ne sont pas finies' : 'Corriger les valeurs non finies',
    detail: props.context === 'project'
      ? `Valeurs non finies dans ${names(nonFinite)}, comme NaN ou Infinity. Ces valeurs ne sont pas utilisables telles quelles par les modèles.`
      : `Valeurs non finies dans ${names(nonFinite)}, comme NaN ou Infinity. Corrigez-les dans le fichier, ou excluez les colonnes concernées si elles ne servent pas à définir le résultat ou les groupes.`,
  });
  // Empty durations and non-finite values are already reported above.
  if (time.value && time.value.populatedCount > 0 && !time.value.nonFiniteCount
    && (time.value.type !== 'numeric' || (time.value.min ?? 0) < 0)) result.push({
    key: 'followup-invalid', blocking: true,
    title: props.context === 'project' ? 'Durées de suivi non valides' : 'Corriger la durée de suivi',
    detail: `« ${timeName.value} » ${time.value.type !== 'numeric' ? 'contient des valeurs non numériques' : 'contient des durées négatives'}. La durée de suivi doit être un nombre positif ou nul.`,
  });
  const empty = predictors.value.filter((name) => props.dataset.profiles[name]?.populatedCount === 0);
  if (empty.length) result.push({
    key: 'empty', blocking: false, title: 'Des informations sont entièrement vides',
    detail: props.context === 'project'
      ? `${names(empty)} : aucune valeur renseignée. Ces colonnes n’apportent aucune information à l’apprentissage.`
      : `${names(empty)} : aucune valeur renseignée. Vous pouvez exclure ces colonnes à l’étape « Informations ».`,
  });
  const constant = predictors.value.filter((name) => {
    const profile = props.dataset.profiles[name];
    return profile && profile.populatedCount > 0 && !profile.distinctValuesTruncated && profile.distinctValues.length === 1;
  });
  if (constant.length) result.push({
    key: 'constant', blocking: false, title: 'Des informations ne varient pas',
    detail: `${names(constant)} : une seule valeur distincte parmi les cellules renseignées. Ces valeurs n’apportent pas de différence entre les observations.`,
  });
  if (predictorMissing.value > 0 && predictorCells.value > 0) result.push({
    key: 'predictor-missing', blocking: false,
    title: props.context === 'project' ? 'Valeurs manquantes dans les informations pour prédire' : 'Prévoir le traitement des valeurs manquantes',
    detail: `${number.format(predictorMissing.value)} cellules manquantes sur ${number.format(predictorCells.value)} dans les informations pour prédire (${percentage(predictorMissing.value, predictorCells.value)} %). Leur traitement fait partie de la préparation des données.`,
  });
  return result;
});

function role(name: string): string { return props.roles[name]?.role ?? 'predictor'; }
function roleLabel(name: string): string { return props.roles[name]?.label ?? 'Pour prédire'; }
function roleColor(name: string): string {
  if (['identifier', 'unavailable'].includes(role(name))) return 'grey-7';
  return ['group', 'predictor'].includes(role(name)) ? 'secondary' : 'accent';
}
function typeLabel(profile?: ColumnProfile): string {
  return profile ? { numeric: 'Nombre', boolean: 'Oui / non', text: 'Texte ou catégorie', empty: 'Vide' }[profile.type] : 'En cours';
}
function percentage(count: number, total: number): string { return percent.format(total > 0 ? 100 * count / total : 0); }
function ratio(count: number, total: number): number { return total > 0 ? count / total : 0; }
function names(columns: string[]): string {
  return `${columns.slice(0, 4).map((name) => `« ${name} »`).join(', ')}${columns.length > 4 ? ` et ${columns.length - 4} autre${columns.length > 5 ? 's' : ''} colonne${columns.length > 5 ? 's' : ''}` : ''}`;
}
function scalarLabel(value: ProjectScalar): string { return typeof value === 'boolean' ? (value ? 'Vrai' : 'Faux') : String(value); }
function numericLabel(value: number): string {
  const magnitude = Math.abs(value);
  return (magnitude > 0 && (magnitude < 0.001 || magnitude >= 1000000) ? scientificNumber : numericNumber).format(value);
}
function numericRange(profile?: ColumnProfile): string {
  if (profile?.type !== 'numeric' || !Number.isFinite(profile.min) || !Number.isFinite(profile.max)) return '';
  return `${numericLabel(profile.min as number)} à ${numericLabel(profile.max as number)}`;
}
function distinctLabel(profile?: ColumnProfile): string {
  if (!profile) return '—';
  return `${profile.distinctValuesTruncated ? 'Au moins ' : ''}${number.format(profile.distinctValues.length)}`;
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
  if (key === 'median') return 'Médiane';
  if (key === 'nunique') return 'Valeurs distinctes (DrIA)';
  if (key === 'count') return 'Valeurs renseignées (DrIA)';
  return key === 'most_frequent_ratio' ? 'Part la plus fréquente (%)' : statistic.label;
}
</script>

<template>
  <div class="data-summary">
    <div class="summary-heading">
      <h2 class="text-subtitle1 text-weight-medium q-my-none">Comprendre vos données</h2>
      <p class="text-caption text-grey-7 q-mt-xs q-mb-none">
        <template v-if="dataset.isComplete">
          Bilan calculé sur toutes les observations du fichier{{ filename ? ` « ${filename} »` : '' }}.
          <template v-if="context === 'project'">Les rôles attribués aux colonnes sont ceux du projet.</template>
          <template v-else>Il s’adapte au rôle des colonnes.</template>
        </template>
        <template v-else>Le bilan sera disponible lorsque la lecture complète du fichier sera terminée.</template>
      </p>
    </div>

    <div v-if="!dataset.isComplete" class="summary-loading" role="status" aria-live="polite">
      <q-spinner color="primary" size="32px" aria-hidden="true" />
      <span>Analyse de l’ensemble du fichier en cours…</span>
    </div>

    <template v-else>
      <div class="summary-metrics" aria-label="Vue d’ensemble du fichier">
        <div class="summary-metric"><strong>{{ number.format(dataset.rowCount) }}</strong><span>observations</span></div>
        <div class="summary-metric"><strong>{{ number.format(dataset.columns.length) }}</strong><span>colonnes importées</span></div>
        <div class="summary-metric"><strong>{{ number.format(predictors.length) }}</strong><span>informations pour prédire</span></div>
        <div class="summary-metric"><strong>{{ percentage(populatedCells, totalCells) }}<small> %</small></strong><span>cellules renseignées</span></div>
      </div>
      <p class="text-caption text-grey-7 q-mt-sm q-mb-md">
        {{ number.format(populatedCells) }} cellules renseignées sur {{ number.format(totalCells) }} dans le fichier entier, y compris les colonnes exclues.
        <template v-if="exclusions.length">{{ exclusions.length }} colonne{{ exclusions.length > 1 ? 's sont exclues' : ' est exclue' }} de l’apprentissage.</template>
        <template v-if="groups.length">{{ groups.length }} colonne{{ groups.length > 1 ? 's servent' : ' sert' }} au regroupement, sans servir à prédire.</template>
      </p>

      <section class="summary-section" aria-labelledby="summary-checks-title">
        <div class="section-title">
          <q-icon name="fact_check" color="primary" size="21px" aria-hidden="true" />
          <h3 id="summary-checks-title" class="text-subtitle2 q-my-none">{{ context === 'project' ? 'Points d’attention' : 'À vérifier' }}</h3>
        </div>
        <ul v-if="checks.length" class="checks-list">
          <li v-for="check in checks" :key="check.key" class="data-check" :class="{ 'blocking-check': check.blocking && context === 'creation' }">
            <q-icon :name="check.blocking && context === 'creation' ? 'error_outline' : 'info_outline'" :color="check.blocking && context === 'creation' ? 'negative' : 'primary'" size="20px" aria-hidden="true" />
            <div>
              <div class="check-title">{{ check.title }} <span v-if="check.blocking && context === 'creation'" class="blocking-label">Avant de lancer</span></div>
              <p class="text-caption text-grey-8 q-my-none">{{ check.detail }}</p>
            </div>
          </li>
        </ul>
        <p v-else class="text-caption text-grey-7 q-mt-sm q-mb-none">
          Aucune valeur manquante ou non finie détectée dans les colonnes retenues, ni information pour prédire vide ou constante.
        </p>
      </section>

      <section class="summary-section" aria-labelledby="summary-outcome-title">
        <div class="section-title">
          <q-icon name="flag" color="primary" size="21px" aria-hidden="true" />
          <h3 id="summary-outcome-title" class="text-subtitle2 q-my-none">Le résultat à prédire</h3>
        </div>
        <p v-if="!target" class="text-body2 text-grey-7 q-mt-sm q-mb-none">{{ context === 'project' ? 'Le résultat à prédire n’est pas renseigné dans la configuration de ce projet.' : 'Choisissez le résultat à l’étape « Prédire » pour voir sa répartition ici.' }}</p>
        <template v-else>
          <div class="outcome-column q-mt-sm">{{ targetName }}</div>
          <div class="text-caption text-grey-7 q-mb-sm">{{ number.format(target.populatedCount) }} observations renseignées · {{ number.format(target.missingCount) }} valeurs manquantes</div>

          <template v-if="predictionKind === 'survival'">
            <div v-if="eventCount !== undefined" class="survival-counts">
              <div><strong>{{ number.format(eventCount) }}</strong><span>avec l’événement observé</span><small>{{ percentage(eventCount, target.populatedCount) }} % des observations renseignées</small></div>
              <div><strong>{{ number.format(target.populatedCount - eventCount) }}</strong><span>sans événement observé</span><small>L’événement n’a pas été observé pendant le suivi.</small></div>
            </div>
            <p v-else class="text-caption text-grey-7 q-my-sm">{{ context === 'project' ? 'Les effectifs par événement ne sont pas disponibles pour la valeur configurée dans ce projet.' : 'Choisissez la valeur indiquant l’événement observé à l’étape « Prédire » pour afficher les effectifs.' }}</p>
            <div class="followup-details q-mt-sm">
              <template v-if="time">
                <div class="text-body2 text-weight-medium">Durée de suivi · {{ timeName }}</div>
                <div v-if="numericRange(time)" class="text-body2">De {{ numericRange(time) }} {{ timeUnit }}</div>
                <div v-else class="text-caption text-grey-7">La durée doit être une valeur numérique finie.</div>
                <div class="text-caption text-grey-7">{{ number.format(time.missingCount) }} durée{{ time.missingCount > 1 ? 's' : '' }} manquante{{ time.missingCount > 1 ? 's' : '' }}</div>
              </template>
              <span v-else class="text-caption text-grey-7">{{ context === 'project' ? 'La colonne de durée de suivi n’est pas disponible dans ce fichier.' : 'La durée de suivi apparaîtra ici une fois sa colonne choisie.' }}</span>
            </div>
          </template>

          <template v-else-if="predictionKind === 'quantity'">
            <div v-if="numericRange(target)" class="quantity-range"><span>Valeurs observées</span><strong>{{ numericRange(target) }}</strong><small>Minimum et maximum sur toutes les valeurs renseignées.</small></div>
            <p v-else class="text-caption text-grey-7 q-my-none">Le résultat doit être une quantité numérique finie.</p>
          </template>

          <template v-else-if="categoryCounts.length">
            <p class="text-caption text-grey-7 q-mt-none q-mb-sm">Répartition parmi les {{ number.format(target.populatedCount) }} observations dont le résultat est renseigné.</p>
            <div v-for="(entry, index) in categoryCounts" :key="JSON.stringify(entry.value)" class="category-row">
              <div class="category-label"><span>{{ scalarLabel(entry.value) }}</span><span>{{ number.format(entry.count) }} · {{ percentage(entry.count, target.populatedCount) }} %</span></div>
              <q-linear-progress rounded size="8px" :value="ratio(entry.count, target.populatedCount)" :color="index % 2 ? 'accent' : 'primary'" track-color="grey-3" aria-hidden="true" />
            </div>
          </template>
          <p v-else class="text-caption text-grey-7 q-my-none">{{ distinctLabel(target) }} valeurs distinctes. La répartition n’est affichée ici que lorsque toutes les catégories sont connues et qu’il y en a au plus 12.</p>
        </template>
      </section>

      <section class="summary-section" aria-labelledby="summary-columns-title">
        <div class="section-title q-mb-sm">
          <q-icon name="view_column" color="primary" size="21px" aria-hidden="true" />
          <h3 id="summary-columns-title" class="text-subtitle2 q-my-none">Le bilan par colonne</h3>
        </div>
        <div v-if="statisticsLoading" class="statistics-status" role="status" aria-live="polite">
          <q-spinner color="primary" size="20px" aria-hidden="true" />
          <span>Calcul automatique des statistiques DrIA… Le bilan du fichier reste disponible.</span>
        </div>
        <div v-else-if="statisticsError" class="statistics-status statistics-status-error" role="status">
          <q-icon name="error_outline" color="negative" size="20px" aria-hidden="true" />
          <div class="statistics-status-copy"><strong>Les statistiques DrIA n’ont pas pu être calculées.</strong><span>{{ statisticsError }}</span></div>
          <q-btn flat dense no-caps color="primary" label="Réessayer" @click="emit('retry-statistics')" />
        </div>
        <div v-else-if="statisticsUnavailableReason" class="statistics-status" role="status">
          <q-icon name="info_outline" color="primary" size="20px" aria-hidden="true" />
          <span>{{ statisticsUnavailableReason }}</span>
        </div>
        <p v-else-if="hasColumnStatistics" class="text-caption text-grey-7 q-mt-none q-mb-sm">
          Les statistiques DrIA ajoutent des colonnes au bilan. Les mesures portent sur toutes les observations par défaut ; choisissez un sous-ensemble sous le nom d’une variable pour consulter ses résultats séparément.
        </p>
        <p v-else-if="statistics" class="text-caption text-grey-7 q-mt-none q-mb-sm">DrIA n’a pas renvoyé de statistiques rattachées aux colonnes de ce fichier. Le bilan du fichier reste disponible.</p>
        <div class="summary-table-tools q-mb-sm">
          <q-input v-model="search" outlined dense clearable label="Rechercher dans le bilan des colonnes" class="summary-search">
            <template #prepend><q-icon name="search" aria-hidden="true" /></template>
          </q-input>
          <q-btn-dropdown
            v-if="statisticColumns.length"
            outline dense no-caps color="primary" icon="view_column"
            :label="`Mesures DrIA : ${displayedStatisticColumns.length} / ${statisticColumns.length}`"
            :content-class="context === 'project' ? 'project-workspace-colors' : 'project-creation-colors'"
            @show="measureSearch = ''"
          >
            <div class="measure-menu q-pa-md">
              <div class="text-subtitle2">Mesures affichées</div>
              <p class="text-caption text-grey-7 q-mt-xs q-mb-sm">Chaque mesure ajoute une colonne au tableau.</p>
              <div class="row q-gutter-x-xs q-mb-sm">
                <q-btn flat dense no-caps color="primary" label="Principales" @click="chosenMeasures = null" />
                <q-btn flat dense no-caps color="primary" label="Tout afficher" @click="chosenMeasures = statisticColumns.map(measure => measure.key)" />
              </div>
              <q-input v-model="measureSearch" outlined dense clearable label="Rechercher une mesure" class="q-mb-sm" />
              <div class="measure-list" role="group" aria-label="Statistiques DrIA à afficher">
                <q-checkbox
                  v-for="measure in filteredStatisticColumns"
                  :key="measure.key"
                  :model-value="displayedStatisticColumns.some(column => column.key === measure.key)"
                  dense color="primary" :label="measure.label" class="measure-option"
                  @update:model-value="setMeasureVisibility(measure.key, $event)"
                />
                <p v-if="!filteredStatisticColumns.length" class="text-caption text-grey-7 q-my-sm">Aucune mesure ne correspond à votre recherche.</p>
              </div>
            </div>
          </q-btn-dropdown>
        </div>
        <div v-if="filteredColumns.length" class="summary-table-scroll" tabindex="0" role="region" aria-label="Bilan de chaque colonne, défilement possible">
          <table class="summary-table" :style="{ '--summary-statistic-count': displayedStatisticColumns.length }">
            <caption class="sr-only">Rôle, type, complétude, valeurs distinctes et statistiques DrIA de toutes les colonnes du fichier</caption>
            <colgroup>
              <col class="variable-width">
              <col class="completeness-width">
              <col class="distinct-width">
              <col v-for="measure in displayedStatisticColumns" :key="measure.key" class="statistic-width">
            </colgroup>
            <thead>
              <tr>
                <th scope="col">Colonne et rôle</th>
                <th scope="col">Valeurs renseignées</th>
                <th scope="col">Valeurs distinctes</th>
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
                    :label="`Observations pour ${name}`"
                    class="statistics-subset q-mt-sm"
                    :popup-content-class="context === 'project' ? 'project-workspace-colors' : 'project-creation-colors'"
                    @update:model-value="selectAnalysis(name, $event)"
                  />
                  <p v-if="analyses(name).length && !selectedAnalysis(name)" class="text-caption text-grey-7 q-mt-xs q-mb-none">Choisissez un sous-ensemble pour consulter ses statistiques.</p>
                  <p v-else-if="statistics && !statisticsLoading && !analyses(name).length" class="text-caption text-grey-7 q-mt-xs q-mb-none">{{ exclusions.includes(name) ? 'Exclue de l’analyse' : 'Non analysée par DrIA' }}</p>
                </th>
                <td>
                  <strong>{{ percentage(dataset.profiles[name]?.populatedCount ?? 0, dataset.rowCount) }} %</strong>
                  <div class="text-caption text-grey-7">{{ number.format(dataset.profiles[name]?.populatedCount ?? 0) }} / {{ number.format(dataset.rowCount) }} observations</div>
                  <div v-if="dataset.profiles[name]?.nonFiniteCount" class="text-caption text-negative">{{ number.format(dataset.profiles[name].nonFiniteCount) }} non finie{{ dataset.profiles[name].nonFiniteCount > 1 ? 's' : '' }}</div>
                </td>
                <td>
                  <strong>{{ distinctLabel(dataset.profiles[name]) }}</strong>
                  <div v-if="numericRange(dataset.profiles[name])" class="text-caption text-grey-7">De {{ numericRange(dataset.profiles[name]) }}</div>
                </td>
                <td v-for="measure in displayedStatisticColumns" :key="measure.key" class="statistic-cell" :class="{ 'missing-statistic': !selectedStatistics[name]?.has(measure.key), 'numeric-statistic': typeof selectedStatistics[name]?.get(measure.key)?.value === 'number' }" :data-statistic="measure.key">
                  <div class="statistic-value" :title="statisticCell(name, measure.key)">{{ statisticCell(name, measure.key) }}</div>
                </td>
              </tr>
            </tbody>
          </table>
        </div>
        <p v-else class="text-caption text-grey-7 q-my-sm">Aucune colonne ne correspond à votre recherche.</p>
        <p v-if="displayedStatisticColumns.length" class="text-caption text-grey-7 q-mt-sm q-mb-none">Défilez horizontalement pour parcourir les mesures. Le nom des variables reste visible à gauche.</p>
        <p class="text-caption text-grey-7 q-mt-sm q-mb-none">Les valeurs distinctes sont comptées hors cellules vides. Au-delà du catalogue de 100 valeurs, un minimum est indiqué.</p>
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
