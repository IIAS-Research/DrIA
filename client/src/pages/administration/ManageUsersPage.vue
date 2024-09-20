<script setup lang="ts">
import { QTableColumn } from 'quasar';
import { deleteOneUser, findAllUsers, updateOneUser } from 'src/api/users.api';
import { IUser } from 'components/models';
import ManageUserForm from 'components/users/ManageUserForm.vue';
import { onMounted, ref } from 'vue';

const columns: QTableColumn[] = [
  {
    name: 'id',
    label: 'ID',
    field: 'id',
    align: 'left',
  },
  {
    name: 'firstName',
    label: 'Prénom',
    field: 'firstName',
    align: 'left',
  },
  {
    name: 'lastName',
    label: 'Nom',
    field: 'lastName',
    align: 'left',
  },
  {
    name: 'email',
    label: 'Email',
    field: 'email',
    align: 'left',
  },
  {
    name: 'isAdmin',
    label: 'Admin',
    field: 'isAdmin',
    align: 'left',
    format: (val) => (val === true ? 'Oui' : 'Non'),
  },
  {
    name: 'actions',
    label: 'Actions',
    field: '',
    align: 'left',
  },
];

const users = ref<IUser[]>();
const dialogUpdateUser = ref(false);
const userToManage = ref<IUser>();

async function submit(updatedUser: IUser) {
  if (!userToManage.value || !users.value) return;
  const updatedUserResponse = await updateOneUser(userToManage.value?.id, updatedUser);
  users.value = [...users.value.map((user) => {
    if (user.id === updatedUserResponse?.id) {
      return updatedUserResponse;
    } return user;
  })];
  userToManage.value = undefined;
  dialogUpdateUser.value = false;
}

async function deleteUser(userId: IUser['id']) {
  if (!users.value) return;
  await deleteOneUser(userId);
  users.value = [...users.value.filter((user) => user.id !== userId)];
}

onMounted(async () => {
  users.value = await findAllUsers();
});
</script>

<template>
  <q-page padding>
    <q-table
      v-if="users"
      title="Utilisateurs"
      :rows="users"
      :columns="columns"
      row-key="name"
      flat
      bordered
    >
      <template #body-cell-actions="{ row }">
        <q-td>
          <div class="q-gutter-x-sm">
            <q-btn
              icon="edit"
              color="warning"
              glossy
              size="md"
              rounded
              dense
              @click="userToManage = row; dialogUpdateUser = true;"
            >
              <q-tooltip>Modifier l'utilisateur</q-tooltip>
            </q-btn>

            <q-btn
              icon="delete"
              color="negative"
              glossy
              size="md"
              rounded
              dense
              @click="deleteUser(row.id)"
            >
              <q-tooltip>Supprimer l'utilisateur</q-tooltip>
            </q-btn>
          </div>
        </q-td>
      </template>
    </q-table>

    <q-dialog v-model="dialogUpdateUser">
      <q-card>
        <q-card-section>
          <ManageUserForm
            v-if="userToManage"
            :user="userToManage"
            @submit="submit"
          />
        </q-card-section>
      </q-card>
    </q-dialog>
  </q-page>
</template>
