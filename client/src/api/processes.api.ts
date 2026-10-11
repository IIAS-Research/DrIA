import { api } from 'src/boot/axios';
import { ICreateProjectResponseDTO, IPipeline, IProcess, IRunPipelineForkDto, } from 'src/components/models';
import { notifyApiError } from 'src/utils/iaml';
const processUrl = 'Processes';
export async function getProcessPipeline(projectId: number, processId: number): Promise<IPipeline | undefined> {
  try {
    // TODO: set right url and type the promise return
    const response: IPipeline = (await api.get(`${processUrl}/${processId}?projectId=${projectId}`))?.data;
    return response;
  }
  catch (error) {
    console.error('Error fetching process pipeline:', error);
    notifyApiError(error);
    return undefined;
  }
}
export async function runNewProcessWithNewPipeline(runPipelineForkDto: IRunPipelineForkDto): Promise<ICreateProjectResponseDTO | undefined> {
  try {
    const response = (await api.post(`${processUrl}`, runPipelineForkDto))?.data;
    return response;
  }
  catch (error) {
    console.error('Error running process with fork pipeline:', error);
    notifyApiError(error);
    return undefined;
  }
}
export async function restartProcessAsync(processId: IProcess['id'], maxDurationTrainingTimeInSec: number): Promise<ICreateProjectResponseDTO | undefined> {
  try {
    console.log(maxDurationTrainingTimeInSec);
    const response = (await api.post(`${processUrl}/${processId}/restart`, { maxDuration: maxDurationTrainingTimeInSec }))?.data;
    return response;
  }
  catch (error) {
    console.error('Error restarting process:', error);
    notifyApiError(error);
    return undefined;
  }
}
export async function getProcessStatus(id: number): Promise<any> {
  return (await api.get(`${processUrl}/${id}/status`)).data;
}
