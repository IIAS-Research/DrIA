<script setup lang="ts">
import AppPresentation from 'components/home/AppPresentation.vue';
import { useUserStore } from 'stores/user-store';
import { reactive, ref } from 'vue';
import { useRoute, useRouter } from 'vue-router';

const userStore = useUserStore();
const router = useRouter();
const route = useRoute();
const submitting = ref(false);

const login = reactive({
  email: '',
  password: '',
});

async function submit() {
  if (submitting.value) return;
  submitting.value = true;
  try {
    if (!await userStore.login(login)) return;
    const redirect = route.query.redirect;
    if (typeof redirect === 'string' && redirect.startsWith('/') && !redirect.startsWith('//')) {
      const destination = router.resolve(redirect);
      if (destination.matched.length && destination.name !== 'login') {
        await router.replace(redirect);
        return;
      }
    }
    await router.replace({ name: 'home' });
  } finally {
    submitting.value = false;
  }
}
</script>

<template>
  <q-page class="column q-pt-xl q-mt-xl">
    <div class="flex column flex-center">
      <img class="fixed-bottom-right" src="/images/iias_miniature.png" height="50px" style="right: 20px;">
      <AppPresentation />

      <q-form class="q-gutter-y-sm q-pt-lg" @submit="submit">
        <div class="text-h6 text-center">
          Connexion
        </div>
        <q-input v-model="login.email" label="Email" filled type="email"
          :rules="[(val, rules) => rules.email(val) || `Format d'adresse email invalide`]" lazy-rules />
        <q-input v-model="login.password" type="password" label="Mot de passe" filled lazy-rules />

        <q-btn color="primary" type="submit" label="Connexion" class="full-width q-mt-md" :loading="submitting" />
      </q-form>
    </div>
  </q-page>
</template>
