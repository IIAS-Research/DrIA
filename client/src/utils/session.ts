import axios from 'axios';
import type { Router } from 'vue-router';
import type { useUserStore } from 'src/stores/user-store';
import { notify } from './notify';

export const SESSION_EXPIRED_CODE = 'SESSION_EXPIRED';

export function isSessionExpired(error: unknown): boolean {
  return axios.isAxiosError(error) && error.code === SESSION_EXPIRED_CODE;
}

export async function expireSession(userStore: ReturnType<typeof useUserStore>, router: Router): Promise<void> {
  const hadSession = Boolean(userStore.accessToken);
  const redirect = router.currentRoute.value.fullPath;
  userStore.logout();
  if (hadSession) notify('warning', 'Your session has expired. Please sign in again.');
  if (router.currentRoute.value.name !== 'login') {
    await router.replace({ name: 'login', query: { redirect } });
  }
}
