import axios from 'axios';
import { boot } from 'quasar/wrappers';
import { useUserStore } from 'src/stores/user-store';

// Be careful when using SSR for cross-request state pollution
// due to creating a Singleton instance here;
// If any client changes this (global) instance, it might be a
// good idea to move this instance creation inside of the
// "export default () => {}" function below (which runs individually
// for each client)
const api = axios.create({ baseURL: process.env.API });

export default boot(({ router }) => {
  const userStore = useUserStore();

  // Requests
  api.interceptors.request.use(
    (config) => {
      if (userStore.isUserConnected) {
        config.headers.Authorization = `Bearer ${userStore.accessToken}`;
      }
      return config;
    },
    (error) => {
      console.error(error.message);
    },
  );

  // Responses
  api.interceptors.response.use(
    (response) => response,
    (error) => {
      return Promise.reject(error);
    },
  );
});

export { api };
