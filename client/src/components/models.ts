export interface IUser {
  id: number;
  email: string;
  firstName: string;
  lastName: string;
  isAdmin: boolean;
}

export interface IProcess {
  id: number;
  iaWebServiceJobId?: string;
}
// export interface IProcess {
//   id: number;
//   iaWebServiceJobId?: string;
// }
export interface ICreateUserDTO {
  email: string;
  firstName: string;
  lastName: string;
  isAdmin: boolean;
}

export interface ICreateProjectResponseDTO {
  id: string;
  status: 'QUEUED' | 'RUNNING' | 'FINISHED' | 'CANCELED' | 'CRASHED' | 'TIMEOUT';
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
  createdAt?: string,
  updatedAt?: string,
  userId: IUser['id']
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

type PipelineStatus = 'QUEUED' | 'RUNNING' | 'FINISHED' | 'CANCELED' | 'CRASHED' | 'TIMEOUT';
export interface IProcess {
  id: number;
  date: Date;
  Duration: number;
  Pipeline: IPipeline;
  status: PipelineStatus;
  ProjectId: IProject['id'];
}
