import { defineStore } from 'pinia';
import { computed, ref } from 'vue';
import { Cookies, Notify } from 'quasar';
import axios from 'axios';
import { jwtDecode, JwtPayload } from 'jwt-decode';
import { IUser } from 'src/components/models';
import { api } from 'src/boot/axios';
import { apiError } from 'src/utils/iaml';

interface CustomJwtPayload extends JwtPayload {
  email: string;
  firstName: string;
  lastName: string;
  isAdmin: boolean;
}

interface CookieOptions {
  expires?: string | number | Date;
  path?: string;
  domain?: string;
  sameSite?: 'Strict' | 'Lax' | 'None';
  secure?: boolean;
  httpOnly?: boolean;
  other?: string;
}

const COOKIES_ACCESS_TOKEN = 'drIA-accessToken';
const COOKIES_OPTIONS_CONFIG: CookieOptions = { sameSite: 'Strict', path: '/' };
const REMAINING_TIME_BEFORE_ASK_REFRESH_ACCESS_TOKEN = 6 * 60 * 1000;

export const useUserStore = defineStore('user', () => {
  const decodedJwtAccessToken = ref<CustomJwtPayload>();
  const accessToken = ref<string>();
  const currentUser = ref<IUser>();
  const isUserConnected = ref(false);
  const isAccessTokenExpired = ref(false);
  const accessTokenExpirationTime = ref(0);
  const sessionRevision = ref(0);
  const isUserAdmin = computed(() => currentUser.value?.isAdmin);

  function checkIfAccessTokenIsExpired(): boolean {
    const expiration = decodedJwtAccessToken.value?.exp;
    isAccessTokenExpired.value = !expiration || !Number.isFinite(expiration)
      || Date.now() >= expiration * 1000;
    return isAccessTokenExpired.value;
  }

  function logout(): void {
    sessionRevision.value += 1;
    Cookies.remove(COOKIES_ACCESS_TOKEN, COOKIES_OPTIONS_CONFIG);
    currentUser.value = undefined;
    accessToken.value = undefined;
    decodedJwtAccessToken.value = undefined;
    accessTokenExpirationTime.value = 0;
    isUserConnected.value = false;
    isAccessTokenExpired.value = true;
  }

  function checkIfUserIsConnected(): boolean {
    if (checkIfAccessTokenIsExpired() || currentUser.value == null) {
      logout();
      return false;
    }
    isUserConnected.value = true;
    return true;
  }

  function checkIfAccessTokenWillExpireSoon(): boolean {
    if (!checkIfUserIsConnected() || !decodedJwtAccessToken.value?.exp) return false;
    return decodedJwtAccessToken.value.exp * 1000 - Date.now()
      < REMAINING_TIME_BEFORE_ASK_REFRESH_ACCESS_TOKEN;
  }

  function assignCurrentUserFromDecodedAccessToken() {
    if (!decodedJwtAccessToken.value) return;
    currentUser.value = {
      id: parseInt(decodedJwtAccessToken.value.sub!, 10),
      email: decodedJwtAccessToken.value.email,
      firstName: decodedJwtAccessToken.value.firstName,
      lastName: decodedJwtAccessToken.value.lastName,
      isAdmin: decodedJwtAccessToken.value.isAdmin,
    };
    isUserConnected.value = true;
    isAccessTokenExpired.value = false;
  }

  function acceptAccessToken(token: string): void {
    const decoded = jwtDecode<CustomJwtPayload>(token);
    if (!decoded.exp || !Number.isFinite(decoded.exp) || Date.now() >= decoded.exp * 1000) {
      throw new Error('The server returned an invalid or expired session.');
    }
    sessionRevision.value += 1;
    accessToken.value = token;
    decodedJwtAccessToken.value = decoded;
    accessTokenExpirationTime.value = decoded.exp;
    assignCurrentUserFromDecodedAccessToken();
    Cookies.set(COOKIES_ACCESS_TOKEN, token, COOKIES_OPTIONS_CONFIG);
  }

  async function login({ email, password }: { email: string; password: string }): Promise<boolean> {
    if (checkIfUserIsConnected()) return true;
    const requestedToken = accessToken.value;
    const requestedRevision = sessionRevision.value;
    try {
      const response: { accessToken: string } = (await api.post('Auth/login', { email, password })).data;
      if (accessToken.value !== requestedToken || sessionRevision.value !== requestedRevision) return false;
      if (!response?.accessToken) throw new Error('The sign-in response does not contain a session.');
      acceptAccessToken(response.accessToken);
      return true;
    } catch (error: unknown) {
      if (accessToken.value !== requestedToken || sessionRevision.value !== requestedRevision) return false;
      logout();
      const invalidCredentials = axios.isAxiosError(error) && error.response?.status === 401;
      Notify.create({
        type: invalidCredentials ? 'warning' : 'negative',
        message: invalidCredentials ? 'Invalid email or password' : apiError(error),
      });
      return false;
    }
  }

  async function refreshToken(): Promise<boolean> {
    const requestedToken = accessToken.value;
    const requestedRevision = sessionRevision.value;
    if (!requestedToken || !isUserConnected.value) return false;
    const response: { accessToken: string } = (await api.get('Auth/refresh-access-token')).data;
    // A response from an older session cannot restore a logged-out user or replace a new login.
    if (accessToken.value !== requestedToken || sessionRevision.value !== requestedRevision
      || !isUserConnected.value) return false;
    if (!response?.accessToken) throw new Error('The session could not be renewed.');
    acceptAccessToken(response.accessToken);
    return true;
  }

  function setUserInfosFromCookies(): void {
    if (!Cookies.has(COOKIES_ACCESS_TOKEN)) {
      logout();
      return;
    }
    try {
      acceptAccessToken(Cookies.get(COOKIES_ACCESS_TOKEN));
    } catch {
      logout();
    }
  }

  return {
    currentUser,
    accessToken,
    isUserConnected,
    isAccessTokenExpired,
    accessTokenExpirationTime,
    sessionRevision,
    isUserAdmin,
    login,
    checkIfAccessTokenWillExpireSoon,
    checkIfUserIsConnected,
    checkIfAccessTokenIsExpired,
    logout,
    refreshToken,
    setUserInfosFromCookies,
  };
});
