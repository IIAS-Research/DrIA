import axios from 'axios';
import { boot } from 'quasar/wrappers';
import { useUserStore } from 'src/stores/user-store';
import { expireSession, SESSION_EXPIRED_CODE } from 'src/utils/session';

// Be careful when using SSR for cross-request state pollution
// due to creating a Singleton instance here;
// If any client changes this (global) instance, it might be a
// good idea to move this instance creation inside of the
// "export default () => {}" function below (which runs individually
// for each client)
const api = axios.create({ baseURL: process.env.API });
const requestSessions = new WeakMap<object, number>();

export default boot(({ router }) => {
  const userStore = useUserStore();
  userStore.setUserInfosFromCookies();

  // Requests
  api.interceptors.request.use(
    (config) => {
      requestSessions.set(config, userStore.sessionRevision);
      if (userStore.isUserConnected) {
        config.headers.Authorization = `Bearer ${userStore.accessToken}`;
      }
      return config;
    },
    (error) => {
      return Promise.reject(error);
    },
  );

  // Responses
  api.interceptors.response.use(
    (response) => response,
    async (error) => {
      const loginRequest = /(?:^|\/)Auth\/login(?:[?#]|$)/i.test(error.config?.url ?? '');
      if (axios.isAxiosError(error) && error.response?.status === 401 && !loginRequest) {
        error.code = SESSION_EXPIRED_CODE;
        const authorization = error.config?.headers?.Authorization ?? error.config?.headers?.authorization;
        const currentToken = userStore.accessToken;
        // A delayed response from an older session must not clear a new login.
        const sameSession = error.config && requestSessions.get(error.config) === userStore.sessionRevision;
        if (sameSession && ((currentToken && authorization === `Bearer ${currentToken}`)
          || (!currentToken && router.currentRoute.value.name !== 'login'))) {
          await expireSession(userStore, router);
        }
      }
      return Promise.reject(error);
    },
  );
});

export { api };
