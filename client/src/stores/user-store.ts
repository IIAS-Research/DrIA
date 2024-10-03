import { defineStore } from 'pinia';
import { ref } from 'vue';
import { Cookies } from 'quasar';
import { jwtDecode, JwtPayload } from 'jwt-decode';
import { IUser } from 'src/components/models';
import { api } from 'src/boot/axios';

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
const COOKIES_OPTIONS_CONFIG: CookieOptions = { sameSite: 'Strict', path: '/' }; // https://quasar.dev/quasar-plugins/cookies#cookies-api
const REMAINING_TIME_BEFORE_ASK_REFRESH_ACCESS_TOKEN = 6 * 60 * 1000;

export const useUserStore = defineStore('user', () => {
  // States
  const decodedJwtAccessToken = ref<CustomJwtPayload>();
  const accessToken = ref<string>();
  const currentUser = ref<IUser>();
  const isUserConnected = ref(false);
  const isAccessTokenExpired = ref(false);
  const accessTokenExpirationTime = ref(0);

  // Actions
  function checkIfAccessTokenIsExpired() {
    if (!decodedJwtAccessToken.value?.exp) {
      isAccessTokenExpired.value = true;
      return true;
    }
    isAccessTokenExpired.value = new Date() > new Date(decodedJwtAccessToken.value.exp * 1000);
    return new Date() > new Date(decodedJwtAccessToken.value.exp * 1000);
  }

  async function logout() {
    Cookies.remove(COOKIES_ACCESS_TOKEN, COOKIES_OPTIONS_CONFIG);
    currentUser.value = undefined;
    accessToken.value = undefined;
    decodedJwtAccessToken.value = undefined;
    isUserConnected.value = false;
    isAccessTokenExpired.value = true;
  }

  function checkIfUserIsConnected() {
    if (checkIfAccessTokenIsExpired() || currentUser.value == null) {
      logout();
      return false;
    }
    isUserConnected.value = true;
    return true;
  }

  function checkIfAccessTokenWillExpireSoon() {
    if (!checkIfUserIsConnected() || !decodedJwtAccessToken.value?.exp) return false;
    const now = new Date().getTime();
    const expTime = new Date(decodedJwtAccessToken.value.exp * 1000).getTime();
    return expTime - now < REMAINING_TIME_BEFORE_ASK_REFRESH_ACCESS_TOKEN;
  }

  function decodeJwtFromAccessToken() {
    if (!accessToken.value) return;
    decodedJwtAccessToken.value = jwtDecode<CustomJwtPayload>(
      accessToken.value,
    );
    accessTokenExpirationTime.value = decodedJwtAccessToken.value.exp ?? 0;
  }

  function assignCurrentUserFromDecodedAccessToken() {
    if (!decodedJwtAccessToken.value) return;
    currentUser.value = {
      // sub is set by the server so no doubts it is defined here
      id: parseInt(decodedJwtAccessToken.value.sub!, 10),
      email: decodedJwtAccessToken.value.email,
      firstName: decodedJwtAccessToken.value.firstName,
      lastName: decodedJwtAccessToken.value.lastName,
      isAdmin: decodedJwtAccessToken.value.isAdmin,
    };
    isUserConnected.value = true;
  }

  async function login({
    email,
    password,
  }: {
    email: string;
    password: string;
  }) {
    if (checkIfUserIsConnected()) return;
    const response: { accessToken: string } | undefined = (
      await api.post('Auth/login', { email, password })
    )?.data;
    if (!response?.accessToken) return;
    accessToken.value = response.accessToken;
    Cookies.set(
      COOKIES_ACCESS_TOKEN,
      accessToken.value,
      COOKIES_OPTIONS_CONFIG,
    );
    decodeJwtFromAccessToken();
    assignCurrentUserFromDecodedAccessToken();
  }

  async function refreshToken() {
    const response: { accessToken: string } | undefined = (
      await api.get('Auth/refresh-access-token')
    )?.data;
    if (!response?.accessToken) return;
    if (Cookies.has(COOKIES_ACCESS_TOKEN)) {
      Cookies.remove(COOKIES_ACCESS_TOKEN, COOKIES_OPTIONS_CONFIG);
    }
    accessToken.value = response.accessToken;
    Cookies.set(
      COOKIES_ACCESS_TOKEN,
      accessToken.value,
      COOKIES_OPTIONS_CONFIG,
    );
    decodeJwtFromAccessToken();
    assignCurrentUserFromDecodedAccessToken();
  }

  function setUserInfosFromCookies() {
    if (!Cookies.has(COOKIES_ACCESS_TOKEN)) return;
    accessToken.value = Cookies.get(COOKIES_ACCESS_TOKEN);
    decodeJwtFromAccessToken();
    if (checkIfAccessTokenIsExpired()) {
      Cookies.remove(COOKIES_ACCESS_TOKEN, COOKIES_OPTIONS_CONFIG);
    } else {
      assignCurrentUserFromDecodedAccessToken();
    }
  }

  return {
    currentUser,
    accessToken,
    isUserConnected,
    isAccessTokenExpired,
    accessTokenExpirationTime,
    login,
    checkIfAccessTokenWillExpireSoon,
    checkIfUserIsConnected,
    checkIfAccessTokenIsExpired,
    logout,
    refreshToken,
    setUserInfosFromCookies,
  };
});
