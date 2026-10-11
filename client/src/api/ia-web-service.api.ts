import { api } from 'src/boot/axios';
import { IPipeline, IStatistics } from 'src/components/models';
import { notifyApiError } from 'src/utils/iaml';
import { isSessionExpired } from 'src/utils/session';
export async function getDefaultPipeline(): Promise<IPipeline | undefined> {
  try {
    const response = (await api.get('IaWebService/pipeline'))?.data;
    return response;
  }
  catch (error) {
    if (isSessionExpired(error)) return undefined;
    console.error(error);
    notifyApiError(error);
    return undefined;
  }
}
export async function getCatalogue(): Promise<any> {
  return (await api.get('IaWebService/catalogue')).data;
}
export async function validateStudy(configuration: IPipeline): Promise<{
  configuration: IPipeline;
  description: any;
}> {
  return (await api.post('IaWebService/validate', { configuration })).data;
}
export async function exploreStatistics(body: FormData, plots = true, signal?: AbortSignal): Promise<IStatistics> {
  return (await api.post(`IaWebService/statistics?plots=${plots}`, body, { signal })).data;
}
