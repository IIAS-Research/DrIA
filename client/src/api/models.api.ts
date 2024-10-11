import { api } from 'src/boot/axios';
import { IModel, ProcessStatus, ICsvProject } from 'src/components/models';
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

interface ICreateProjectResponseDTO {
  id: string;
  status: ProcessStatus;
  socket: string;
}

export async function startModeleExplanationById(
  id: number,
): Promise<ICreateProjectResponseDTO | undefined> {
  try {
    const response = (await api.get(`${modelUrl}/${id}/explanation`))?.data;
    return response;
  } catch (error) {
    console.error(error);
    notify('negative', 'Une erreur est survenue');
    return undefined;
  }
}

export async function predictValueUsingAiModel(
  modelId: number,
  dataTable: ICsvProject,
): Promise<ICsvProject | undefined> {
  try {
    const response = (
      await api.post(`${modelUrl}/${modelId}/predict`, dataTable)
    )?.data;
    return response;
  } catch (error) {
    console.error(error);
    notify('negative', 'Une erreur est survenue');
    return undefined;
  }
}
