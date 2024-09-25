export interface IUser {
  id: string;
  email: string;
  firstName: string;
  lastName: string;
  isAdmin: boolean;
}
export interface IProjectForm {
  title: string;
  description: string;
  csvColumnToPredict: string;
  rowDepedencies: boolean;
  rowDepedenciesLinked: string[];
  datasetHasIdColumn: boolean;
  datasetHasIdColumnList: string[];
}
export interface ICreateUserDTO {
  email: string;
  firstName: string;
  lastName: string;
  isAdmin: boolean;
}
