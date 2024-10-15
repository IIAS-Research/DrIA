<template>
  <q-page padding>
    <div class="flex-center column q-gutter-y-md">
      <q-btn
        color="orange"
        size="lg"
        label="Gestion utilisateur"
        :to="{ name: 'admin-users' }"
        icon="group"
      />

      <q-btn
        color="green"
        size="lg"
        label="Gestion projet"
        :to="{ name: 'admin-projects' }"
        icon="rocket_launch"
      />

      <q-btn
        color="blue"
        size="lg"
        label="Visualisation pulling"
        :to="{ name: 'admin-pulling' }"
        icon="sync"
      />
    </div>
  </q-page>
</template>
