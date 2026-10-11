<script setup lang="ts">
import 'src/css/project-creation.scss';
import {
  computed, onMounted, onUnmounted, ref,
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
const userName = computed(() => [userStore.currentUser?.firstName, userStore.currentUser?.lastName]
  .filter(Boolean).join(' ') || userStore.currentUser?.email || 'Mon compte');
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
    <div v-if="userStore.isUserConnected" class="menu-trigger-row">
      <q-btn
        no-caps unelevated class="menu-trigger dria-navigation-colors"
        :aria-label="leftDrawerOpen ? 'Fermer le menu de navigation' : 'Ouvrir le menu de navigation'"
        :aria-expanded="leftDrawerOpen"
        aria-controls="dria-navigation"
        @click="leftDrawerOpen = !leftDrawerOpen"
      >
        <span class="menu-trigger-icon"><q-icon name="menu" size="21px" aria-hidden="true" /></span>
        <span class="menu-trigger-copy"><strong>Menu</strong><span>{{ userName }}</span></span>
        <q-icon name="chevron_right" size="18px" aria-hidden="true" />
      </q-btn>
    </div>
    <q-drawer
      v-if="userStore.isUserConnected"
      v-model="leftDrawerOpen"
      side="left"
      behavior="mobile"
      :width="320"
      class="navigation-drawer dria-navigation-colors"
      bordered
    >
      <DrawerContent
        @logout="logout"
        @close="leftDrawerOpen = false"
        @navigate="leftDrawerOpen = false"
        :is-open="leftDrawerOpen"
        :user="userStore.currentUser"
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
.menu-trigger-row { position: relative; z-index: 2; width: fit-content; max-width: 100%; padding: 16px; }
.menu-trigger { max-width: 100%; padding: 8px 12px; color: var(--project-ink); background: var(--project-surface); border: 1px solid var(--project-border); border-radius: 10px; }
.menu-trigger .q-btn__content { flex-wrap: nowrap; gap: 10px; }
.menu-trigger:hover { background: var(--project-paper); }
.menu-trigger:focus-visible { outline: 2px solid var(--q-primary); outline-offset: 3px; }
.menu-trigger-icon { display: inline-flex; align-items: center; justify-content: center; flex-shrink: 0; width: 34px; height: 34px; background: var(--project-brand); border-radius: 8px; }
.menu-trigger-copy { display: flex; flex-direction: column; align-items: flex-start; min-width: 0; max-width: 200px; text-align: left; line-height: 1.4; }
.menu-trigger-copy strong { font-size: 13px; font-weight: 600; }
.menu-trigger-copy > span { display: block; max-width: 100%; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; font-size: 11px; color: var(--project-muted); }
.navigation-drawer { color: var(--project-ink); background: var(--project-paper); border-color: var(--project-border); }
</style>
