import { api } from 'src/boot/axios';
import { notifyApiError } from 'src/utils/iaml';

const adminUrl = 'Admin';

export async function getPullingData(): Promise<IPullingData | undefined> {
  try {
    const response = (await api.get(`${adminUrl}/PullingData`))?.data;
    return response;
  } catch (error) {
    console.error('Error fetching pulling data:', error);
    notifyApiError(error);
  }
}

export async function stopPullingConnection(jobServiceId: string):
Promise<void> {
  try {
    await api.delete(`${adminUrl}/DeleteConnection/${jobServiceId}`);
  } catch (error) {
    console.error('Error deleting pulling connection:', error);
    notifyApiError(error);
    return undefined;
  }
}

export interface IPullingData
{
  connectionsModel: IConnectionPullingData[];
  connectionsExplain: IConnectionPullingData[];
}

export interface IConnectionPullingData {
  hub: {
    clients: {
      all: any;
    },
    groups: any;
  },
  jobWebServiceId: string;
  isPulling: boolean;
  status: string;
}
