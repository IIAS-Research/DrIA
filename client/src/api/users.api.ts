import { api } from 'src/boot/axios';
import { IUser } from 'src/components/models';
import { notify } from 'src/utils/notify';

const defaultMessageForSuccessServerInteraction = 'Vos modifications ont biens été prises en compte';

export async function findAllUsers(): Promise<IUser[] | undefined> {
  try {
    const response = (await api.get('users'))?.data;
    return response;
  } catch (error) {
    console.error('Error fetching users:', error);
    notify('negative', String(error));
    return undefined;
  }
}

export async function updateOneUser(id: IUser['id'], user: IUser): Promise<IUser | undefined> {
  try {
    const response = (await api.patch(`users/${id}`, user))?.data;
    notify('positive', defaultMessageForSuccessServerInteraction);
    return response;
  } catch (error) {
    console.error('Error fetching users:', error);
    notify('negative', String(error));
    return undefined;
  }
}

export async function deleteOneUser(id: IUser['id']): Promise<void | undefined> {
  try {
    await api.delete(`users/${id}`);
    notify('positive', defaultMessageForSuccessServerInteraction);
  } catch (error) {
    console.error('Error fetching users:', error);
    notify('negative', String(error));
  }
}
