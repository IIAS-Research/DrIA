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
  { title: 'Upload', icon: 'upload_file' },
  { title: 'Predict', icon: 'flag' },
  { title: 'Group', icon: 'group' },
  { title: 'Features', icon: 'view_column' },
  { title: 'Evaluate', icon: 'fact_check' },
  { title: 'Search', icon: 'tune' },
  { title: 'Start', icon: 'play_arrow' },
];
const number = new Intl.NumberFormat('en-US');
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
      ? { role: 'event', label: 'Event', reason: eventChoice.value ? `The observed event corresponds to ${eventOptions.value.find((option) => option.value === eventChoice.value)?.label}.` : 'This column indicates whether the event was observed.' }
      : { role: 'target', label: 'Target', reason: 'The model will learn to predict this column.' };
  } else if (predictionKind.value === 'survival' && column === project.value.survivalModeldatasetTargetTime) {
    annotation = { role: 'time', label: 'Follow-up duration', reason: timeUnit.value ? `Duration measured in ${timeUnit.value}.` : 'This column contains the follow-up duration in the units used in the file.' };
  } else if (project.value.rowDepedenciesLinked.includes(column)) {
    annotation = { role: 'group', label: 'Patient grouping', reason: 'These observations stay together when the data is split. This column is not used for prediction.' };
  } else if (exclusionReasons.value[column] === 'identifier') {
    annotation = { role: 'identifier', label: 'Identifier — excluded', reason: 'This column is an identifier and will be excluded from training.' };
  } else if (exclusionReasons.value[column] === 'unavailable') {
    annotation = { role: 'unavailable', label: 'Unavailable — excluded', reason: 'This information will not be available at prediction time.' };
  } else {
    annotation = { role: 'predictor', label: 'Feature', reason: 'This information can be used for prediction.' };
  }
  return [column, annotation];
})));
const tableSelectable = computed(() => !busy.value && dataset.value?.isComplete && previewSource.value === 'train' && [2, 3, 4].includes(step.value));
const activeColumns = computed(() => step.value === 2 ? [project.value.csvColumnToPredict].filter(Boolean)
  : step.value === 3 ? project.value.rowDepedenciesLinked : step.value === 4 ? selectedExclusions.value : []);
const selectionHint = computed(() => step.value === 2 ? 'Click the name of the outcome you want to predict.'
  : step.value === 3 && repeatedPatients.value ? 'Click the columns that identify the same patient.'
    : step.value === 4 ? `Click columns ${exclusionMode.value === 'identifier' ? 'that contain identifiers' : 'unavailable at prediction time'} to exclude them.` : '');
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
  if (!source?.isComplete || !visibleFile.value) return 'Analyses will be available once the file has been fully read.';
  const target = source.profiles[project.value.csvColumnToPredict];
  if (!target) return 'Choose the target column in step 2 to access statistics and charts.';
  if (target.missingCount) return 'Fill in missing target values before calculating these analyses.';
  if (target.nonFiniteCount) return 'Correct nonfinite target values before calculating these analyses.';
  if (predictionKind.value === 'survival') {
    if (target.distinctValuesTruncated || target.distinctValues.length > 2) return 'The event column must distinguish observed and unobserved events.';
    if (!eventOptions.value.some((option) => option.value === eventChoice.value)) return 'Specify the value for the observed event in step 2.';
    const eventValue = JSON.parse(eventChoice.value) as TargetLabel;
    if (!target.distinctValues.includes(eventValue)) return 'The selected observed-event value is absent from this file. Survival analyses require at least one observed event in the file.';
    const timeColumn = project.value.survivalModeldatasetTargetTime;
    const time = timeColumn ? source.profiles[timeColumn] : undefined;
    if (!time || timeColumn === project.value.csvColumnToPredict) return 'Choose the follow-up duration column in step 2.';
    if (time.type !== 'numeric' || time.missingCount || time.nonFiniteCount || (time.min ?? -1) < 0) return 'Follow-up duration must be present, finite, and nonnegative for every observation.';
  }
  if (!predictorColumns.value.length) return 'Keep at least one feature for prediction to calculate these analyses.';
  if (project.value.rowDepedenciesLinked.some((column) => source.profiles[column]?.missingCount)) return 'Fill in missing values in grouping columns before calculating these analyses.';
  if (project.value.rowDepedenciesLinked.some((column) => source.profiles[column]?.nonFiniteCount)) return 'Correct nonfinite values in grouping columns before calculating these analyses.';
  if (predictorColumns.value.some((column) => source.profiles[column]?.nonFiniteCount)) return 'Correct or exclude features with nonfinite values before calculating these analyses.';
  return '';
});
const priorityOptions = computed(() => {
  if (predictionKind.value === 'survival') return [
    { value: 'recommended', label: 'Compare risks over time', detail: 'The recommended metric measures how well the model ranks observations by risk.' },
  ];
  if (predictionKind.value === 'quantity') return [
    { value: 'recommended', label: 'Reduce the average error', detail: 'Favor predictions closer to the actual values on average. An error of 10 counts twice as much as an error of 5.' },
    { value: 'mse', label: 'Give larger errors more weight', detail: 'Predictions far from the actual values have more influence on model selection. An error of 10 counts four times as much as an error of 5.' },
  ];
  return [
    { value: 'recommended', label: 'Identify each category accurately', detail: 'The recommended metric gives each category equal importance, even when their sizes differ.' },
    ...(binaryTarget.value ? [
      { value: 'recall', label: 'Detect as many cases as possible', detail: 'Prioritize sensitivity for the category you want to detect.' },
      { value: 'precision', label: 'Make alerts more reliable', detail: 'Prioritize the precision of alerts for this category.' },
      { value: 'f1', label: 'Balance detection and reliability', detail: 'Find a balance between sensitivity and precision.' },
    ] : []),
  ];
});
const needsPositiveLabel = computed(() => predictionKind.value === 'category' && priority.value !== 'recommended');
const hasPriorityChoice = computed(() => priorityOptions.value.length > 1);
const predictionLabel = computed(() => ({ category: 'a category', quantity: 'a quantity', survival: 'an event over time' })[predictionKind.value]);
const evaluationLabel = computed(() => ({ split: '80% for training, 20% for the holdout test set', external: 'Final test on an independent file', cross_validation: 'Internal validation only, without a separate test set' })[evaluationMode.value]);

function scalarLabel(value: TargetLabel): string {
  return typeof value === 'boolean' ? (value ? 'true' : 'false') : String(value);
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
    error.value = 'The file must not exceed 100 MB.';
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
    testError.value = 'The test file must not exceed 100 MB.';
    return;
  }
  if (file.size + (uploadedFile.value?.size ?? 0) > 100 * 1024 * 1024) {
    testError.value = 'Training and test files must not exceed 100 MB combined.';
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
      throw new Error(`The test file must contain the same columns.${missing.length ? ` Missing columns: ${missing.join(', ')}.` : ''}${extra.length ? ` Extra columns: ${extra.join(', ')}.` : ''}`);
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
    feedback.value = `“${column}” is the outcome the model will learn to predict.`;
    return;
  }
  if (step.value === 3) {
    if (!repeatedPatients.value) {
      feedback.value = 'First, indicate whether multiple rows refer to the same patient.';
      return;
    }
    if (!featureColumns.value.includes(column)) {
      error.value = 'A target column cannot be used to group patients.';
      return;
    }
    const selected = project.value.rowDepedenciesLinked;
    project.value.rowDepedenciesLinked = selected.includes(column) ? selected.filter((name) => name !== column) : [...selected, column];
    feedback.value = 'Observations from the same patient will stay together when the data is split.';
    return;
  }
  if (!selectableFeatures.value.includes(column)) {
    error.value = 'The target, follow-up duration, and grouping columns already have a separate role.';
    return;
  }
  const reasons = { ...exclusionReasons.value };
  if (reasons[column] === exclusionMode.value) {
    delete reasons[column];
    feedback.value = `“${column}” will be available for prediction again.`;
  } else {
    reasons[column] = exclusionMode.value;
    feedback.value = exclusionMode.value === 'identifier'
      ? `“${column}” is an identifier and will be excluded from training.`
      : `“${column}” will be excluded because it will not be available at prediction time.`;
  }
  exclusionReasons.value = reasons;
}

function selectionError(currentStep: number): string {
  if (!uploadedFile.value || !dataset.value?.isComplete || readingFile.value) return 'Upload a CSV file and wait for its analysis to finish.';
  if (currentStep === 1) return '';
  if (currentStep === 2) {
    const profile = targetProfile.value;
    if (!project.value.csvColumnToPredict || !profile) return 'Choose the outcome you want to predict.';
    if (profile.missingCount) return 'The target column has missing values. Fill them in or remove the affected observations from the file.';
    if (profile.nonFiniteCount) return 'The target column has nonfinite values. Correct them in the file.';
    if (profile.distinctValues.length < 2 && !profile.distinctValuesTruncated) return 'The target column must contain at least two different values.';
    if (predictionKind.value !== 'survival' && inferredKind.value !== predictionKind.value) return 'The selected outcome type does not match the column values. Choose the suggested type or adjust the values in your file.';
    if (predictionKind.value === 'survival') {
      if (profile.distinctValuesTruncated || profile.distinctValues.length !== 2) return 'The event column must distinguish two states: event observed and event not observed.';
      if (!eventOptions.value.some((option) => option.value === eventChoice.value)) return 'Indicate which value corresponds to an observed event.';
      const timeColumn = project.value.survivalModeldatasetTargetTime;
      const timeProfile = timeColumn ? dataset.value.profiles[timeColumn] : undefined;
      if (!timeColumn || timeColumn === project.value.csvColumnToPredict) return 'Choose a separate column containing the follow-up duration.';
      if (!timeProfile || timeProfile.type !== 'numeric' || timeProfile.missingCount || timeProfile.nonFiniteCount || (timeProfile.min ?? -1) < 0) return 'Follow-up duration must contain only finite, nonnegative numbers, with no missing values.';
    }
    return '';
  }
  if (currentStep === 3) {
    if (repeatedPatients.value === null) return 'Indicate whether multiple rows refer to the same patient.';
    if (repeatedPatients.value && !project.value.rowDepedenciesLinked.length) return 'Choose the column that identifies the same patient.';
    if (project.value.rowDepedenciesLinked.some((column) => !featureColumns.value.includes(column))) return 'Grouping columns must be separate from target columns.';
    if (project.value.rowDepedenciesLinked.some((column) => dataset.value?.profiles[column]?.missingCount)) return 'Fill in missing group identifiers before starting the study.';
    if (repeatedPatients.value && project.value.rowDepedenciesLinked.length === 1
      && dataset.value.profiles[project.value.rowDepedenciesLinked[0]].distinctValues.length < 2) return 'At least two distinct groups are needed to split the observations.';
    return '';
  }
  if (currentStep === 4) {
    if (!predictorColumns.value.length) return 'Keep at least one available feature for prediction.';
    if (predictorColumns.value.some((column) => dataset.value?.profiles[column]?.nonFiniteCount)) return 'Some selected features have nonfinite values. Correct these values in the file or exclude the affected columns.';
    return '';
  }
  if (currentStep === 5) {
    if (evaluationMode.value !== 'external') return '';
    if (readingTestFile.value || !testDataset.value?.isComplete || !testFile.value) return testError.value || 'Upload the test file and wait for its analysis to finish.';
    const profile = testDataset.value.profiles[project.value.csvColumnToPredict];
    if (!profile || profile.missingCount || profile.nonFiniteCount) return 'The expected outcome must be present and finite for every observation in the test file.';
    if (predictionKind.value === 'survival') {
      const timeProfile = testDataset.value.profiles[project.value.survivalModeldatasetTargetTime ?? ''];
      if (!timeProfile || timeProfile.type !== 'numeric' || timeProfile.missingCount || timeProfile.nonFiniteCount || (timeProfile.min ?? -1) < 0) return 'The test file must contain finite, nonnegative follow-up durations, with no missing values.';
      if (profile.distinctValuesTruncated || profile.distinctValues.some((value) => !targetProfile.value?.distinctValues.some((known) => known === value))) return 'The test file uses event values different from those in the training file.';
    } else if (predictionKind.value === 'category' && (profile.distinctValuesTruncated
      || profile.distinctValues.some((value) => !targetProfile.value?.distinctValues.some((known) => known === value)))) return 'The test file contains an outcome category absent from the training file.';
    return '';
  }
  if (currentStep === 6) {
    if (!priorityOptions.value.some((option) => option.value === priority.value)) return 'Choose a priority suitable for your outcome.';
    if (needsPositiveLabel.value && (!binaryTarget.value || !eventOptions.value.some((option) => option.value === positiveChoice.value))) return 'Choose the category for the cases you want to detect.';
    if (!Number.isInteger(searchDuration.value) || searchDuration.value < 60 || searchDuration.value > 3600) return 'Choose a search duration between 1 and 60 minutes.';
    return '';
  }
  if (!project.value.title.trim()) return 'Enter a project name.';
  if (!pipeline.value) return 'DrIA configuration is unavailable. Try again when the service is accessible.';
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
    if (!editedStudy) throw new Error('Correct the advanced configuration before starting training.');
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
    if (!pipeline.value && userStore.isUserConnected) studyError.value = 'DrIA configuration is unavailable. Check the service and try again.';
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
    throw new Error(statisticsBlockedReason.value || 'This data selection is no longer available.');
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
  <section class="guided-project project-creation-colors" :class="{ 'q-mt-lg': dataset }" aria-label="Guided project creation">
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
          <h1 class="text-h5 text-primary q-my-none">Prepare your study</h1>
          <p class="text-grey-7 q-mt-xs q-mb-none">Answer the questions to prepare your study. Your data remains available at every step.</p>
        </div>
        <span class="step-counter text-caption text-grey-7">Step {{ step }} of 7</span>
      </div>
      <nav class="wizard-steps q-mb-md" aria-label="Project creation steps">
        <button
          v-for="(item, index) in steps"
          :key="item.title"
          type="button"
          class="wizard-step"
          :class="{ 'current-step': step === index + 1, 'visited-step': furthestStep > index + 1 }"
          :disabled="index + 1 > furthestStep || busy"
          :aria-current="step === index + 1 ? 'step' : undefined"
          :aria-label="`Step ${index + 1}: ${item.title}`"
          @click="moveTo(index + 1)"
        >
          <span class="step-number"><q-icon v-if="furthestStep > index + 1 && step !== index + 1" name="check" size="16px" /><template v-else>{{ index + 1 }}</template></span>
          <span class="step-name">{{ item.title }}</span>
        </button>
      </nav>
      <q-linear-progress :value="step / 7" color="accent" size="4px" rounded class="q-mb-lg" aria-label="Questionnaire progress" />

      <div class="creation-layout">
        <div ref="questionCard" class="question-panel">
          <q-card flat bordered class="question-card">
            <div class="question-guidance">
              <span class="question-guidance-icon"><q-icon :name="steps[step - 1].icon" size="23px" aria-hidden="true" /></span>
              <div>
                <div class="question-guidance-title">{{ !dataset?.isComplete ? 'Reading your file' : step === 2 && !project.csvColumnToPredict ? 'Start here' : 'Your next step' }}</div>
                <div class="question-guidance-step">{{ steps[step - 1].title }} · Step {{ step }} of 7</div>
              </div>
            </div>
            <q-form ref="questionForm" class="q-pa-lg" @submit.prevent="next">
              <template v-if="step === 1">
                <h2 class="question-title" tabindex="-1">Which file contains your observations?</h2>
                <p class="question-help">Upload a CSV file with one observation per row and a name for each column.</p>
                <q-file v-model="uploadedFile" filled clearable color="primary" label="Upload my data" accept=".csv,text/csv" :max-file-size="100 * 1024 * 1024" :disable="busy" @rejected="error = 'The file must not exceed 100 MB.'">
                  <template #prepend><q-icon name="upload_file" /></template>
                </q-file>
                <div v-if="readingFile" class="row items-center text-caption text-grey-7 q-mt-md" role="status"><q-spinner color="primary" size="20px" class="q-mr-sm" />Analyzing all observations…</div>
                <div v-else-if="dataset?.isComplete" class="file-success q-mt-md" role="status"><q-icon name="check_circle" color="secondary" class="q-mr-xs" /> {{ number.format(dataset.rowCount) }} observations ready to explore.</div>
                <p class="question-note q-mt-md">The table appears as soon as the first rows are read. It will update as you answer the questions.</p>
              </template>

              <template v-else-if="step === 2">
                <h2 class="question-title" tabindex="-1">What outcome would you like to predict?</h2>
                <p class="question-help">Choose its column from the list or directly in the table.</p>
                <q-select v-model="project.csvColumnToPredict" :options="columns" filled label="Target column" :disable="busy" popup-content-class="project-creation-colors" />
                <template v-if="project.csvColumnToPredict">
                  <div v-if="inferredKind && !survivalAnalysis" class="answer-feedback q-mt-md">
                    <strong>{{ inferredKind === 'category' ? 'Predicting a category' : 'Predicting a quantity' }}</strong>
                    <div>{{ inferredKind === 'category' ? 'DrIA will learn to identify the categories in this column.' : 'DrIA will learn to estimate the numerical value in this column.' }} The type is detected from the values in the file.</div>
                  </div>
                  <template v-if="survivalEligible">
                    <q-toggle v-model="survivalAnalysis" color="primary" label="Study this event over time (survival analysis)" :disable="busy" class="q-mt-md" />
                    <p class="question-note q-mt-xs">Enable this if the two values indicate whether an event was observed, such as a relapse. You will choose the follow-up duration from another column.</p>
                  </template>
                  <template v-if="predictionKind === 'survival'">
                    <p class="question-note q-mt-md">The selected column indicates whether the event was observed. The other value means it was not observed during follow-up.</p>
                    <q-select v-model="eventChoice" :options="eventOptions" filled emit-value map-options label="Which value means “event observed”?" :disable="busy" class="q-mt-md" popup-content-class="project-creation-colors" />
                    <q-select v-model="project.survivalModeldatasetTargetTime" :options="columns.filter(column => column !== project.csvColumnToPredict)" filled label="Which column contains the follow-up duration?" :disable="busy" class="q-mt-md" popup-content-class="project-creation-colors" />
                    <q-select v-model="timeUnit" :options="['days', 'weeks', 'months', 'years', 'hours']" filled clearable label="Unit displayed in the summary (optional)" :disable="busy" class="q-mt-md" popup-content-class="project-creation-colors" />
                    <p class="question-note q-mt-sm">Durations are used as stored in the file. The unit helps read the summary and does not convert values.</p>
                  </template>
                </template>
              </template>

              <template v-else-if="step === 3">
                <h2 class="question-title" tabindex="-1">Do multiple rows refer to the same patient?</h2>
                <p class="question-help">If a patient appears more than once, their rows must stay together. This avoids testing the model on a patient already seen during training.</p>
                <q-option-group v-model="repeatedPatients" :options="[{ label: 'Yes', value: true }, { label: 'No', value: false }]" inline color="primary" :disable="busy" />
                <template v-if="repeatedPatients">
                  <q-select v-model="project.rowDepedenciesLinked" :options="featureColumns" multiple use-chips filled label="Which column identifies the same patient?" :disable="busy" class="q-mt-md" popup-content-class="project-creation-colors" />
                  <p class="question-note q-mt-sm">Their observations will stay together when the data is split. This identifier will be excluded from the features used for prediction.</p>
                  <p class="question-note">If the identifier depends on multiple columns, select all of them. Their combination will identify a group.</p>
                </template>
                <p v-else-if="repeatedPatients === false" class="question-note q-mt-md">DrIA can assign rows independently to training and evaluation sets.</p>
              </template>

              <template v-else-if="step === 4">
                <h2 class="question-title" tabindex="-1">Which columns should be excluded from training?</h2>
                <p class="question-help">Remove identifiers and information unavailable at prediction time. For a prediction at admission, a result obtained at discharge comes too late.</p>
                <q-btn-toggle v-model="exclusionMode" no-caps unelevated color="grey-2" text-color="grey-8" toggle-color="primary" :options="[{ label: 'Identifiers', value: 'identifier' }, { label: 'Unavailable information', value: 'unavailable' }]" :disable="busy" class="exclusion-toggle q-mt-sm" />
                <p class="question-note q-mt-sm">{{ exclusionMode === 'identifier' ? 'For example: a record number or patient name. Grouping columns are already excluded from the features.' : 'For example: a test performed after the decision, or information that already reveals the outcome to predict.' }}</p>
                <q-select v-model="selectedExclusions" :options="selectableFeatures" multiple use-chips filled :label="exclusionMode === 'identifier' ? 'Columns containing identifiers' : 'Information available too late'" :disable="busy" class="q-mt-md" popup-content-class="project-creation-colors" />
                <p class="question-note q-mt-sm">You can also click columns in the table. Click again to undo their exclusion.</p>
                <div v-if="Object.keys(exclusionReasons).length" class="excluded-list q-mt-md">
                  <div v-for="(reason, column) in exclusionReasons" :key="column" class="excluded-item">
                    <span><strong>{{ column }}</strong><small>{{ reason === 'identifier' ? 'Identifier' : 'Unavailable at prediction time' }}</small></span>
                    <q-btn flat round dense size="sm" icon="close" color="grey-7" :aria-label="`Use ${column} again`" :disable="busy" @click="exclusionReasons = Object.fromEntries(Object.entries(exclusionReasons).filter(([name]) => name !== column))" />
                  </div>
                </div>
                <div class="file-success q-mt-md">{{ predictorColumns.length }} feature{{ predictorColumns.length > 1 ? 's' : '' }} kept for prediction.</div>
              </template>

              <template v-else-if="step === 5">
                <h2 class="question-title" tabindex="-1">Which data should be reserved for the final test?</h2>
                <p class="question-help">A final test measures performance on data kept separate from the model search.</p>
                <div class="answer-options">
                  <label class="answer-option" :class="{ selected: evaluationMode === 'split' }">
                    <q-radio v-model="evaluationMode" val="split" color="primary" :disable="busy" label="Automatically reserve 20% of the data" />
                    <span>DrIA uses 80% of the data to search for a model and keeps 20% for the final test.<q-badge outline color="secondary" class="q-ml-xs">Recommended</q-badge></span>
                  </label>
                  <label class="answer-option" :class="{ selected: evaluationMode === 'external' }">
                    <q-radio v-model="evaluationMode" val="external" color="primary" :disable="busy" label="Use another file for the test" />
                    <span>An independent file containing the same columns and expected outcomes.</span>
                  </label>
                  <label class="answer-option" :class="{ selected: evaluationMode === 'cross_validation' }">
                    <q-radio v-model="evaluationMode" val="cross_validation" color="primary" :disable="busy" label="Use no separate test set" />
                    <span>All data remains available for the search. Scores come from the internal validation defined in the settings.</span>
                  </label>
                </div>
                <p v-if="repeatedPatients" class="question-note q-mt-md">Observations from the same patient stay together when the data is split.</p>
                <template v-if="evaluationMode === 'external'">
                  <q-file v-model="testFile" filled clearable label="Upload the test file" accept=".csv,text/csv" :max-file-size="100 * 1024 * 1024" :disable="busy" class="q-mt-md" @rejected="testError = 'The test file must not exceed 100 MB.'">
                    <template #prepend><q-icon name="upload_file" /></template>
                  </q-file>
                  <div v-if="readingTestFile" class="row items-center text-caption text-grey-7 q-mt-sm" role="status"><q-spinner color="primary" size="18px" class="q-mr-sm" />Analyzing the test file…</div>
                  <div v-if="testError" class="text-caption text-negative q-mt-sm" role="alert">{{ testError }}</div>
                  <div v-if="testDataset" class="file-success q-mt-sm">{{ number.format(testDataset.rowCount) }} observations available for testing.</div>
                </template>
              </template>

              <template v-else-if="step === 6">
                <h2 class="question-title" tabindex="-1">How should the model search be guided?</h2>
                <p class="question-help">Set {{ hasPriorityChoice ? 'the metric to prioritize and ' : '' }}the maximum time for the search.</p>
                <div v-if="hasPriorityChoice" class="field-label q-mb-sm">{{ predictionKind === 'quantity' ? 'How would you like to evaluate prediction errors?' : 'Which error would you like to reduce first?' }}</div>
                <p v-if="predictionKind === 'quantity'" class="question-note q-mb-sm">An error is the difference between the predicted and actual value, in the units of your data.</p>
                <div v-if="hasPriorityChoice" class="answer-options">
                  <label v-for="option in priorityOptions" :key="option.value" class="answer-option" :class="{ selected: priority === option.value }">
                    <q-radio v-model="priority" :val="option.value" color="primary" :disable="busy" :label="option.label" />
                    <span>{{ option.detail }}<q-badge v-if="option.value === 'recommended'" outline color="secondary" class="q-ml-xs">Recommended</q-badge></span>
                  </label>
                </div>
                <div v-else class="answer-feedback">
                  <strong>Automatic metric: {{ priorityOptions[0].label }}</strong>
                  <div>{{ priorityOptions[0].detail }}</div>
                </div>
                <q-select v-if="needsPositiveLabel" v-model="positiveChoice" :options="eventOptions" filled emit-value map-options label="Which category represents the cases to detect?" :disable="busy" class="q-mt-md" popup-content-class="project-creation-colors" />
                <p v-if="hasPriorityChoice" class="question-note q-mt-sm">This metric is used to select the model during the search. Other scores remain available in the results.</p>
                <q-select v-model="searchDuration" filled emit-value map-options label="Maximum model search duration" :options="[{ label: '1 minute - quick trial', value: 60 }, { label: '5 minutes', value: 300 }, { label: '15 minutes', value: 900 }, { label: '30 minutes', value: 1800 }, { label: '60 minutes', value: 3600 }]" :disable="busy" class="q-mt-lg" popup-content-class="project-creation-colors" />
                <p class="question-note q-mt-sm">More time allows more settings to be explored. Final evaluation may continue after the search ends.</p>
              </template>

              <template v-else>
                <h2 class="question-title" tabindex="-1">What DrIA will learn</h2>
                <p class="question-help">Check your objective, then start the search.</p>
                <dl class="study-summary">
                  <div><dt>Target</dt><dd>{{ project.csvColumnToPredict }} · {{ predictionLabel }}</dd></div>
                  <div v-if="predictionKind === 'survival'"><dt>Follow-up</dt><dd>{{ project.survivalModeldatasetTargetTime }}<template v-if="timeUnit"> in {{ timeUnit }}</template><br>Event observed: {{ eventOptions.find(option => option.value === eventChoice)?.label }}</dd></div>
                  <div><dt>Observations</dt><dd>{{ number.format(dataset?.rowCount ?? 0) }} rows<br><template v-if="repeatedPatients">Grouping: {{ project.rowDepedenciesLinked.join(', ') }}</template><template v-else>Rows assigned independently</template></dd></div>
                  <div><dt>Features</dt><dd>{{ predictorColumns.length }} selected, {{ Object.keys(exclusionReasons).length }} excluded</dd></div>
                  <div><dt>Evaluation</dt><dd>{{ evaluationLabel }}</dd></div>
                  <div><dt>Priority</dt><dd>{{ priorityOptions.find(option => option.value === priority)?.label }}<template v-if="needsPositiveLabel"><br>Cases to detect: {{ eventOptions.find(option => option.value === positiveChoice)?.label }}</template></dd></div>
                  <div><dt>Search</dt><dd>{{ searchDuration / 60 }} minute{{ searchDuration > 60 ? 's' : '' }} maximum</dd></div>
                </dl>
                <q-input v-model="project.title" filled label="Project name" :maxlength="100" :rules="[value => Boolean(value?.trim()) || 'Enter a project name.']" :disable="busy" />
                <q-input v-model="project.description" filled autogrow label="Description (optional)" :maxlength="2000" :disable="busy" class="q-mt-sm" />
                <p class="question-note q-mt-md">Preprocessing, algorithms, and validation settings are suggested automatically. You can adjust them in the advanced settings below.</p>
                <div v-if="studyError" class="text-caption text-negative q-mt-md" role="alert">{{ studyError }}<q-btn flat no-caps dense color="primary" label="Try again" :loading="loadingStudy" @click="loadStudy" /></div>
                <div v-if="loadingStudy" class="row items-center text-caption text-grey-7 q-mt-sm"><q-spinner size="18px" color="primary" class="q-mr-sm" />Preparing the study…</div>
              </template>

              <div v-if="feedback" class="answer-feedback q-mt-md" role="status">{{ feedback }}</div>
              <div v-if="error" class="form-error q-mt-md" role="alert"><q-icon name="error_outline" size="18px" class="q-mr-xs" />{{ error }}</div>
              <div class="wizard-actions q-mt-lg">
                <q-btn v-if="step > 1" flat no-caps color="primary" icon="arrow_back" label="Back" :disable="busy" @click="moveTo(step - 1)" />
                <q-btn unelevated no-caps color="primary" :icon-right="step < 7 ? 'arrow_forward' : 'play_arrow'" :label="step < 7 ? 'Continue' : 'Start training'" type="submit" :loading="busy" :disable="readingFile || readingTestFile || (step === 7 && (!pipeline || loadingStudy))" />
              </div>
            </q-form>
          </q-card>
        </div>

        <div class="data-panel">
          <div v-if="testDataset" class="row items-center justify-between q-mb-sm q-col-gutter-sm">
            <div class="col-auto">
              <q-btn-toggle v-model="previewSource" no-caps unelevated rounded color="grey-2" text-color="grey-8" toggle-color="primary" :options="[{ label: 'Training', value: 'train' }, { label: 'Independent test set', value: 'test' }]" :disable="busy" size="sm" />
            </div>
            <div class="col text-right text-caption text-grey-7">{{ previewSource === 'test' ? testFile?.name : uploadedFile?.name }}</div>
          </div>
          <q-tabs v-model="dataView" dense no-caps align="justify" active-color="primary" indicator-color="accent" class="data-view-tabs q-mb-sm" aria-label="Explore the data">
            <q-tab name="preview" icon="table_chart" label="Your data" />
            <q-tab name="summary" icon="analytics" label="Data summary" />
            <q-tab name="plots" icon="query_stats" label="Charts" />
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
        <q-expansion-item v-model="showAdvanced" icon="settings" label="Advanced settings (optional)" caption="Preprocessing, algorithms, validation, and analyses" :disable="busy" @before-show="advancedOpened = true">
          <div class="q-pa-md" :inert="busy ? '' : undefined">
            <p class="text-caption text-grey-7">The primary metric and search duration remain those from the “Search” step, even if you edit the full configuration.</p>
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
