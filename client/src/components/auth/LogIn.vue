<script setup lang="ts">
import { QForm } from 'quasar';
import { useUserStore } from 'stores/user-store';
import { reactive } from 'vue';

const userStore = useUserStore();

const login = reactive({
  email: '',
  password: '',
});

async function submit() {
  await userStore.login(login);
}
</script>

<template>
  <q-form
    class="q-gutter-y-sm"
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
      :rules="[ (val, rules) => rules.email(val) || `Format d'adresse email invalide` ]"
      lazy-rules
    />
    <q-input
      v-model="login.password"
      type="password"
      label="Mot de passe"
      filled
      :rules="[ (val) => val.length >= 6 || `Mot de passe trop court` ]"
      lazy-rules
    />

    <q-btn
      color="primary"
      type="submit"
      label="Connexion"
      class="full-width q-mt-md"
    />
  </q-form>
</template>
