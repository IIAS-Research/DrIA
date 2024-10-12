import { api } from 'src/boot/axios';
import { ICsvProject, IProject } from 'src/components/models';
import { notify } from 'src/utils/notify';

const projectUrl = 'Projects';

export async function getUserProjects(): Promise<IProject[]> {
  try {
    const response = (await api.get(`${projectUrl}/me`))?.data;
    return response;
  } catch (error) {
    console.error('Error fetching projects:', error);
    notify('negative', String(error));
    return [];
  }
}

export async function GetProjectWithNestedDataById(projectId: IProject['id']): Promise<IProject | undefined> {
  try {
    const response = (await api.get(`${projectUrl}/${projectId}/nested-data`))?.data;

    // Sort processes models by main test metric
    response.processes = response.processes.map((p) => {
      p.models = p.models.sort((a, b) => {
        if (!a.metrics.metrics_test || !b.metrics.metrics_test) {
          return b.metrics.metrics_train[b.metrics.main_metrics] - a.metrics.metrics_train[a.metrics.main_metrics];
        }
        return b.metrics.metrics_test[b.metrics.main_metrics] - a.metrics.metrics_test[a.metrics.main_metrics];
      });

      return p;
    });

    return response;
  } catch (error) {
    console.error('Error fetching project:', error);
    notify('negative', String(error));
    return undefined;
  }
}

export async function getProjectDataSampleAsync(projectId: IProject['id']): Promise<ICsvProject | undefined> {
  try {
    const response = (await api.get(`${projectUrl}/${projectId}/data-sample`))?.data;
    return response;
  } catch (error) {
    console.error('Error fetching project data sample:', error);
    notify('negative', String(error));
    return undefined;
  }
}
