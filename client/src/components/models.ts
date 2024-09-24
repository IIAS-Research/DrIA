export interface IUser {
    id: number;
    email: string
    firstName: string
    lastName: string
    isAdmin: boolean;
}

export interface ICreateUserDTO {
    email: string
    firstName: string
    lastName: string
    isAdmin: boolean;
}
