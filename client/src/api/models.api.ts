import { api } from 'src/boot/axios';
import { ICsvProject, IModel } from 'src/components/models';
import { notify } from 'src/utils/notify';

const modelUrl = 'Models';

export async function getModeleById(id: number): Promise<IModel | undefined> {
  try {
    const response = (await api.get(`${modelUrl}/${id}`))?.data;
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
    const response = (await api.get(`${modelUrl}/${id}/explanation`))?.data;
    return response;
  } catch (error) {
    console.error(error);
    notify('negative', 'Une erreur est survenue');
    return undefined;
  }
}

export async function predictValueUsingAiModel(modelId: number, dataTable: ICsvProject): Promise<ICsvProject | undefined> {
  try {
    const response = (await api.post(`${modelUrl}/${modelId}/predict`, dataTable))?.data;
    return response;
  } catch (error) {
    console.error(error);
    notify('negative', 'Une erreur est survenue');
    return undefined;
  }
}
