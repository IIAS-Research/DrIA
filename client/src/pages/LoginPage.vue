<script setup lang="ts">
import AppPresentation from 'components/home/AppPresentation.vue';
import { QForm } from 'quasar';
import { useUserStore } from 'stores/user-store';
import { reactive } from 'vue';
import { useRouter } from 'vue-router';

const userStore = useUserStore();
const router = useRouter();

const login = reactive({
  email: '',
  password: '',
});

async function submit() {
  await userStore.login(login);
  router.push({ name: 'home' });
}
</script>

<template>
  <q-page class="column q-pt-xl q-mt-xl">
    <div class="flex column flex-center">
      <img
        class="fixed-bottom-right"
        src="/images/iias_miniature.png"
        height="50px"
        style="right: 20px;"
      >
      <AppPresentation />

      <q-form
        class="q-gutter-y-sm q-pt-lg"
        @submit="submit"
      >
        <div class="text-h6 text-center">
          Connexion
        </div>
        <q-input
          v-model="login.email"
          label="Email"
          filled
          type="email"
          :rules="[(val, rules) => rules.email(val) || `Format d'adresse email invalide`]"
          lazy-rules
        />
        <q-input
          v-model="login.password"
          type="password"
          label="Mot de passe"
          filled
          lazy-rules
        />

        <q-btn
          color="primary"
          type="submit"
          label="Connexion"
          class="full-width q-mt-md"
        />
      </q-form>
    </div>
  </q-page>
</template>
