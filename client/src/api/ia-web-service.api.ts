import { api } from 'src/boot/axios';
import { IPipeline } from 'src/components/models';
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
