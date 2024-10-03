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

export interface ICreateProjectResponseDTO {
  id: string;
  status:
    | 'QUEUED'
    | 'RUNNING'
    | 'FINISHED'
    | 'CANCELED'
    | 'CRASHED'
    | 'TIMEOUT';
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
  can_be_disable: boolean;
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

export interface IMetrics {
  metricsInfos: {
    [key: string]: {
      name: string;
      description: string;
    };
  };
  metricsTrain: IModelAccuracyData;
  metricsTest: IModelAccuracyData | null;
  mainMetric: string;
}

export interface IStructuredBibliography {
  step: string;
  year: number;
  name: string;
  author: string[];
  doi: string | null;
  publisher: string | null;
}
export interface IModel {
  iaRemoteId: string;
  id: number;
  metrics: IMetrics;
  bibliography?: string; // Only for model slug page
  structuredBibliography?: IStructuredBibliography; // Only for model slug page
}

type PipelineStatus =
  | 'QUEUED'
  | 'RUNNING'
  | 'FINISHED'
  | 'CANCELED'
  | 'CRASHED'
  | 'TIMEOUT';
export interface IProcess {
  iaRemoteId: string;
  id: number;
  date: Date;
  duration: number;
  maxDuration: number;
  pipeline: IPipeline;
  status: PipelineStatus;
  bestTrainAccuracy: number;
  bestTestAccuracy: number | null;
  // mainMetricsName?: string;
  models: IModel[];
  projectId: IProject['id'];
}
