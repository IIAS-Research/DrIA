import { api } from 'src/boot/axios';
import { IModel, IExplanation, IPrediction } from 'src/components/models';
import { notifyApiError } from 'src/utils/iaml';
const modelUrl = 'Models';
export async function getModeleById(id: number): Promise<IModel | undefined> {
  try {
    return (await api.get(`${modelUrl}/${id}`)).data;
  }
  catch (e) {
    notifyApiError(e);
    return undefined;
  }
}
export async function startModeleExplanationById(id: number): Promise<any> {
  return (await api.get(`${modelUrl}/${id}/explanation`)).data;
}
export async function getExplanationStatus(id: number): Promise<any> {
  return (await api.get(`${modelUrl}/${id}/explanation/status`)).data;
}
export async function getExplanationResults(id: number): Promise<IExplanation> {
  return (await api.get(`${modelUrl}/${id}/explanation/results`)).data;
}
export async function getExplanationPlots(id: number, params: Record<string, any>): Promise<any> {
  return (await api.get(`${modelUrl}/${id}/explanation/plots`, { params })).data;
}
export async function predictValueUsingAiModel(modelId: number, request: {
  json_row: Record<string, any>[];
  method: string;
  target?: any;
  times?: any;
}, signal?: AbortSignal): Promise<IPrediction> {
  return (await api.post(`${modelUrl}/${modelId}/predict`, request, { signal })).data;
}
