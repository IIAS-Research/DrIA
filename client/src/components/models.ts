export interface IUser {
  id: string;
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
    }
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
    }
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
    }
  };
  children: IMetaStep[];
}
