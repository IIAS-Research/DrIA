import { api } from 'src/boot/axios';
import { ICreateUserDTO, IUser } from 'src/components/models';
import { notify } from 'src/utils/notify';
import { notifyApiError } from 'src/utils/iaml';

const defaultMessageForSuccessServerInteraction = 'Your changes have been saved';

export async function findAllUsers(): Promise<IUser[] | undefined> {
  try {
    const response = (await api.get('Users'))?.data;
    return response;
  } catch (error) {
    console.error('Error fetching users:', error);
    notifyApiError(error);
    return undefined;
  }
}

export async function createOneUser(user: ICreateUserDTO): Promise<IUser | undefined> {
  try {
    const response = (await api.post('Auth/register', user))?.data;
    notify('positive', defaultMessageForSuccessServerInteraction);
    return response;
  } catch (error) {
    console.error('Error fetching users:', error);
    notifyApiError(error);
    return undefined;
  }
}

export async function updateOneUser(id: IUser['id'], user: IUser): Promise<IUser | undefined> {
  try {
    const response = (await api.patch(`Users/${id}`, user))?.data;
    notify('positive', defaultMessageForSuccessServerInteraction);
    return response;
  } catch (error) {
    console.error('Error fetching users:', error);
    notifyApiError(error);
    return undefined;
  }
}

export async function deleteOneUser(id: IUser['id']): Promise<void | undefined> {
  try {
    await api.delete(`Users/${id}`);
    notify('positive', defaultMessageForSuccessServerInteraction);
  } catch (error) {
    console.error('Error fetching users:', error);
    notifyApiError(error);
  }
}
