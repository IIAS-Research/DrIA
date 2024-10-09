import { api } from 'src/boot/axios';
import { IPipeline, IRunPipelineForkDto } from 'src/components/models';
import { notify } from 'src/utils/notify';

const processUrl = 'Processes';

export async function getProcessPipeline(projectId: number, processId: number): Promise<IPipeline | undefined> {
  try {
    // TODO: set right url and type the promise return
    const response: IPipeline = (await api.get(`${processUrl}/${processId}?projectId=${projectId}`))?.data;
    return response;
  } catch (error) {
    console.error('Error fetching process pipeline:', error);
    notify('negative', String(error));
    return undefined;
  }
}

export async function runNewProcessWithNewPipeline(runPipelineForkDto: IRunPipelineForkDto): Promise<{ isSuccess: boolean } | undefined> {
  try {
    // TODO: set right url and type the promise return
    const response = (await api.post(`${processUrl}`, runPipelineForkDto))?.data;
    return { isSuccess: true };
  } catch (error) {
    console.error('Error running process with fork pipeline:', error);
    notify('negative', String(error));
    return undefined;
  }
}
