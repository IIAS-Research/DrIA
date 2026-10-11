import { api } from 'src/boot/axios';
import type { IPullingData } from './admin.api';
import type { ICreateUserDTO, IProject, IUser } from 'src/components/models';

// Administration pages own their loading, errors and success notifications.
// Keep rejected requests rejected so the displayed lists reflect confirmed changes.
export async function getAdminUsers(signal?: AbortSignal): Promise<IUser[]> {
  const { data } = await api.get<IUser[]>('Users', { signal });
  if (!Array.isArray(data)) throw new Error('La liste des utilisateurs n’a pas pu être chargée.');
  return data;
}

export async function createAdminUser(user: ICreateUserDTO & { password: string }): Promise<IUser> {
  return (await api.post<IUser>('Auth/register', user)).data;
}

export async function updateAdminUser(id: number, user: IUser & { password?: string }): Promise<IUser> {
  return (await api.patch<IUser>(`Users/${id}`, user)).data;
}

export async function deleteAdminUser(id: number): Promise<void> {
  await api.delete(`Users/${id}`);
}

export async function getAdminProjects(limit: number, offset: number, signal?: AbortSignal): Promise<IProject[]> {
  const { data } = await api.get<IProject[]>('Projects', { params: { limit, offset }, signal });
  if (!Array.isArray(data)) throw new Error('La liste des projets n’a pas pu être chargée.');
  return data;
}

export async function deleteAdminProject(id: number): Promise<void> {
  await api.delete(`Projects/${id}`);
}

export async function getAdminMonitoring(signal?: AbortSignal): Promise<IPullingData> {
  const { data } = await api.get<IPullingData>('Admin/PullingData', { signal });
  if (!Array.isArray(data?.connectionsModel) || !Array.isArray(data?.connectionsExplain))
    throw new Error('Le suivi des calculs n’a pas pu être chargé.');
  return data;
}

export async function stopAdminMonitoring(jobId: string): Promise<void> {
  await api.delete(`Admin/DeleteConnection/${encodeURIComponent(jobId)}`);
}
