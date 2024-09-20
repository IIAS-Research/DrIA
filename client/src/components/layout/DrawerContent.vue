<script setup lang="ts">

const emits = defineEmits<{
    click: [click: Event]
}>();

</script>

<template>
  <div>
    <div class="column q-pa-sm full-width">
      <div
        class="flex justify-end q-gutter-x-sm"
      >
        <q-btn
          color="primary"
          icon="settings"
          to="/manage-users"
        >
          <q-tooltip>
            Administration du site
          </q-tooltip>
        </q-btn>

        <q-btn
          color="negative"
          icon="logout"
          flat
          @click="(event) => emits('click', event)"
        >
          <q-tooltip>
            Se déconnecter
          </q-tooltip>
        </q-btn>
      </div>

      <div>coucou</div>
    </div>
  </div>
</template>
