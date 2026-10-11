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
  return (value?.trim().length ?? 0) >= 3 || 'Enter at least 3 characters.';
}

function passwordRule(value?: string): true | string {
  if (!value || value.length < 6) return 'Enter at least 6 characters.';
  if (!/[a-z]/.test(value) || !/[A-Z]/.test(value) || !/[0-9]/.test(value) || !/[^a-zA-Z0-9]/.test(value)) {
    return 'Include an uppercase letter, a lowercase letter, a number and a symbol.';
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
        label="First name"
        autocomplete="given-name"
        outlined
        :disable="loading"
        :rules="[nameRule]"
        lazy-rules
      />
      <q-input
        v-model="userLocalComponent.lastName"
        label="Last name"
        autocomplete="family-name"
        outlined
        :disable="loading"
        :rules="[nameRule]"
        lazy-rules
      />
    </div>
    <q-input
      v-model="userLocalComponent.email"
      label="Email address"
      type="email"
      autocomplete="email"
      outlined
      :disable="loading"
      :rules="[(value, rules) => rules.email(value?.trim()) || 'Enter a valid email address.']"
      lazy-rules
    />
    <q-input
      v-if="!isEditing"
      v-model="userLocalComponent.password"
      label="Password"
      :type="showPassword ? 'text' : 'password'"
      autocomplete="new-password"
      hint="At least 6 characters, including an uppercase letter, a lowercase letter, a number and a symbol."
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
          :aria-label="showPassword ? 'Hide password' : 'Show password'"
          :disable="loading"
          @click="showPassword = !showPassword"
        />
      </template>
    </q-input>

    <div class="admin-user-access">
      <div>
        <strong>Administrator access</strong>
        <p>An administrator can manage all DrIA accounts and projects.</p>
      </div>
      <q-toggle
        v-model="userLocalComponent.isAdmin"
        color="primary"
        aria-label="Grant administrator access"
        :disable="loading"
      />
    </div>

    <div class="admin-dialog-actions">
      <q-btn flat no-caps label="Cancel" :disable="loading" @click="emits('cancel')" />
      <q-btn
        unelevated
        no-caps
        :label="isEditing ? 'Save changes' : 'Create account'"
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
