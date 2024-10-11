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

export type ProcessStatus =
  | 'QUEUED'
  | 'RUNNING'
  | 'FINISHED'
  | 'CANCELED'
  | 'CRASHED'
  | 'TIMEOUT';

export interface ICreateProjectResponseDTO {
  id: string;
  status: ProcessStatus;
  socket: string;
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
export interface IPipeline {
  step: string;
  name: string;
  description: string;
  enable: boolean;
  canBeDisable: boolean;
  configuration: {
    [key: string]: IConfigurationKey;
  };
  children: IPipeline[];
}
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
  createdAt?: string;
  updatedAt?: string;
  userId: IUser['id'];
  // eslint-disable-next-line no-use-before-define
  processes: IProcess[];
}

export interface IColumn {
  label: string;
  name: string; // -> Label parameterized
  isColToPredict: boolean;
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

export interface IMetricInfo {
  name: string;
  desc: string;
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
  metrics_info: { [name: string]: IMetricInfo };
  model_info: { [name:string]: IModelInfo };
  main_metrics: string;
  metrics_train: { [name: string]: number };
  metrics_test: { [name: string]: number } | undefined;
  plots: IPlot[];
}

export interface IStructuredBibliography {
  step: string;
  year: number;
  name: string;
  author: string[];
  doi: string | null;
  publisher: string | null;
}

export interface IExplanation {
  plots: { [keyPlot: string]: {
    title: string;
    description: string;
    image: string;
  }};
  features: {
    name: string;
    value: number;
  }[]
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
  iaRemoteId: number;
  id: number;
  date: Date;
  duration: number;
  maxDuration: number;
  pipeline: IPipeline;
  status: ProcessStatus;
  bestTrainAccuracy: number;
  bestTestAccuracy: number | null;
  // mainMetricsName?: string;
  models: IModel[];
  projectId: IProject['id'];
}

export interface IRunPipelineForkDto {
  projectId: number;
  pipeline: IPipeline;
}
