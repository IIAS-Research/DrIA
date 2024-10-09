import { api } from 'src/boot/axios';
import { IPipeline } from 'src/components/models';
import { notify } from 'src/utils/notify';

export async function getDefaultPipeline(): Promise<IPipeline | undefined> {
  try {
    const response = (await api.get('IaWebService/pipeline'))?.data;
    return response;
  } catch (error) {
    console.error(error);
    notify('negative', String(error));
    return undefined;
  }
}
