import { api } from 'src/boot/axios';
import { IModel } from 'src/components/models';
import { notify } from 'src/utils/notify';

const projectUrl = 'Models';

export async function getModeleById(id: number): Promise<IModel | undefined> {
  try {
    const response = (await api.get(`${projectUrl}/${id}`))?.data;
    return response;
  } catch (error) {
    console.error(error);
    notify('negative', 'Une erreur est survenue');
    return undefined;
  }
}

export async function getModeleExplanationById(
  id: number,
): Promise<IModel['explanation'] | undefined> {
  try {
    const response = (await api.get(`${projectUrl}/${id}/explanation`))?.data;
    return response;
  } catch (error) {
    console.error(error);
    notify('negative', 'Une erreur est survenue');
    return undefined;
  }
}
