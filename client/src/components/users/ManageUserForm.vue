<script setup lang="ts">
import { ref } from 'vue';
import { ICreateUserDTO, IUser } from 'components/models';

const props = defineProps<{
    user: IUser | ICreateUserDTO
}>();

const emits = defineEmits<{
    submit: [updatedUser: IUser | ICreateUserDTO]
}>();

const userLocalComponent = ref<(IUser | ICreateUserDTO) & { password?: string }>({ ...props.user });

</script>

<template>
  <div>
    <div class="text-h6 text-center">
      Mise à jour de l'utilisateur
    </div>
    <q-form
      class="q-gutter-y-sm q-mt-md"
      @submit="emits('submit', userLocalComponent)"
    >
      <div class="flex q-gutter-x-md">
        <q-input
          v-model="userLocalComponent.lastName"
          label="Nom"
          filled
          :rules="[(v) => v.length > 2 || 'Minimum 3 charactères requis']"
          lazy-rules
        />

        <q-input
          v-model="userLocalComponent.firstName"
          label="Prénom"
          filled
          :rules="[(v) => v.length > 2 || 'Minimum 3 charactères requis']"
          lazy-rules
        />
      </div>

      <q-input
        v-model="userLocalComponent.email"
        label="Email"
        filled
        :rules="[(v, rules) => rules.email(v) || 'Adresse email valide requise']"
        lazy-rules
      />

      <q-input
        v-model="userLocalComponent.password"
        label="Nouveau mot de passe (facultatif)"
        type="password"
        filled
        lazy-rules
      />

      <q-toggle
        v-model="userLocalComponent.isAdmin"
        label="Administrateur"
      />

      <q-btn
        class="full-width q-mt-md"
        label="Enregistrer"
        color="primary"
        type="submit"
        :disable="JSON.stringify(props.user) === JSON.stringify(userLocalComponent)"
      />
    </q-form>
  </div>
</template>
