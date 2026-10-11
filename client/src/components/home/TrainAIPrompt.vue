<script setup lang="ts">
import 'src/css/project-creation.scss';
import { QForm } from 'quasar';
import { computed, nextTick, onMounted, onUnmounted, ref, watch } from 'vue';
import { getDefaultPipeline, exploreStatistics, validateStudy } from 'src/api/ia-web-service.api';
import { apiError } from 'src/utils/iaml';
import { useUserStore } from 'src/stores/user-store';
import { readProjectCsv, ProjectDataset, ProjectColumnAnnotation } from 'src/utils/project-data';
import { buildGuidedStudy, inferPredictionKind, PredictionKind, StudyPriority, TargetLabel } from 'src/utils/project-study';
import type { NativeDataPlotPlan } from 'src/utils/project-native-plots';
import { IPipeline, IProjectForm, IStatistics } from '../models';
import ProjectDataPreview from './ProjectDataPreview.vue';
import ProjectDataSummary from './ProjectDataSummary.vue';
import ProjectDataPlots from './ProjectDataPlots.vue';
import ProjectFileUpload from './ProjectFileUpload.vue';
import PipelineProject from '../pipeline/PipelineProject.vue';

const props = defineProps<{ submitting?: boolean }>();
const emit = defineEmits<{
  (event: 'submitProject', projectForm: IProjectForm, file: File): void;
  (event: 'datasetReady', ready: boolean): void;
}>();
const userStore = useUserStore();
const project = ref<IProjectForm>({
  title: '', description: '', csvColumnToPredict: '', rowDepedencies: false,
  rowDepedenciesLinked: [], datasetHasIdColumn: false, datasetHasIdColumnList: [], survivalModel: false,
});
const uploadedFile = ref<File | null>(null);
const testFile = ref<File | null>(null);
const dataset = ref<ProjectDataset>();
const testDataset = ref<ProjectDataset>();
const readingFile = ref(false);
const readingTestFile = ref(false);
const testError = ref('');
const previewSource = ref<'train' | 'test'>('train');
const dataView = ref<'preview' | 'summary' | 'plots'>('preview');
const step = ref(1);
const furthestStep = ref(1);
const questionForm = ref<QForm>();
const questionCard = ref<HTMLElement>();
const error = ref('');
const feedback = ref('');
const validating = ref(false);
const loadingStudy = ref(false);
const studyError = ref('');
const pipeline = ref<IPipeline>();
const editor = ref<InstanceType<typeof PipelineProject>>();
const showAdvanced = ref(false);
const advancedOpened = ref(false);
const predictionKind = ref<PredictionKind>('category');
const eventChoice = ref('');
const timeUnit = ref<string | null>(null);
const repeatedPatients = ref<boolean | null>(null);
const exclusionMode = ref<'identifier' | 'unavailable'>('identifier');
const exclusionReasons = ref<Record<string, 'identifier' | 'unavailable'>>({});
const evaluationMode = ref<'split' | 'external' | 'cross_validation'>('split');
const priority = ref<StudyPriority>('recommended');
const positiveChoice = ref('');
const searchDuration = ref(300);
const statistics = ref<IStatistics>();
const statsLoading = ref(false);
const statsError = ref('');
let fileRevision = 0;
let testRevision = 0;
let statsRevision = 0;
let statisticsTimer: ReturnType<typeof setTimeout> | undefined;
let statisticsController: AbortController | undefined;
let fileController: AbortController | undefined;
let testController: AbortController | undefined;
let previousAutoTitle = '';

const steps = [
  { title: 'Importer', icon: 'upload_file' },
  { title: 'Prédire', icon: 'flag' },
  { title: 'Regrouper', icon: 'group' },
  { title: 'Informations', icon: 'view_column' },
  { title: 'Évaluer', icon: 'fact_check' },
  { title: 'Rechercher', icon: 'tune' },
  { title: 'Lancer', icon: 'play_arrow' },
];
const number = new Intl.NumberFormat('fr-FR');
const busy = computed(() => Boolean(props.submitting || validating.value));
const columns = computed(() => dataset.value?.columns ?? []);
const targetProfile = computed(() => dataset.value?.profiles[project.value.csvColumnToPredict]);
const inferredKind = computed(() => targetProfile.value ? inferPredictionKind(targetProfile.value) : undefined);
const featureColumns = computed(() => columns.value.filter((column) => column !== project.value.csvColumnToPredict
  && (predictionKind.value !== 'survival' || column !== project.value.survivalModeldatasetTargetTime)));
const selectableFeatures = computed(() => featureColumns.value.filter((column) => !project.value.rowDepedenciesLinked.includes(column)));
const predictorColumns = computed(() => selectableFeatures.value.filter((column) => !exclusionReasons.value[column]));
const selectedExclusions = computed({
  get: () => selectableFeatures.value.filter((column) => exclusionReasons.value[column] === exclusionMode.value),
  set: (selected: string[]) => {
    const reasons = { ...exclusionReasons.value };
    Object.keys(reasons).filter((column) => reasons[column] === exclusionMode.value).forEach((column) => { delete reasons[column]; });
    selected.filter((column) => selectableFeatures.value.includes(column)).forEach((column) => { reasons[column] = exclusionMode.value; });
    exclusionReasons.value = reasons;
  },
});
const eventOptions = computed(() => (targetProfile.value?.distinctValues ?? []).map((value) => ({ label: scalarLabel(value), value: JSON.stringify(value) })));
const binaryTarget = computed(() => predictionKind.value === 'category' && targetProfile.value?.distinctValues.length === 2
  && !targetProfile.value.distinctValuesTruncated);
// Eligibility depends on the event's two values, including numeric event codes
// that IAML may infer as quantities for decimal codes or very small datasets.
const survivalEligible = computed(() => Boolean(dataset.value?.isComplete
  && targetProfile.value?.distinctValues.length === 2 && !targetProfile.value.distinctValuesTruncated
  && !targetProfile.value.nonFiniteCount));
const survivalAnalysis = computed({
  get: () => predictionKind.value === 'survival',
  set: (enabled: boolean) => { predictionKind.value = enabled && survivalEligible.value ? 'survival' : inferredKind.value ?? 'category'; },
});
const roles = computed<Record<string, ProjectColumnAnnotation>>(() => Object.fromEntries(columns.value.map((column) => {
  let annotation: ProjectColumnAnnotation;
  if (column === project.value.csvColumnToPredict) {
    annotation = predictionKind.value === 'survival'
      ? { role: 'event', label: 'Événement', reason: eventChoice.value ? `L’événement observé correspond à ${eventOptions.value.find((option) => option.value === eventChoice.value)?.label}.` : 'Cette colonne indique si l’événement a été observé.' }
      : { role: 'target', label: 'À prédire', reason: 'Le modèle apprendra à prédire cette colonne.' };
  } else if (predictionKind.value === 'survival' && column === project.value.survivalModeldatasetTargetTime) {
    annotation = { role: 'time', label: 'Durée de suivi', reason: timeUnit.value ? `Durée exprimée en ${timeUnit.value}.` : 'Cette colonne contient la durée de suivi, dans l’unité utilisée par le fichier.' };
  } else if (project.value.rowDepedenciesLinked.includes(column)) {
    annotation = { role: 'group', label: 'Regroupement patient', reason: 'Ces observations restent ensemble lors des découpages. Cette colonne ne sert pas à prédire.' };
  } else if (exclusionReasons.value[column] === 'identifier') {
    annotation = { role: 'identifier', label: 'Identifiant — exclu', reason: 'Cette colonne est un identifiant et sera exclue de l’apprentissage.' };
  } else if (exclusionReasons.value[column] === 'unavailable') {
    annotation = { role: 'unavailable', label: 'Indisponible — exclu', reason: 'Cette information ne sera pas connue au moment de la prédiction.' };
  } else {
    annotation = { role: 'predictor', label: 'Pour prédire', reason: 'Cette information pourra être utilisée pour la prédiction.' };
  }
  return [column, annotation];
})));
const tableSelectable = computed(() => !busy.value && dataset.value?.isComplete && previewSource.value === 'train' && [2, 3, 4].includes(step.value));
const activeColumns = computed(() => step.value === 2 ? [project.value.csvColumnToPredict].filter(Boolean)
  : step.value === 3 ? project.value.rowDepedenciesLinked : step.value === 4 ? selectedExclusions.value : []);
const selectionHint = computed(() => step.value === 2 ? 'Cliquez sur le nom du résultat que vous souhaitez prédire.'
  : step.value === 3 && repeatedPatients.value ? 'Cliquez sur la ou les colonnes qui permettent de reconnaître le même patient.'
    : step.value === 4 ? `Cliquez sur les colonnes ${exclusionMode.value === 'identifier' ? 'qui sont des identifiants' : 'indisponibles au moment de la prédiction'} pour les exclure.` : '');
const visibleDataset = computed(() => previewSource.value === 'test' && testDataset.value ? testDataset.value : dataset.value);
const visibleFile = computed(() => previewSource.value === 'test' ? testFile.value : uploadedFile.value);
const statisticsRecipe = computed(() => advancedOpened.value ? (editor.value?.pipelineRefLocalComponent ?? pipeline.value)?.statistics : undefined);
const excludedStatisticsColumns = computed(() => selectableFeatures.value.filter((column) => Boolean(exclusionReasons.value[column])));
const statisticsContext = computed(() => JSON.stringify([
  previewSource.value, project.value.csvColumnToPredict, predictionKind.value, eventChoice.value,
  project.value.survivalModeldatasetTargetTime, project.value.rowDepedenciesLinked,
  excludedStatisticsColumns.value, statisticsRecipe.value,
]));
const statisticsBlockedReason = computed(() => {
  const source = visibleDataset.value;
  if (!source?.isComplete || !visibleFile.value) return 'Les analyses seront disponibles une fois le fichier entièrement lu.';
  const target = source.profiles[project.value.csvColumnToPredict];
  if (!target) return 'Choisissez le résultat à prédire à l’étape 2 pour accéder aux statistiques et graphiques.';
  if (target.missingCount) return 'Complétez les valeurs manquantes du résultat à prédire avant de calculer ces analyses.';
  if (target.nonFiniteCount) return 'Corrigez les valeurs non finies du résultat à prédire avant de calculer ces analyses.';
  if (predictionKind.value === 'survival') {
    if (target.distinctValuesTruncated || target.distinctValues.length > 2) return 'La colonne d’événement doit distinguer l’événement observé et l’événement non observé.';
    if (!eventOptions.value.some((option) => option.value === eventChoice.value)) return 'Précisez la valeur correspondant à l’événement observé à l’étape 2.';
    const eventValue = JSON.parse(eventChoice.value) as TargetLabel;
    if (!target.distinctValues.includes(eventValue)) return 'La valeur choisie pour l’événement observé est absente du fichier affiché. Les analyses de survie nécessitent au moins un événement observé dans ce fichier.';
    const timeColumn = project.value.survivalModeldatasetTargetTime;
    const time = timeColumn ? source.profiles[timeColumn] : undefined;
    if (!time || timeColumn === project.value.csvColumnToPredict) return 'Choisissez la colonne de durée de suivi à l’étape 2.';
    if (time.type !== 'numeric' || time.missingCount || time.nonFiniteCount || (time.min ?? -1) < 0) return 'La durée de suivi doit être renseignée, finie et positive ou nulle pour chaque observation.';
  }
  if (!predictorColumns.value.length) return 'Conservez au moins une information pour prédire afin de calculer ces analyses.';
  if (project.value.rowDepedenciesLinked.some((column) => source.profiles[column]?.missingCount)) return 'Complétez les valeurs manquantes dans les colonnes de regroupement avant de calculer ces analyses.';
  if (project.value.rowDepedenciesLinked.some((column) => source.profiles[column]?.nonFiniteCount)) return 'Corrigez les valeurs non finies dans les colonnes de regroupement avant de calculer ces analyses.';
  if (predictorColumns.value.some((column) => source.profiles[column]?.nonFiniteCount)) return 'Corrigez ou excluez les informations contenant des valeurs non finies avant de calculer ces analyses.';
  return '';
});
const priorityOptions = computed(() => {
  if (predictionKind.value === 'survival') return [
    { value: 'recommended', label: 'Comparer les risques dans le temps', detail: 'Le critère recommandé évalue si le modèle classe correctement les observations selon leur risque.' },
  ];
  if (predictionKind.value === 'quantity') return [
    { value: 'recommended', label: 'Réduire l’erreur moyenne', detail: 'Privilégier les prédictions les plus proches des valeurs réelles, en moyenne. Une erreur de 10 compte deux fois plus qu’une erreur de 5.' },
    { value: 'mse', label: 'Donner plus de poids aux grandes erreurs', detail: 'Les prédictions très éloignées des valeurs réelles pèsent davantage dans le choix du modèle. Une erreur de 10 compte quatre fois plus qu’une erreur de 5.' },
  ];
  return [
    { value: 'recommended', label: 'Bien reconnaître chaque catégorie', detail: 'Le critère recommandé accorde la même importance aux catégories, même si leurs effectifs diffèrent.' },
    ...(binaryTarget.value ? [
      { value: 'recall', label: 'Repérer le plus possible de cas', detail: 'Privilégier la sensibilité pour la catégorie que vous souhaitez repérer.' },
      { value: 'precision', label: 'Avoir des alertes plus fiables', detail: 'Privilégier la précision des alertes pour cette catégorie.' },
      { value: 'f1', label: 'Équilibrer détection et fiabilité', detail: 'Rechercher un compromis entre la sensibilité et la précision.' },
    ] : []),
  ];
});
const needsPositiveLabel = computed(() => predictionKind.value === 'category' && priority.value !== 'recommended');
const hasPriorityChoice = computed(() => priorityOptions.value.length > 1);
const predictionLabel = computed(() => ({ category: 'une catégorie', quantity: 'une quantité', survival: 'un événement au cours du temps' })[predictionKind.value]);
const evaluationLabel = computed(() => ({ split: '80 % pour l’apprentissage, 20 % réservés au test final', external: 'Test final sur un fichier indépendant', cross_validation: 'Validation interne seule, sans test séparé' })[evaluationMode.value]);

function scalarLabel(value: TargetLabel): string {
  return typeof value === 'boolean' ? (value ? 'true (vrai)' : 'false (faux)') : String(value);
}

function invalidateStatistics() {
  statsRevision += 1;
  if (statisticsTimer !== undefined) clearTimeout(statisticsTimer);
  statisticsTimer = undefined;
  statisticsController?.abort();
  statisticsController = undefined;
  statistics.value = undefined;
  statsError.value = '';
  statsLoading.value = false;
}

function resetSelections() {
  project.value.csvColumnToPredict = '';
  project.value.survivalModeldatasetTargetTime = '';
  project.value.rowDepedenciesLinked = [];
  project.value.datasetHasIdColumnList = [];
  project.value.rowDepedencies = false;
  project.value.datasetHasIdColumn = false;
  predictionKind.value = 'category';
  eventChoice.value = '';
  timeUnit.value = null;
  repeatedPatients.value = null;
  exclusionReasons.value = {};
  priority.value = 'recommended';
  positiveChoice.value = '';
  furthestStep.value = 1;
  step.value = 1;
  feedback.value = '';
  previewSource.value = 'train';
  dataView.value = 'preview';
  invalidateStatistics();
}

async function readFile() {
  fileController?.abort();
  const revision = ++fileRevision;
  dataset.value = undefined;
  emit('datasetReady', false);
  readingFile.value = false;
  error.value = '';
  resetSelections();
  testFile.value = null;
  testDataset.value = undefined;
  testError.value = '';
  const file = uploadedFile.value;
  if (!file) return;
  if (file.size > 100 * 1024 * 1024) {
    error.value = 'Le fichier ne doit pas dépasser 100 Mo.';
    return;
  }
  const title = file.name.replace(/\.csv$/i, '').replace(/[_-]+/g, ' ').slice(0, 100);
  if (!project.value.title || project.value.title === previousAutoTitle) project.value.title = title;
  previousAutoTitle = title;
  const controller = new AbortController();
  fileController = controller;
  readingFile.value = true;
  try {
    const result = await readProjectCsv(file, {
      signal: controller.signal,
      onPreview: (preview) => {
        if (revision !== fileRevision) return;
        dataset.value = preview;
        emit('datasetReady', true);
      },
    });
    if (revision !== fileRevision) return;
    dataset.value = result;
    emit('datasetReady', true);
    furthestStep.value = 2;
    step.value = 2;
    feedback.value = '';
  } catch (exception) {
    if (revision !== fileRevision || controller.signal.aborted) return;
    dataset.value = undefined;
    emit('datasetReady', false);
    error.value = apiError(exception);
  } finally {
    if (revision === fileRevision) readingFile.value = false;
  }
}

async function readTestFile() {
  testController?.abort();
  const revision = ++testRevision;
  previewSource.value = 'train';
  testDataset.value = undefined;
  testError.value = '';
  readingTestFile.value = false;
  const file = testFile.value;
  if (!file) {
    previewSource.value = 'train';
    return;
  }
  if (file.size > 100 * 1024 * 1024) {
    testError.value = 'Le fichier de test ne doit pas dépasser 100 Mo.';
    return;
  }
  if (file.size + (uploadedFile.value?.size ?? 0) > 100 * 1024 * 1024) {
    testError.value = 'Les fichiers d’apprentissage et de test réunis ne doivent pas dépasser 100 Mo.';
    return;
  }
  const controller = new AbortController();
  testController = controller;
  readingTestFile.value = true;
  try {
    const result = await readProjectCsv(file, { signal: controller.signal });
    if (revision !== testRevision) return;
    const missing = columns.value.filter((column) => !result.columns.includes(column));
    const extra = result.columns.filter((column) => !columns.value.includes(column));
    if (missing.length || extra.length) {
      throw new Error(`Le fichier de test doit contenir les mêmes colonnes.${missing.length ? ` Colonnes absentes : ${missing.join(', ')}.` : ''}${extra.length ? ` Colonnes supplémentaires : ${extra.join(', ')}.` : ''}`);
    }
    testDataset.value = result;
    previewSource.value = 'test';
  } catch (exception) {
    if (revision !== testRevision || controller.signal.aborted) return;
    testError.value = apiError(exception);
    previewSource.value = 'train';
  } finally {
    if (revision === testRevision) readingTestFile.value = false;
  }
}

function selectColumn(column: string) {
  if (!tableSelectable.value) return;
  error.value = '';
  if (step.value === 2) {
    project.value.csvColumnToPredict = column;
    feedback.value = `« ${column} » est le résultat que le modèle apprendra à prédire.`;
    return;
  }
  if (step.value === 3) {
    if (!repeatedPatients.value) {
      feedback.value = 'Indiquez d’abord si plusieurs lignes concernent le même patient.';
      return;
    }
    if (!featureColumns.value.includes(column)) {
      error.value = 'Une colonne à prédire ne peut pas servir au regroupement des patients.';
      return;
    }
    const selected = project.value.rowDepedenciesLinked;
    project.value.rowDepedenciesLinked = selected.includes(column) ? selected.filter((name) => name !== column) : [...selected, column];
    feedback.value = 'Les observations d’un même patient resteront ensemble lors des découpages.';
    return;
  }
  if (!selectableFeatures.value.includes(column)) {
    error.value = 'La cible, la durée de suivi et les colonnes de regroupement ont déjà un rôle distinct.';
    return;
  }
  const reasons = { ...exclusionReasons.value };
  if (reasons[column] === exclusionMode.value) {
    delete reasons[column];
    feedback.value = `« ${column} » sera de nouveau disponible pour la prédiction.`;
  } else {
    reasons[column] = exclusionMode.value;
    feedback.value = exclusionMode.value === 'identifier'
      ? `« ${column} » est un identifiant et sera exclu de l’apprentissage.`
      : `« ${column} » sera exclu : cette information ne sera pas connue au moment de la prédiction.`;
  }
  exclusionReasons.value = reasons;
}

function selectionError(currentStep: number): string {
  if (!uploadedFile.value || !dataset.value?.isComplete || readingFile.value) return 'Importez un fichier CSV et attendez la fin de son analyse.';
  if (currentStep === 1) return '';
  if (currentStep === 2) {
    const profile = targetProfile.value;
    if (!project.value.csvColumnToPredict || !profile) return 'Choisissez le résultat que vous souhaitez prédire.';
    if (profile.missingCount) return 'La colonne à prédire contient des valeurs manquantes. Complétez-la ou retirez les observations concernées dans le fichier.';
    if (profile.nonFiniteCount) return 'La colonne à prédire contient des valeurs non finies. Corrigez-les dans le fichier.';
    if (profile.distinctValues.length < 2 && !profile.distinctValuesTruncated) return 'Le résultat à prédire doit contenir au moins deux valeurs différentes.';
    if (predictionKind.value !== 'survival' && inferredKind.value !== predictionKind.value) return 'Le type de résultat choisi ne correspond pas aux valeurs de cette colonne. Choisissez le type proposé ou adaptez les valeurs de votre fichier.';
    if (predictionKind.value === 'survival') {
      if (profile.distinctValuesTruncated || profile.distinctValues.length !== 2) return 'La colonne d’événement doit distinguer deux situations : événement observé et événement non observé.';
      if (!eventOptions.value.some((option) => option.value === eventChoice.value)) return 'Indiquez quelle valeur correspond à un événement observé.';
      const timeColumn = project.value.survivalModeldatasetTargetTime;
      const timeProfile = timeColumn ? dataset.value.profiles[timeColumn] : undefined;
      if (!timeColumn || timeColumn === project.value.csvColumnToPredict) return 'Choisissez une colonne distincte contenant la durée de suivi.';
      if (!timeProfile || timeProfile.type !== 'numeric' || timeProfile.missingCount || timeProfile.nonFiniteCount || (timeProfile.min ?? -1) < 0) return 'La durée de suivi doit contenir uniquement des nombres finis, positifs ou nuls, sans valeur manquante.';
    }
    return '';
  }
  if (currentStep === 3) {
    if (repeatedPatients.value === null) return 'Indiquez si plusieurs lignes concernent le même patient.';
    if (repeatedPatients.value && !project.value.rowDepedenciesLinked.length) return 'Choisissez la colonne qui permet de reconnaître le même patient.';
    if (project.value.rowDepedenciesLinked.some((column) => !featureColumns.value.includes(column))) return 'Les colonnes de regroupement doivent être distinctes des colonnes à prédire.';
    if (project.value.rowDepedenciesLinked.some((column) => dataset.value?.profiles[column]?.missingCount)) return 'Complétez les identifiants de regroupement manquants avant de lancer l’étude.';
    if (repeatedPatients.value && project.value.rowDepedenciesLinked.length === 1
      && dataset.value.profiles[project.value.rowDepedenciesLinked[0]].distinctValues.length < 2) return 'Au moins deux groupes distincts sont nécessaires pour séparer les observations.';
    return '';
  }
  if (currentStep === 4) {
    if (!predictorColumns.value.length) return 'Conservez au moins une information disponible pour la prédiction.';
    if (predictorColumns.value.some((column) => dataset.value?.profiles[column]?.nonFiniteCount)) return 'Certaines informations retenues contiennent des valeurs non finies. Corrigez ces valeurs dans le fichier ou excluez les colonnes concernées.';
    return '';
  }
  if (currentStep === 5) {
    if (evaluationMode.value !== 'external') return '';
    if (readingTestFile.value || !testDataset.value?.isComplete || !testFile.value) return testError.value || 'Importez le fichier de test et attendez la fin de son analyse.';
    const profile = testDataset.value.profiles[project.value.csvColumnToPredict];
    if (!profile || profile.missingCount || profile.nonFiniteCount) return 'Le résultat attendu doit être renseigné et fini pour chaque observation du fichier de test.';
    if (predictionKind.value === 'survival') {
      const timeProfile = testDataset.value.profiles[project.value.survivalModeldatasetTargetTime ?? ''];
      if (!timeProfile || timeProfile.type !== 'numeric' || timeProfile.missingCount || timeProfile.nonFiniteCount || (timeProfile.min ?? -1) < 0) return 'Le fichier de test doit contenir des durées de suivi finies, positives ou nulles, sans valeur manquante.';
      if (profile.distinctValuesTruncated || profile.distinctValues.some((value) => !targetProfile.value?.distinctValues.some((known) => known === value))) return 'Le fichier de test utilise des valeurs d’événement différentes de celles du fichier d’apprentissage.';
    } else if (predictionKind.value === 'category' && (profile.distinctValuesTruncated
      || profile.distinctValues.some((value) => !targetProfile.value?.distinctValues.some((known) => known === value)))) return 'Le fichier de test contient une catégorie du résultat absente du fichier d’apprentissage.';
    return '';
  }
  if (currentStep === 6) {
    if (!priorityOptions.value.some((option) => option.value === priority.value)) return 'Choisissez une priorité adaptée à votre résultat.';
    if (needsPositiveLabel.value && (!binaryTarget.value || !eventOptions.value.some((option) => option.value === positiveChoice.value))) return 'Choisissez la catégorie correspondant aux cas que vous souhaitez repérer.';
    if (!Number.isInteger(searchDuration.value) || searchDuration.value < 60 || searchDuration.value > 3600) return 'Choisissez un temps de recherche compris entre 1 et 60 minutes.';
    return '';
  }
  if (!project.value.title.trim()) return 'Donnez un nom à votre projet.';
  if (!pipeline.value) return 'La configuration DrIA est indisponible. Réessayez lorsque le service est accessible.';
  return '';
}

function moveTo(nextStep: number) {
  if (busy.value || nextStep > furthestStep.value) return;
  error.value = '';
  feedback.value = '';
  step.value = nextStep;
  previewSource.value = 'train';
  questionForm.value?.resetValidation();
}

async function next() {
  if (busy.value) return;
  error.value = '';
  if (questionForm.value && !await questionForm.value.validate()) return;
  const message = selectionError(step.value);
  if (message) {
    error.value = message;
    return;
  }
  if (step.value < 7) {
    furthestStep.value = Math.max(furthestStep.value, step.value + 1);
    moveTo(step.value + 1);
    return;
  }
  for (let currentStep = 1; currentStep <= 7; currentStep += 1) {
    const previousError = selectionError(currentStep);
    if (previousError) {
      moveTo(currentStep);
      error.value = previousError;
      return;
    }
  }
  if (!uploadedFile.value || !pipeline.value) return;
  validating.value = true;
  try {
    const editedStudy = editor.value?.getConfiguration ? editor.value.getConfiguration() : (editor.value?.pipelineRefLocalComponent ?? pipeline.value);
    if (!editedStudy) throw new Error('Corrigez la configuration avancée avant de lancer l’apprentissage.');
    const guidedStudy = buildGuidedStudy(editedStudy, {
      predictionKind: predictionKind.value,
      priority: priority.value,
      positiveLabel: needsPositiveLabel.value ? JSON.parse(positiveChoice.value) as TargetLabel : undefined,
      searchDuration: searchDuration.value,
    });
    const result = await validateStudy(guidedStudy);
    const excluded = Object.keys(exclusionReasons.value).filter((column) => selectableFeatures.value.includes(column));
    const form: IProjectForm = {
      ...project.value,
      title: project.value.title.trim(), description: project.value.description.trim(),
      pipeline: result.configuration,
      rowDepedencies: Boolean(repeatedPatients.value),
      rowDepedenciesLinked: repeatedPatients.value ? [...project.value.rowDepedenciesLinked] : [],
      datasetHasIdColumn: excluded.length > 0, datasetHasIdColumnList: excluded,
      survivalModel: predictionKind.value === 'survival',
      survivalModeldatasetTargetTime: predictionKind.value === 'survival' ? project.value.survivalModeldatasetTargetTime : undefined,
      survivalModelEventValue: predictionKind.value === 'survival' ? eventChoice.value : undefined,
      evaluationMode: evaluationMode.value,
      testFile: evaluationMode.value === 'external' ? testFile.value ?? undefined : undefined,
      creationContextJson: JSON.stringify({
        predictionKind: predictionKind.value,
        timeUnit: predictionKind.value === 'survival' && timeUnit.value ? timeUnit.value : undefined,
        exclusionReasons: Object.fromEntries(excluded.map((column) => [column, exclusionReasons.value[column]])),
      }),
    };
    emit('submitProject', form, uploadedFile.value);
  } catch (exception) {
    error.value = apiError(exception);
  } finally {
    validating.value = false;
  }
}

async function loadStudy() {
  loadingStudy.value = true;
  studyError.value = '';
  try {
    pipeline.value = await getDefaultPipeline();
    if (!pipeline.value && userStore.isUserConnected) studyError.value = 'La configuration DrIA est indisponible. Vérifiez le service puis réessayez.';
  } finally {
    loadingStudy.value = false;
  }
}

function statisticsBody(): FormData | undefined {
  const file = visibleFile.value;
  if (!file || statisticsBlockedReason.value) return;
  const body = new FormData();
  body.set('train_dataset', file);
  body.set('dataset_target', project.value.csvColumnToPredict);
  // Descriptive analysis needs no training pipeline or search configuration.
  // Keep only a statistics recipe if the user has selected one.
  body.set('configuration', JSON.stringify({
    schema_version: 1,
    ...(statisticsRecipe.value ? { statistics: statisticsRecipe.value } : {}),
  }));
  if (predictionKind.value === 'survival' && project.value.survivalModeldatasetTargetTime) {
    body.set('dataset_target_time', project.value.survivalModeldatasetTargetTime);
    body.set('event_value', eventChoice.value);
  }
  if (project.value.rowDepedenciesLinked.length) body.set('groups_columns', JSON.stringify(project.value.rowDepedenciesLinked));
  if (excludedStatisticsColumns.value.length) body.set('excluded_columns', JSON.stringify(excludedStatisticsColumns.value));
  return body;
}

async function loadNativePlot(plan: NativeDataPlotPlan, signal: AbortSignal): Promise<IStatistics> {
  const body = statisticsBody();
  const source = visibleDataset.value;
  if (!body || !source || !plan.columns.length || plan.columns.some((column) => !predictorColumns.value.includes(column)))
    throw new Error(statisticsBlockedReason.value || 'Cette sélection de données n’est plus disponible.');
  // IAML's image titles do not identify the source column. Request only the
  // chosen inputs so a native plot can be attached without guessing its origin.
  const retained = new Set([
    ...plan.columns, project.value.csvColumnToPredict,
    ...project.value.rowDepedenciesLinked,
  ]);
  body.set('excluded_columns', JSON.stringify(source.columns.filter((column) => !retained.has(column))));
  body.set('configuration', JSON.stringify({ schema_version: 1, statistics: plan.statistics }));
  return exploreStatistics(body, true, signal);
}

async function loadStatistics() {
  const body = statisticsBody();
  if (!body || statisticsController) return;
  if (statisticsTimer !== undefined) clearTimeout(statisticsTimer);
  statisticsTimer = undefined;
  const revision = statsRevision;
  const controller = new AbortController();
  statisticsController = controller;
  statsLoading.value = true;
  statsError.value = '';
  try {
    const result = await exploreStatistics(body, false, controller.signal);
    if (revision === statsRevision && !controller.signal.aborted) statistics.value = result;
  } catch (exception) {
    if (revision === statsRevision && !controller.signal.aborted) statsError.value = apiError(exception);
  } finally {
    if (revision === statsRevision && statisticsController === controller) {
      statisticsController = undefined;
      statsLoading.value = false;
    }
  }
}

function scheduleStatistics() {
  invalidateStatistics();
  if (statisticsBlockedReason.value) return;
  // Coalesce successive answers without waiting for the summary tab to open.
  statsLoading.value = true;
  statisticsTimer = setTimeout(() => {
    statisticsTimer = undefined;
    void loadStatistics();
  }, 500);
}

watch(uploadedFile, readFile);
watch(testFile, readTestFile);
watch(() => project.value.csvColumnToPredict, () => {
  if (predictionKind.value !== 'survival' || !survivalEligible.value) predictionKind.value = inferredKind.value ?? 'category';
  eventChoice.value = '';
  positiveChoice.value = '';
  priority.value = 'recommended';
  if (project.value.survivalModeldatasetTargetTime === project.value.csvColumnToPredict) project.value.survivalModeldatasetTargetTime = '';
  error.value = '';
});
watch(predictionKind, (kind) => {
  project.value.survivalModel = kind === 'survival';
  if (kind !== 'survival') {
    eventChoice.value = '';
    project.value.survivalModeldatasetTargetTime = '';
    timeUnit.value = null;
  }
  priority.value = 'recommended';
  positiveChoice.value = '';
  error.value = '';
});
watch(repeatedPatients, (repeated) => {
  project.value.rowDepedencies = Boolean(repeated);
  if (!repeated) project.value.rowDepedenciesLinked = [];
});
watch(featureColumns, (available) => {
  project.value.rowDepedenciesLinked = project.value.rowDepedenciesLinked.filter((column) => available.includes(column));
});
watch(selectableFeatures, (available) => {
  exclusionReasons.value = Object.fromEntries(Object.entries(exclusionReasons.value).filter(([column]) => available.includes(column)));
});
watch(evaluationMode, () => { previewSource.value = 'train'; error.value = ''; });
watch([
  visibleFile,
  () => visibleDataset.value?.isComplete,
  statisticsContext,
  statisticsBlockedReason,
], scheduleStatistics, { immediate: true });
watch(step, async (nextStep) => {
  const revision = fileRevision;
  await nextTick();
  // The CSV preview appears before profiling finishes. Orient the user only
  // once the question is ready, and ignore an import replaced or cancelled.
  if (revision !== fileRevision || step.value !== nextStep || !dataset.value?.isComplete) return;
  const card = questionCard.value;
  const heading = card?.querySelector<HTMLElement>('.question-title');
  if (!card || !heading) return;
  heading.focus({ preventScroll: true });
  const bounds = card.getBoundingClientRect();
  if (bounds.top < 72 || heading.getBoundingClientRect().bottom > window.innerHeight - 24
    || (window.innerWidth < 1024 && bounds.top > 88)) {
    card.scrollIntoView({
      block: 'start',
      behavior: window.matchMedia('(prefers-reduced-motion: reduce)').matches ? 'instant' as ScrollBehavior : 'smooth',
    });
  }
});
onMounted(loadStudy);
onUnmounted(() => {
  fileRevision += 1;
  testRevision += 1;
  invalidateStatistics();
  fileController?.abort();
  testController?.abort();
});
</script>

<template>
  <section class="guided-project project-creation-colors" :class="{ 'q-mt-lg': dataset }" aria-label="Création guidée d’un projet">
    <ProjectFileUpload
      v-if="!dataset"
      :loading="readingFile"
      :disabled="busy"
      :error="error"
      :filename="uploadedFile?.name"
      @select-file="uploadedFile = $event"
      @reject-file="error = $event"
      @cancel="uploadedFile = null"
    />
    <template v-else>
      <div class="wizard-heading q-mb-md">
        <div>
          <h1 class="text-h5 text-primary q-my-none">Préparer votre étude</h1>
          <p class="text-grey-7 q-mt-xs q-mb-none">Répondez aux questions pour préparer votre étude. Vos données restent disponibles à chaque étape.</p>
        </div>
        <span class="step-counter text-caption text-grey-7">Étape {{ step }} sur 7</span>
      </div>
      <nav class="wizard-steps q-mb-md" aria-label="Étapes de création">
        <button
          v-for="(item, index) in steps"
          :key="item.title"
          type="button"
          class="wizard-step"
          :class="{ 'current-step': step === index + 1, 'visited-step': furthestStep > index + 1 }"
          :disabled="index + 1 > furthestStep || busy"
          :aria-current="step === index + 1 ? 'step' : undefined"
          :aria-label="`Étape ${index + 1} : ${item.title}`"
          @click="moveTo(index + 1)"
        >
          <span class="step-number"><q-icon v-if="furthestStep > index + 1 && step !== index + 1" name="check" size="16px" /><template v-else>{{ index + 1 }}</template></span>
          <span class="step-name">{{ item.title }}</span>
        </button>
      </nav>
      <q-linear-progress :value="step / 7" color="accent" size="4px" rounded class="q-mb-lg" aria-label="Progression du questionnaire" />

      <div class="creation-layout">
        <div ref="questionCard" class="question-panel">
          <q-card flat bordered class="question-card">
            <div class="question-guidance">
              <span class="question-guidance-icon"><q-icon :name="steps[step - 1].icon" size="23px" aria-hidden="true" /></span>
              <div>
                <div class="question-guidance-title">{{ !dataset?.isComplete ? 'Lecture de votre fichier' : step === 2 && !project.csvColumnToPredict ? 'Commencez ici' : 'Votre prochaine étape' }}</div>
                <div class="question-guidance-step">{{ steps[step - 1].title }} · Étape {{ step }} sur 7</div>
              </div>
            </div>
            <q-form ref="questionForm" class="q-pa-lg" @submit.prevent="next">
              <template v-if="step === 1">
                <h2 class="question-title" tabindex="-1">Quel fichier contient vos observations ?</h2>
                <p class="question-help">Importez un fichier CSV : une observation par ligne et un nom pour chaque colonne.</p>
                <q-file v-model="uploadedFile" filled clearable color="primary" label="Importer mes données" accept=".csv,text/csv" :max-file-size="100 * 1024 * 1024" :disable="busy" @rejected="error = 'Le fichier ne doit pas dépasser 100 Mo.'">
                  <template #prepend><q-icon name="upload_file" /></template>
                </q-file>
                <div v-if="readingFile" class="row items-center text-caption text-grey-7 q-mt-md" role="status"><q-spinner color="primary" size="20px" class="q-mr-sm" />Analyse de toutes les observations…</div>
                <div v-else-if="dataset?.isComplete" class="file-success q-mt-md" role="status"><q-icon name="check_circle" color="secondary" class="q-mr-xs" /> {{ number.format(dataset.rowCount) }} observations prêtes à explorer.</div>
                <p class="question-note q-mt-md">Le tableau apparaît dès les premières lignes lues. Vous le verrez évoluer avec vos réponses.</p>
              </template>

              <template v-else-if="step === 2">
                <h2 class="question-title" tabindex="-1">Quel résultat souhaitez-vous prédire ?</h2>
                <p class="question-help">Choisissez sa colonne dans la liste ou directement dans le tableau.</p>
                <q-select v-model="project.csvColumnToPredict" :options="columns" filled label="Résultat à prédire" :disable="busy" popup-content-class="project-creation-colors" />
                <template v-if="project.csvColumnToPredict">
                  <div v-if="inferredKind && !survivalAnalysis" class="answer-feedback q-mt-md">
                    <strong>{{ inferredKind === 'category' ? 'Prédiction d’une catégorie' : 'Prédiction d’une quantité' }}</strong>
                    <div>{{ inferredKind === 'category' ? 'DrIA apprendra à reconnaître les catégories présentes dans cette colonne.' : 'DrIA apprendra à estimer la valeur numérique de cette colonne.' }} Le type est détecté à partir des valeurs du fichier.</div>
                  </div>
                  <template v-if="survivalEligible">
                    <q-toggle v-model="survivalAnalysis" color="primary" label="Étudier cet événement au cours du temps (analyse de survie)" :disable="busy" class="q-mt-md" />
                    <p class="question-note q-mt-xs">À activer si ces deux valeurs indiquent si un événement a été observé, par exemple une rechute. La durée de suivi sera choisie dans une autre colonne.</p>
                  </template>
                  <template v-if="predictionKind === 'survival'">
                    <p class="question-note q-mt-md">La colonne choisie indique si l’événement a été observé. L’autre valeur signifie qu’il n’a pas été observé pendant le suivi.</p>
                    <q-select v-model="eventChoice" :options="eventOptions" filled emit-value map-options label="Quelle valeur signifie « événement observé » ?" :disable="busy" class="q-mt-md" popup-content-class="project-creation-colors" />
                    <q-select v-model="project.survivalModeldatasetTargetTime" :options="columns.filter(column => column !== project.csvColumnToPredict)" filled label="Quelle colonne contient la durée de suivi ?" :disable="busy" class="q-mt-md" popup-content-class="project-creation-colors" />
                    <q-select v-model="timeUnit" :options="['jours', 'semaines', 'mois', 'années', 'heures']" filled clearable label="Unité affichée dans le bilan (facultative)" :disable="busy" class="q-mt-md" popup-content-class="project-creation-colors" />
                    <p class="question-note q-mt-sm">Les durées sont utilisées telles qu’elles figurent dans le fichier. L’unité sert à lire le bilan, sans convertir les valeurs.</p>
                  </template>
                </template>
              </template>

              <template v-else-if="step === 3">
                <h2 class="question-title" tabindex="-1">Plusieurs lignes concernent-elles le même patient ?</h2>
                <p class="question-help">Si un patient apparaît plusieurs fois, ses lignes doivent rester ensemble. Cela évite de tester le modèle sur un patient déjà vu pendant l’apprentissage.</p>
                <q-option-group v-model="repeatedPatients" :options="[{ label: 'Oui', value: true }, { label: 'Non', value: false }]" inline color="primary" :disable="busy" />
                <template v-if="repeatedPatients">
                  <q-select v-model="project.rowDepedenciesLinked" :options="featureColumns" multiple use-chips filled label="Quelle colonne permet de reconnaître le même patient ?" :disable="busy" class="q-mt-md" popup-content-class="project-creation-colors" />
                  <p class="question-note q-mt-sm">Ses observations resteront ensemble lors des découpages. Ce repère sera exclu des informations utilisées pour prédire.</p>
                  <p class="question-note">Si l’identifiant dépend de plusieurs colonnes, sélectionnez-les toutes : leur combinaison désignera un groupe.</p>
                </template>
                <p v-else-if="repeatedPatients === false" class="question-note q-mt-md">DrIA pourra répartir les lignes séparément entre apprentissage et évaluation.</p>
              </template>

              <template v-else-if="step === 4">
                <h2 class="question-title" tabindex="-1">Quelles colonnes faut-il retirer de l’apprentissage ?</h2>
                <p class="question-help">Retirez les identifiants et les informations qui ne seront pas disponibles quand le modèle devra prédire. Pour une prédiction à l’admission, un résultat obtenu à la sortie arrive trop tard.</p>
                <q-btn-toggle v-model="exclusionMode" no-caps unelevated color="grey-2" text-color="grey-8" toggle-color="primary" :options="[{ label: 'Identifiants', value: 'identifier' }, { label: 'Informations indisponibles', value: 'unavailable' }]" :disable="busy" class="exclusion-toggle q-mt-sm" />
                <p class="question-note q-mt-sm">{{ exclusionMode === 'identifier' ? 'Par exemple : numéro de dossier, nom du patient. Les colonnes de regroupement sont déjà exclues des prédicteurs.' : 'Par exemple : un examen réalisé après la décision ou une information qui révèle déjà le résultat à prédire.' }}</p>
                <q-select v-model="selectedExclusions" :options="selectableFeatures" multiple use-chips filled :label="exclusionMode === 'identifier' ? 'Colonnes contenant des identifiants' : 'Informations connues trop tard'" :disable="busy" class="q-mt-md" popup-content-class="project-creation-colors" />
                <p class="question-note q-mt-sm">Vous pouvez aussi cliquer sur les colonnes dans le tableau. Un second clic annule leur exclusion.</p>
                <div v-if="Object.keys(exclusionReasons).length" class="excluded-list q-mt-md">
                  <div v-for="(reason, column) in exclusionReasons" :key="column" class="excluded-item">
                    <span><strong>{{ column }}</strong><small>{{ reason === 'identifier' ? 'Identifiant' : 'Indisponible au moment de la prédiction' }}</small></span>
                    <q-btn flat round dense size="sm" icon="close" color="grey-7" :aria-label="`Utiliser de nouveau ${column}`" :disable="busy" @click="exclusionReasons = Object.fromEntries(Object.entries(exclusionReasons).filter(([name]) => name !== column))" />
                  </div>
                </div>
                <div class="file-success q-mt-md">{{ predictorColumns.length }} information{{ predictorColumns.length > 1 ? 's' : '' }} conservée{{ predictorColumns.length > 1 ? 's' : '' }} pour prédire.</div>
              </template>

              <template v-else-if="step === 5">
                <h2 class="question-title" tabindex="-1">Quelles données réserver au test final ?</h2>
                <p class="question-help">Un test final mesure les performances sur des données tenues à l’écart de la recherche du modèle.</p>
                <div class="answer-options">
                  <label class="answer-option" :class="{ selected: evaluationMode === 'split' }">
                    <q-radio v-model="evaluationMode" val="split" color="primary" :disable="busy" label="Réserver automatiquement 20 % des données" />
                    <span>DrIA utilise 80 % des données pour rechercher le modèle et garde 20 % pour son test final.<q-badge outline color="secondary" class="q-ml-xs">Recommandé</q-badge></span>
                  </label>
                  <label class="answer-option" :class="{ selected: evaluationMode === 'external' }">
                    <q-radio v-model="evaluationMode" val="external" color="primary" :disable="busy" label="Utiliser un autre fichier pour le test" />
                    <span>Un fichier indépendant contenant les mêmes colonnes et les résultats attendus.</span>
                  </label>
                  <label class="answer-option" :class="{ selected: evaluationMode === 'cross_validation' }">
                    <q-radio v-model="evaluationMode" val="cross_validation" color="primary" :disable="busy" label="Ne pas réserver de test séparé" />
                    <span>Toutes les données restent disponibles pour la recherche. Les scores proviennent de la validation interne définie dans les réglages.</span>
                  </label>
                </div>
                <p v-if="repeatedPatients" class="question-note q-mt-md">Les observations d’un même patient restent ensemble lors des découpages.</p>
                <template v-if="evaluationMode === 'external'">
                  <q-file v-model="testFile" filled clearable label="Importer le fichier de test" accept=".csv,text/csv" :max-file-size="100 * 1024 * 1024" :disable="busy" class="q-mt-md" @rejected="testError = 'Le fichier de test ne doit pas dépasser 100 Mo.'">
                    <template #prepend><q-icon name="upload_file" /></template>
                  </q-file>
                  <div v-if="readingTestFile" class="row items-center text-caption text-grey-7 q-mt-sm" role="status"><q-spinner color="primary" size="18px" class="q-mr-sm" />Analyse du fichier de test…</div>
                  <div v-if="testError" class="text-caption text-negative q-mt-sm" role="alert">{{ testError }}</div>
                  <div v-if="testDataset" class="file-success q-mt-sm">{{ number.format(testDataset.rowCount) }} observations disponibles pour le test.</div>
                </template>
              </template>

              <template v-else-if="step === 6">
                <h2 class="question-title" tabindex="-1">Comment orienter la recherche du modèle ?</h2>
                <p class="question-help">Définissez {{ hasPriorityChoice ? 'le critère à privilégier et ' : '' }}le temps maximal consacré à la recherche.</p>
                <div v-if="hasPriorityChoice" class="field-label q-mb-sm">{{ predictionKind === 'quantity' ? 'Comment souhaitez-vous évaluer les erreurs de prédiction ?' : 'Quelle erreur souhaitez-vous limiter en priorité ?' }}</div>
                <p v-if="predictionKind === 'quantity'" class="question-note q-mb-sm">Une erreur est l’écart entre la valeur prédite et la valeur réelle, dans l’unité de vos données.</p>
                <div v-if="hasPriorityChoice" class="answer-options">
                  <label v-for="option in priorityOptions" :key="option.value" class="answer-option" :class="{ selected: priority === option.value }">
                    <q-radio v-model="priority" :val="option.value" color="primary" :disable="busy" :label="option.label" />
                    <span>{{ option.detail }}<q-badge v-if="option.value === 'recommended'" outline color="secondary" class="q-ml-xs">Recommandé</q-badge></span>
                  </label>
                </div>
                <div v-else class="answer-feedback">
                  <strong>Critère automatique : {{ priorityOptions[0].label }}</strong>
                  <div>{{ priorityOptions[0].detail }}</div>
                </div>
                <q-select v-if="needsPositiveLabel" v-model="positiveChoice" :options="eventOptions" filled emit-value map-options label="Quelle catégorie correspond aux cas à repérer ?" :disable="busy" class="q-mt-md" popup-content-class="project-creation-colors" />
                <p v-if="hasPriorityChoice" class="question-note q-mt-sm">Ce critère sert à choisir le modèle pendant la recherche. Les autres scores restent disponibles dans les résultats.</p>
                <q-select v-model="searchDuration" filled emit-value map-options label="Temps maximal de recherche du modèle" :options="[{ label: '1 minute - essai rapide', value: 60 }, { label: '5 minutes', value: 300 }, { label: '15 minutes', value: 900 }, { label: '30 minutes', value: 1800 }, { label: '60 minutes', value: 3600 }]" :disable="busy" class="q-mt-lg" popup-content-class="project-creation-colors" />
                <p class="question-note q-mt-sm">Un temps plus long permet d’explorer davantage de réglages. L’évaluation finale peut se poursuivre après cette recherche.</p>
              </template>

              <template v-else>
                <h2 class="question-title" tabindex="-1">Voici ce que Dr IA va apprendre</h2>
                <p class="question-help">Vérifiez votre objectif, puis lancez la recherche.</p>
                <dl class="study-summary">
                  <div><dt>À prédire</dt><dd>{{ project.csvColumnToPredict }} · {{ predictionLabel }}</dd></div>
                  <div v-if="predictionKind === 'survival'"><dt>Suivi</dt><dd>{{ project.survivalModeldatasetTargetTime }}<template v-if="timeUnit"> en {{ timeUnit }}</template><br>Événement observé : {{ eventOptions.find(option => option.value === eventChoice)?.label }}</dd></div>
                  <div><dt>Observations</dt><dd>{{ number.format(dataset?.rowCount ?? 0) }} lignes<br><template v-if="repeatedPatients">Regroupement : {{ project.rowDepedenciesLinked.join(', ') }}</template><template v-else>Lignes réparties séparément</template></dd></div>
                  <div><dt>Informations</dt><dd>{{ predictorColumns.length }} retenue{{ predictorColumns.length > 1 ? 's' : '' }}, {{ Object.keys(exclusionReasons).length }} exclue{{ Object.keys(exclusionReasons).length > 1 ? 's' : '' }}</dd></div>
                  <div><dt>Évaluation</dt><dd>{{ evaluationLabel }}</dd></div>
                  <div><dt>Priorité</dt><dd>{{ priorityOptions.find(option => option.value === priority)?.label }}<template v-if="needsPositiveLabel"><br>Cas à repérer : {{ eventOptions.find(option => option.value === positiveChoice)?.label }}</template></dd></div>
                  <div><dt>Recherche</dt><dd>{{ searchDuration / 60 }} minute{{ searchDuration > 60 ? 's' : '' }} au maximum</dd></div>
                </dl>
                <q-input v-model="project.title" filled label="Nom du projet" :maxlength="100" :rules="[value => Boolean(value?.trim()) || 'Donnez un nom à votre projet.']" :disable="busy" />
                <q-input v-model="project.description" filled autogrow label="Description (facultative)" :maxlength="2000" :disable="busy" class="q-mt-sm" />
                <p class="question-note q-mt-md">Les prétraitements, algorithmes et réglages de validation sont proposés automatiquement. Vous pouvez les ajuster dans les réglages avancés ci-dessous.</p>
                <div v-if="studyError" class="text-caption text-negative q-mt-md" role="alert">{{ studyError }}<q-btn flat no-caps dense color="primary" label="Réessayer" :loading="loadingStudy" @click="loadStudy" /></div>
                <div v-if="loadingStudy" class="row items-center text-caption text-grey-7 q-mt-sm"><q-spinner size="18px" color="primary" class="q-mr-sm" />Préparation de l’étude…</div>
              </template>

              <div v-if="feedback" class="answer-feedback q-mt-md" role="status">{{ feedback }}</div>
              <div v-if="error" class="form-error q-mt-md" role="alert"><q-icon name="error_outline" size="18px" class="q-mr-xs" />{{ error }}</div>
              <div class="wizard-actions q-mt-lg">
                <q-btn v-if="step > 1" flat no-caps color="primary" icon="arrow_back" label="Retour" :disable="busy" @click="moveTo(step - 1)" />
                <q-btn unelevated no-caps color="primary" :icon-right="step < 7 ? 'arrow_forward' : 'play_arrow'" :label="step < 7 ? 'Continuer' : 'Lancer l’apprentissage'" type="submit" :loading="busy" :disable="readingFile || readingTestFile || (step === 7 && (!pipeline || loadingStudy))" />
              </div>
            </q-form>
          </q-card>
        </div>

        <div class="data-panel">
          <div v-if="testDataset" class="row items-center justify-between q-mb-sm q-col-gutter-sm">
            <div class="col-auto">
              <q-btn-toggle v-model="previewSource" no-caps unelevated rounded color="grey-2" text-color="grey-8" toggle-color="primary" :options="[{ label: 'Apprentissage', value: 'train' }, { label: 'Test indépendant', value: 'test' }]" :disable="busy" size="sm" />
            </div>
            <div class="col text-right text-caption text-grey-7">{{ previewSource === 'test' ? testFile?.name : uploadedFile?.name }}</div>
          </div>
          <q-tabs v-model="dataView" dense no-caps align="justify" active-color="primary" indicator-color="accent" class="data-view-tabs q-mb-sm" aria-label="Consulter les données">
            <q-tab name="preview" icon="table_chart" label="Vos données" />
            <q-tab name="summary" icon="analytics" label="Bilan des données" />
            <q-tab name="plots" icon="query_stats" label="Graphiques" />
          </q-tabs>
          <q-tab-panels v-model="dataView" keep-alive class="data-view-panels">
            <q-tab-panel name="preview" class="q-pa-none">
              <ProjectDataPreview
                :dataset="visibleDataset"
                :roles="roles"
                :selectable="Boolean(tableSelectable)"
                :active-columns="activeColumns"
                :group-columns="project.rowDepedenciesLinked"
                :selection-hint="selectionHint"
                @select-column="selectColumn"
              />
            </q-tab-panel>
            <q-tab-panel name="summary" class="q-pa-none">
              <ProjectDataSummary
                v-if="visibleDataset"
                :dataset="visibleDataset"
                :roles="roles"
                :target-column="project.csvColumnToPredict"
                :prediction-kind="predictionKind"
                :filename="visibleFile?.name"
                :group-columns="project.rowDepedenciesLinked"
                :time-unit="timeUnit ?? undefined"
                :event-value="eventChoice"
                :statistics="statistics"
                :statistics-loading="statsLoading"
                :statistics-error="statsError"
                :statistics-unavailable-reason="statisticsBlockedReason"
                @retry-statistics="loadStatistics"
              />
            </q-tab-panel>
            <q-tab-panel name="plots" class="q-pa-none">
              <ProjectDataPlots
                v-if="visibleDataset"
                :dataset="visibleDataset"
                :columns="predictorColumns"
                :statistics="statistics"
                :statistics-loading="statsLoading"
                :statistics-error="statsError"
                :prediction-kind="predictionKind"
                :target-column="project.csvColumnToPredict"
                :disabled="busy"
                :unavailable-reason="statisticsBlockedReason"
                :load-native-plot="loadNativePlot"
                :analysis-context="statisticsContext"
                @retry-statistics="loadStatistics"
              />
            </q-tab-panel>
          </q-tab-panels>
        </div>
      </div>

      <q-card v-if="pipeline" v-show="step === 7" flat bordered class="advanced-card q-mt-lg">
        <q-expansion-item v-model="showAdvanced" icon="settings" label="Réglages avancés (facultatifs)" caption="Prétraitements, algorithmes, validation et analyses" :disable="busy" @before-show="advancedOpened = true">
          <div class="q-pa-md" :inert="busy ? '' : undefined">
            <p class="text-caption text-grey-7">Le critère principal et la durée de recherche restent ceux de l’étape « Rechercher », même si vous modifiez la configuration complète.</p>
            <PipelineProject v-if="advancedOpened" ref="editor" :pipeline="pipeline" guided-search />
          </div>
        </q-expansion-item>
      </q-card>
    </template>
  </section>
</template>

<style scoped lang="scss">
.guided-project { width: 100%; max-width: 1480px; margin-left: auto; margin-right: auto; min-width: 0; }
.wizard-heading { display: flex; align-items: center; justify-content: space-between; gap: 16px; }
.step-counter { flex-shrink: 0; }
.wizard-steps { display: flex; gap: 8px; justify-content: space-between; }
.wizard-step { display: flex; align-items: center; justify-content: center; flex: 1; gap: 7px; border: 0; padding: 8px 4px; border-radius: 6px; color: var(--project-muted); background: transparent; cursor: pointer; font: inherit; font-size: 12px; }
.wizard-step:disabled { opacity: .65; cursor: default; }
.wizard-step:focus-visible { outline: 2px solid var(--q-primary); outline-offset: 2px; }
.step-number { display: inline-flex; align-items: center; justify-content: center; flex-shrink: 0; width: 25px; height: 25px; border-radius: 50%; background: var(--project-paper); color: var(--project-muted); font-weight: 600; }
.current-step { color: var(--project-selection-ink); background: var(--project-selection-surface); }
.current-step .step-number { color: var(--project-selection-ink); background: var(--q-accent); }
.visited-step .step-number { color: white; background: var(--q-secondary); }
.creation-layout { display: grid; grid-template-columns: minmax(380px, 440px) minmax(0, 1fr); align-items: start; gap: 24px; }
.data-panel { position: sticky; top: 76px; min-width: 0; }
.question-panel { min-width: 0; scroll-margin-top: 80px; }
.question-card,
.advanced-card { min-width: 0; border-radius: 8px; overflow: hidden; }
.question-card { border: 1px solid var(--project-border); box-shadow: 0 8px 28px var(--project-shadow); }
.question-guidance { display: flex; align-items: center; gap: 12px; padding: 18px 24px; color: white; background: var(--project-ink); }
.question-guidance-icon { display: inline-flex; align-items: center; justify-content: center; flex-shrink: 0; width: 42px; height: 42px; border-radius: 10px; color: var(--project-selection-ink); background: var(--q-accent); }
.question-guidance-title { font-size: 16px; font-weight: 600; line-height: 1.4; }
.question-guidance-step { margin-top: 3px; font-size: 12px; line-height: 1.4; }
.data-view-tabs { border: 1px solid var(--project-border); border-radius: 8px; background: var(--project-paper); }
.data-view-tabs :deep(.q-tab) { min-width: 0; min-height: 58px; }
.data-view-tabs :deep(.q-tab__label) { font-size: 13px; }
.data-view-panels { background: transparent; overflow: visible; }
.question-title { font-size: 25px; font-weight: 600; line-height: 1.35; color: var(--project-ink); margin: 0 0 14px; overflow-wrap: anywhere; }
.question-title:focus-visible { outline: 2px solid var(--q-primary); outline-offset: 5px; border-radius: 2px; }
.question-help { font-size: 14px; line-height: 1.6; color: var(--project-muted); margin-bottom: 22px; }
.question-note { font-size: 12px; line-height: 1.6; color: var(--project-muted); margin-bottom: 0; }
.field-label { font-size: 14px; font-weight: 500; line-height: 1.5; color: var(--project-ink); }
.file-success { font-size: 13px; line-height: 1.6; color: var(--q-primary); }
.wizard-actions { display: flex; align-items: center; justify-content: space-between; gap: 8px; }
.wizard-actions > :last-child { margin-left: auto; }
.form-error { font-size: 13px; line-height: 1.55; color: var(--q-negative); background: #fff3f2; padding: 10px 12px; border-radius: 6px; overflow-wrap: anywhere; }
.answer-feedback { font-size: 12px; line-height: 1.55; color: var(--q-primary); background: var(--project-tint); padding: 10px 12px; border-radius: 6px; }
.exclusion-toggle { width: 100%; }
.exclusion-toggle :deep(.q-btn) { min-width: 0; flex: 1; padding-left: 10px; padding-right: 10px; }
.exclusion-toggle :deep(.q-btn__content) { line-height: 1.4; }
.excluded-item { display: flex; align-items: center; justify-content: space-between; gap: 8px; padding: 8px 0; border-bottom: 1px solid var(--project-border); font-size: 12px; }
.excluded-item span { min-width: 0; overflow-wrap: anywhere; }
.excluded-item small { display: block; color: var(--project-muted); margin-top: 2px; }
.answer-options { display: flex; flex-direction: column; gap: 10px; }
.answer-option { display: block; border: 1px solid var(--project-border); border-radius: 6px; padding: 8px 12px 12px 3px; cursor: pointer; }
.answer-option.selected { border-color: var(--q-primary); background: var(--project-selection-surface); }
.answer-option > span { display: block; padding-left: 41px; font-size: 12px; line-height: 1.6; color: var(--project-muted); }
.answer-option :deep(.q-radio__label) { font-size: 13px; color: var(--project-ink); }
.study-summary { margin: 0 0 20px; }
.study-summary > div { display: grid; grid-template-columns: 93px minmax(0, 1fr); gap: 10px; padding: 8px 0; border-bottom: 1px solid var(--project-border); font-size: 12px; line-height: 1.6; }
.study-summary dt { color: var(--project-muted); }
.study-summary dd { margin: 0; color: var(--project-ink); overflow-wrap: anywhere; }
.advanced-card :deep(.study-editor) { width: 100%; min-width: 0; }

@media (max-width: 1023px) {
  .creation-layout { grid-template-columns: minmax(0, 1fr); gap: 20px; }
  .question-panel { grid-row: 1; }
  .data-panel { position: static; grid-row: 2; }
}
@media (max-width: 599px) {
  .data-view-tabs :deep(.q-tab) { padding-left: 6px; padding-right: 6px; }
  .data-view-tabs :deep(.q-tab__label) { font-size: 12px; }
  .wizard-heading { align-items: flex-start; gap: 8px; }
  .wizard-heading h1 { font-size: 21px; }
  .wizard-heading p { font-size: 12px; }
  .step-counter { padding-top: 5px; font-size: 11px; }
  .wizard-steps { gap: 3px; }
  .wizard-step { padding: 6px 0; }
  .step-name { display: none; }
  .question-card :deep(.q-form) { padding: 20px; }
  .question-guidance { padding: 16px 20px; }
  .question-guidance-title { font-size: 15px; }
  .question-title { font-size: 23px; }
  .study-summary > div { grid-template-columns: 85px minmax(0, 1fr); gap: 7px; }
  .wizard-actions :deep(.q-btn) { padding-left: 10px; padding-right: 10px; font-size: 12px; }
}
</style>
