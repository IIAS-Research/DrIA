import { defineStore } from 'pinia';
import { computed, ref } from 'vue';
import { Cookies } from 'quasar';
import { jwtDecode, JwtPayload } from 'jwt-decode';
import { IUser } from 'src/components/models';
import { api } from 'src/boot/axios';

interface CustomJwtPayload extends JwtPayload {
  email: string;
  firstName: string;
  lastName: string;
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

export const useUserStore = defineStore('user', () => {
  // States
  const decodedJwtAccessToken = ref<CustomJwtPayload>();
  const accessToken = ref<string>();
  const currentUser = ref<IUser>();

  // Getters
  const isAccessTokenExpired = computed(() => {
    if (!decodedJwtAccessToken.value?.exp) return true;
    return new Date() > new Date((decodedJwtAccessToken.value.exp) * 1000);
  });
  const isUserConnected = computed(() => (!isAccessTokenExpired.value && currentUser.value !== undefined));

  // Methods

  function decodeJwtFromAccessToken() {
    if (!accessToken.value) return;
    decodedJwtAccessToken.value = jwtDecode<CustomJwtPayload>(accessToken.value);
  }

  function assignCurrentUserFromDecodedAccessToken() {
    if (!decodedJwtAccessToken.value) return;
    currentUser.value = {
      // sub is set by the server so no doubts it is defined here
      id: decodedJwtAccessToken.value.sub!,
      email: decodedJwtAccessToken.value.email,
      firstName: decodedJwtAccessToken.value.firstName,
      lastName: decodedJwtAccessToken.value.lastName,
    };
  }

  async function login({ email, password }: { email: string, password: string}) {
    if (isUserConnected.value) return;
    const response: { access_token: string } | undefined = (await api.post(
      'authentication/login',
      { email, password },
    ))?.data;
    if (!response?.access_token) return;
    accessToken.value = response.access_token;
    Cookies.set(COOKIES_ACCESS_TOKEN, accessToken.value, COOKIES_OPTIONS_CONFIG);
    decodeJwtFromAccessToken();
    assignCurrentUserFromDecodedAccessToken();
  }

  async function logout() {
    Cookies.remove(COOKIES_ACCESS_TOKEN, COOKIES_OPTIONS_CONFIG);
    currentUser.value = undefined;
    accessToken.value = undefined;
    decodedJwtAccessToken.value = undefined;
  }

  async function refreshToken() {
    const response: { access_token: string } | undefined = (await api.get('authentication/refresh-token'))?.data;
    if (!response?.access_token) return;
    if (Cookies.has(COOKIES_ACCESS_TOKEN)) {
      Cookies.remove(COOKIES_ACCESS_TOKEN, COOKIES_OPTIONS_CONFIG);
    }
    accessToken.value = response.access_token;
    Cookies.set(COOKIES_ACCESS_TOKEN, accessToken.value, COOKIES_OPTIONS_CONFIG);
    decodeJwtFromAccessToken();
    assignCurrentUserFromDecodedAccessToken();
  }

  function setUserInfosFromCookies() {
    if (!Cookies.has(COOKIES_ACCESS_TOKEN)) return;
    accessToken.value = Cookies.get(COOKIES_ACCESS_TOKEN);
    decodeJwtFromAccessToken();
    if (isAccessTokenExpired.value) {
      Cookies.remove(COOKIES_ACCESS_TOKEN, COOKIES_OPTIONS_CONFIG);
    } else {
      assignCurrentUserFromDecodedAccessToken();
    }
  }

  return {
    currentUser,
    isUserConnected,
    login,
    logout,
    refreshToken,
    setUserInfosFromCookies,
  };
});
