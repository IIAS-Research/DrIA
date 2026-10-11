import { api } from 'src/boot/axios';
import { ICsvProject, IProject } from 'src/components/models';
import { notifyApiError } from 'src/utils/iaml';
const projectUrl = 'Projects';
export async function getProjects(limit: number, offset: number): Promise<IProject[]> {
  try {
    const response = (await api.get(`${projectUrl}?limit=${limit}&offset=${offset}`))?.data;
    return response;
  }
  catch (error) {
    console.error('Error fetching projects:', error);
    notifyApiError(error);
    return [];
  }
}
export async function deleteProject(projectId: IProject['id']) {
  try {
    await api.delete(`${projectUrl}/${projectId}`);
  }
  catch (error) {
    console.error('Error fetching projects:', error);
    notifyApiError(error);
  }
}
export async function getUserProjects(): Promise<IProject[]> {
  try {
    const response = (await api.get(`${projectUrl}/me`))?.data;
    return response;
  } catch (error) {
    console.error('Error fetching projects:', error);
    notifyApiError(error);
    return [];
  }
}

export async function GetProjectWithNestedDataById(projectId: IProject['id']): Promise<IProject> {
  const response: IProject = (await api.get(`${projectUrl}/${projectId}/nested-data`)).data;
  response.processes = response.processes.map((p) => {
    // Candidate order is stable; the final test must not rank the models.
    p.models = (p.models ?? []).sort((a, b) => a.metrics.id - b.metrics.id || a.id - b.id);
    return p;
  });
  return response;
}
export async function getProjectDataSampleAsync(projectId: IProject['id']): Promise<ICsvProject | undefined> {
  try {
    const response = (await api.get(`${projectUrl}/${projectId}/data-sample`))?.data;
    return response;
  }
  catch (error) {
    console.error('Error fetching project data sample:', error);
    notifyApiError(error);
    return undefined;
  }
}
