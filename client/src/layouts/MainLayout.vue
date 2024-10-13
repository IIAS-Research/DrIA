<script setup lang="ts">
import {
  onMounted, ref,
} from 'vue';
import { useUserStore } from 'stores/user-store';
import { useQuasar } from 'quasar';
import DrawerContent from 'components/layout/DrawerContent.vue';
import ParticlesBackground from 'components/home/ParticlesBackground.vue';

const userStore = useUserStore();
const $q = useQuasar();

const leftDrawerOpen = ref(false);
const alreadyNotifiedAccessTokenWillExpireSoon = ref(false);

async function checkJwtValidity() {
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
          await userStore.refreshToken(); alreadyNotifiedAccessTokenWillExpireSoon.value = false;
        },
      },
    ],
  });
  alreadyNotifiedAccessTokenWillExpireSoon.value = true;
}

onMounted(async () => {
  userStore.setUserInfosFromCookies();
  setInterval(checkJwtValidity, 2 * 60 * 1000); // Check every 2 minutes
});

</script>

<template>
  <q-layout view="hHh Lpr fFf">
    <div
      v-if="userStore.isUserConnected"
      class="flex q-pa-md q-gutter-x-sm cursor-pointer hoverable"
      style="z-index: 2; border-bottom-right-radius: 5px; width: fit-content"
      @click="leftDrawerOpen = !leftDrawerOpen"
    >
      <q-avatar size="lg">
        <q-img src="https://i.pravatar.cc/300" />
      </q-avatar>
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
      v-model="leftDrawerOpen"
      side="left"
      behavior="mobile"
      bordered
    >
      <!-- drawer content -->
      <DrawerContent
        @click="async () => { await userStore.logout(); leftDrawerOpen = false }"
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
