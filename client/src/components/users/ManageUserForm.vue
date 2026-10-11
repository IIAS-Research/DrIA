<script setup lang="ts">
import { computed, ref, watch } from 'vue';
import { ICreateUserDTO, IUser } from 'components/models';

type ManagedUser = (IUser | ICreateUserDTO) & { password?: string };
const props = withDefaults(defineProps<{
  user: ManagedUser;
  loading?: boolean;
}>(), { loading: false });
const emits = defineEmits<{
  submit: [updatedUser: ManagedUser];
  cancel: [];
}>();

const userLocalComponent = ref<ManagedUser>({ ...props.user });
const showPassword = ref(false);
const isEditing = computed(() => 'id' in props.user);
const hasChanges = computed(() => ['firstName', 'lastName', 'email', 'isAdmin'].some((key) => (
  userLocalComponent.value[key as keyof ICreateUserDTO] !== props.user[key as keyof ICreateUserDTO]
)));

watch(() => props.user, (user) => {
  userLocalComponent.value = { ...user };
  showPassword.value = false;
});

function nameRule(value?: string): true | string {
  return (value?.trim().length ?? 0) >= 3 || 'Saisissez au moins 3 caractères.';
}

function passwordRule(value?: string): true | string {
  if (!value || value.length < 6) return 'Saisissez au moins 6 caractères.';
  if (!/[a-z]/.test(value) || !/[A-Z]/.test(value) || !/[0-9]/.test(value) || !/[^a-zA-Z0-9]/.test(value)) {
    return 'Ajoutez une majuscule, une minuscule, un chiffre et un symbole.';
  }
  return true;
}

function submit() {
  if (props.loading) return;
  const payload: ManagedUser = {
    ...userLocalComponent.value,
    firstName: userLocalComponent.value.firstName.trim(),
    lastName: userLocalComponent.value.lastName.trim(),
    email: userLocalComponent.value.email.trim(),
  };
  // Password changes are not supported by the existing user update endpoint.
  if (isEditing.value) delete payload.password;
  emits('submit', payload);
}
</script>

<template>
  <q-form class="admin-form" @submit="submit">
    <div class="admin-form-grid">
      <q-input
        v-model="userLocalComponent.firstName"
        label="Prénom"
        autocomplete="given-name"
        outlined
        :disable="loading"
        :rules="[nameRule]"
        lazy-rules
      />
      <q-input
        v-model="userLocalComponent.lastName"
        label="Nom"
        autocomplete="family-name"
        outlined
        :disable="loading"
        :rules="[nameRule]"
        lazy-rules
      />
    </div>
    <q-input
      v-model="userLocalComponent.email"
      label="Adresse email"
      type="email"
      autocomplete="email"
      outlined
      :disable="loading"
      :rules="[(value, rules) => rules.email(value?.trim()) || 'Saisissez une adresse email valide.']"
      lazy-rules
    />
    <q-input
      v-if="!isEditing"
      v-model="userLocalComponent.password"
      label="Mot de passe"
      :type="showPassword ? 'text' : 'password'"
      autocomplete="new-password"
      hint="6 caractères minimum, avec majuscule, minuscule, chiffre et symbole."
      outlined
      :disable="loading"
      :rules="[passwordRule]"
      lazy-rules
    >
      <template #append>
        <q-btn
          flat
          round
          dense
          :icon="showPassword ? 'visibility_off' : 'visibility'"
          :aria-label="showPassword ? 'Masquer le mot de passe' : 'Afficher le mot de passe'"
          :disable="loading"
          @click="showPassword = !showPassword"
        />
      </template>
    </q-input>

    <div class="admin-user-access">
      <div>
        <strong>Droits d’administration</strong>
        <p>Un administrateur peut gérer tous les comptes et tous les projets de DrIA.</p>
      </div>
      <q-toggle
        v-model="userLocalComponent.isAdmin"
        color="primary"
        aria-label="Accorder les droits d’administration"
        :disable="loading"
      />
    </div>

    <div class="admin-dialog-actions">
      <q-btn flat no-caps label="Annuler" :disable="loading" @click="emits('cancel')" />
      <q-btn
        unelevated
        no-caps
        :label="isEditing ? 'Enregistrer les modifications' : 'Créer le compte'"
        color="primary"
        type="submit"
        :loading="loading"
        :disable="isEditing && !hasChanges"
      />
    </div>
  </q-form>
</template>

<style scoped lang="scss">
.admin-user-access {
  display: flex;
  align-items: center;
  gap: 16px;
  padding: 16px;
  border: 1px solid var(--project-border);
  border-radius: 12px;
  background: var(--project-paper);
  color: var(--project-ink);
  strong { font-size: 13px; font-weight: 600; }
  p { margin: 5px 0 0; color: var(--project-muted); font-size: 12px; line-height: 1.6; }
  :deep(.q-toggle) { flex-shrink: 0; margin-left: auto; }
}
</style>
