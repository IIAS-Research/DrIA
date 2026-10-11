<script setup lang="ts">
import {
  onMounted, onUnmounted, ref,
} from 'vue';
import { useUserStore } from 'stores/user-store';
import { useQuasar } from 'quasar';
import DrawerContent from 'components/layout/DrawerContent.vue';
import ParticlesBackground from 'components/home/ParticlesBackground.vue';
import { useRouter } from 'vue-router';
import { expireSession, isSessionExpired } from 'src/utils/session';
import { apiError } from 'src/utils/iaml';

const userStore = useUserStore();
const $q = useQuasar();
const router = useRouter();

const leftDrawerOpen = ref(false);
const alreadyNotifiedAccessTokenWillExpireSoon = ref(false);
let sessionInterval: ReturnType<typeof setInterval> | undefined;

async function checkJwtValidity() {
  if (!userStore.accessToken) {
    alreadyNotifiedAccessTokenWillExpireSoon.value = false;
    return;
  }
  if (userStore.checkIfAccessTokenIsExpired()) {
    await expireSession(userStore, router);
    alreadyNotifiedAccessTokenWillExpireSoon.value = false;
    return;
  }
  if (!userStore.checkIfAccessTokenWillExpireSoon()) return;
  if (alreadyNotifiedAccessTokenWillExpireSoon.value) return;
  const timeoutBeforeAccessTokenIsExpired = (new Date(userStore.accessTokenExpirationTime * 1000).getTime()
    - new Date().getTime());
  $q.notify({
    message: 'Votre session va bientôt expirer',
    timeout: timeoutBeforeAccessTokenIsExpired,
    closeBtn: true,
    icon: 'info',
    multiLine: true,
    actions: [
      {
        label: 'Rester connecter',
        color: 'primary',
        handler: async () => {
          try {
            if (await userStore.refreshToken()) alreadyNotifiedAccessTokenWillExpireSoon.value = false;
          } catch (error) {
            if (!isSessionExpired(error)) $q.notify({ type: 'negative', message: apiError(error) });
          }
        },
      },
    ],
  });
  alreadyNotifiedAccessTokenWillExpireSoon.value = true;
}

onMounted(() => {
  sessionInterval = setInterval(checkJwtValidity, 2 * 60 * 1000);
});
onUnmounted(() => { if (sessionInterval) clearInterval(sessionInterval); });

async function logout() {
  userStore.logout();
  alreadyNotifiedAccessTokenWillExpireSoon.value = false;
  leftDrawerOpen.value = false;
  await router.replace({ name: 'login' });
}

</script>

<template>
  <q-layout view="hHh Lpr fFf">
    <div
      v-if="userStore.isUserConnected"
      class="flex q-pa-md q-gutter-x-sm cursor-pointer hoverable"
      style="z-index: 2; border-bottom-right-radius: 5px; width: fit-content"
      @click="leftDrawerOpen = !leftDrawerOpen"
    >
      <q-avatar size="lg" color="secondary" icon="person" text-color="white" />
      <div class="column justify-center">
        <div class="text-weight-bold">
          {{ userStore.currentUser?.firstName }} {{ userStore.currentUser?.lastName }}
        </div>
        <div
          class="text-caption text-italic text-grey-8"
          style="line-height: 1rem;"
        >
          Cliquer pour ouvrir le menu
        </div>
      </div>
    </div>
    <q-drawer
      v-if="userStore.isUserConnected"
      v-model="leftDrawerOpen"
      side="left"
      behavior="mobile"
      bordered
    >
      <!-- drawer content -->
      <DrawerContent
        @click="logout"
        :is-user-connected="userStore.isUserConnected"
        :is-user-admin="userStore.isUserAdmin"
      />
    </q-drawer>

    <q-page-container>
      <ParticlesBackground />
      <div
        class="router-view"
      >
        <router-view />
      </div>
    </q-page-container>
  </q-layout>
</template>

<style>
.router-view {
  max-width: 1400px;
  margin: auto;
  padding-bottom: 200px;
}
</style>
