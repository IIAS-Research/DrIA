export interface IUser {
  id: number;
  email: string;
  firstName: string;
  lastName: string;
  isAdmin: boolean;
}
export interface IProcess {
  id: number;
}
export interface ICreateUserDTO {
  email: string;
  firstName: string;
  lastName: string;
  isAdmin: boolean;
}

export interface IConfigurationKey {
  default: any;
  value: any;
  description: string;
  range?: number[];
  categorical?: string[];
}

// Pipeline
export interface IStep {
  step: string;
  name: string;
  description: string;
  enable: boolean;
  can_be_disable: boolean;
  configuration: {
    [key: string]: IConfigurationKey;
  };
  children: [];
}

export interface IMetaStep {
  step: string;
  name: string;
  description: string;
  enable: boolean;
  configuration: {
    [key: string]: IConfigurationKey;
  };
  children: IStep[];
}

export interface IMetaOrderedStep {
  step: string;
  name: string;
  description: string;
  can_be_disable: boolean;
  configuration: {
    [key: string]: IConfigurationKey;
  };
  children: IMetaStep[];
}
export interface IProjectForm {
  title: string;
  description: string;
  csvColumnToPredict: string;
  rowDepedencies: boolean;
  rowDepedenciesLinked: string[];
  datasetHasIdColumn: boolean;
  datasetHasIdColumnList: string[];
  pipeline?: IMetaOrderedStep;
  file?: string;
}
