export interface IUser {
  id: number;
  email: string;
  firstName: string;
  lastName: string;
  isAdmin: boolean;
}
export interface ICreateUserDTO {
  email: string;
  firstName: string;
  lastName: string;
  isAdmin: boolean;
}
export type ProcessStatus = 'QUEUED' | 'RUNNING' | 'FINISHED' | 'CANCELED' | 'CRASHED' | 'TIMEOUT' | 'CACHED';
export interface ICreateProjectResponseDTO {
  id: string;
  status: ProcessStatus;
  socket: string | null;
  projectId: number;
}
export interface IConfigurationKey {
  default: any;
  value: any;
  description: string;
  range?: number[];
  categorical?: string[];
}
// Pipeline
export interface IRecipe {
  type: string;
  component?: string;
  alias?: string;
  family?: string;
  kind?: string;
  params?: Record<string, any>;
  children?: IRecipe[];
  recipe?: IRecipe;
  [key: string]: any;
}
export interface IStudy {
  schema_version: 1;
  pipeline?: IRecipe;
  metrics?: IRecipe;
  statistics?: IRecipe;
  explanations?: IRecipe;
  options?: Record<string, any>;
  fit_options?: Record<string, any>;
}
// Dr IA keeps the historical field name at its API boundary.
export type IPipeline = IStudy;
export interface IProjectForm {
  title: string;
  description: string;
  csvColumnToPredict: string;
  rowDepedencies: boolean;
  rowDepedenciesLinked: string[];
  datasetHasIdColumn: boolean;
  datasetHasIdColumnList: string[];
  pipeline?: IPipeline;
  file?: string;
  survivalModel: boolean;
  survivalModeldatasetTargetTime?: string;
  survivalModelEventValue?: string;
}
export interface IMetricInfo {
  name: string;
  desc: string;
  greater_is_better?: boolean;
}
export interface IBaselineMetrics {
  id?: string;
  status?: {
    current: ProcessStatus;
  };
  metrics: IMetrics[];
  error?: string | null;
}
export interface IProject {
  id: number;
  title: string;
  description: string;
  csvColumnToPredict: string;
  rowDepedencies: boolean;
  rowDepedenciesLinked: string[];
  datasetHasIdColumn: boolean;
  datasetHasIdColumnList: string[];
  survivalModel?: boolean;
  survivalModelDatasetTargetTime?: string | null;
  survivalModelEventValue?: string | null;
  createdAt?: string;
  updatedAt?: string;
  userId: IUser['id'];
  // eslint-disable-next-line no-use-before-define
  processes: IProcess[];
  metricsBaseline?: IBaselineMetrics;
}
export interface IColumn {
  label: string;
  name: string; // -> Label parameterized
  isColToPredict: boolean;
  isGroupColumn?: boolean;
  isIdColumn?: boolean;
}
export interface IRow {
  [key: string]: any;
}
export interface ICsvProject {
  columns: IColumn[];
  rows: IRow[];
}
export interface IModelAccuracyData {
  [key: string]: number;
}
export interface IModelInfo {
  name: string;
  description: string;
  description_long: string;
}
export interface IPlot {
  title: string;
  description: string;
  image: string;
}
export interface IMetrics {
  id: number;
  metrics_info: {
    [name: string]: IMetricInfo;
  };
  model_info: IModelInfo;
  main_metric?: string;
  main_metrics?: string;
  objective?: {
    key: string;
    greater_is_better: boolean;
  } | null;
  score_source?: 'cross_validation' | 'in_sample';
  metrics_train: {
    [name: string]: number | null;
  };
  metrics_test?: {
    [name: string]: number | null;
  } | null;
  plots?: IPlot[] | null;
  metric_report?: any;
  evaluation_report?: any;
  explanation_report?: any;
  training_audit?: any;
}
export interface IStructuredBibliography {
  step: string;
  year: number;
  name: string;
  authors: string[];
  doi: string | null;
  publisher: string | null;
}
export interface IExplanation {
  plots: {
    [keyPlot: string]: {
      title: string;
      description: string;
      image: string;
    };
  };
  features: {
    name: string;
    value: number | number[];
  }[];
  results?: Record<string, IAnalysis>;
  report?: any;
  configured_report?: any;
  status?: {
    current: string;
  } | string | null;
  error?: string | null;
  cache_error?: string | null;
}
export interface IAnalysis {
  kind: 'shap' | 'plot' | 'data' | string;
  features?: Record<string, number | number[]>;
  title?: string;
  description?: string;
  image?: string;
  value?: any;
  [key: string]: any;
}
export interface IPrediction {
  status: string;
  method: string;
  prediction: any;
  classes?: any[];
}
export interface IStatistics {
  schema_version: number;
  statistics: {
    columns: any[];
    index: any[];
    data: any[][];
  };
  plots?: IPlot[];
  reports?: any;
}
export interface IBibliography {
  id: string;
  rid: number;
  bibliography: string;
  structured_bibliography: IStructuredBibliography[];
}
export interface IModel {
  id: number;
  date: string;
  metrics: IMetrics;
  explanation: string;
  textExplanation: string[];
  bibliography?: IBibliography;
}
export interface IProcess {
  iaRemoteId: string;
  id: number;
  date: Date;
  duration: number;
  maxDuration: number;
  pipeline: IPipeline;
  status: ProcessStatus;
  bestTrainAccuracy: number | null;
  bestTestAccuracy: number | null;
  bestTrainMetric?: number | null;
  bestTestMetric?: number | null;
  objective?: {
    key: string;
    greater_is_better: boolean;
  } | null;
  scoreSource?: string;
  chosenCandidate?: number | null;
  error?: string | null;
  cacheError?: string | null;
  statusSnapshot?: {
    results_persisted?: boolean;
    objective?: {
      key: string;
      greater_is_better: boolean;
    } | null;
    best_train_metric?: number | null;
    best_test_metric?: number | null;
    error?: string | null;
    cache_error?: string | null;
    chosen_candidate?: number | null;
    score_source?: string;
    logs?: any;
  } | null;
  // mainMetricsName?: string;
  models: IModel[];
  projectId: IProject['id'];
}
export interface IRunPipelineForkDto {
  projectId: number;
  pipeline: IPipeline;
  maxDuration: number;
}
