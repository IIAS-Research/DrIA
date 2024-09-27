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

// Pipeline
export interface IStep {
  step: string;
  name: string;
  description: string;
  configuration: {
    [key: string]: {
      default: any;
      value: any;
      description: string;
    };
  };
  children: [];
}

export interface IMetaStep {
  step: string;
  name: string;
  description: string;
  configuration: {
    [key: string]: {
      default: any;
      value: any;
      description: string;
    };
  };
  children: IStep[];
}

export interface IMetaOrderedStep {
  step: string;
  name: string;
  description: string;
  configuration: {
    [key: string]: {
      default: any;
      value: any;
      description: string;
    };
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
