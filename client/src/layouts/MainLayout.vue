<script setup lang="ts">
import { ref } from 'vue';

const leftDrawerOpen = ref(false);
</script>

<template>
  <q-layout view="hHh Lpr fFf">
    <div
      class="flex q-pa-md q-gutter-x-sm cursor-pointer fixed-top-left"
      style="z-index: 2;"
      @click="leftDrawerOpen = !leftDrawerOpen"
    >
      <q-avatar size="lg">
        <q-img src="https://i.pravatar.cc/300" />
      </q-avatar>
      <div class="column justify-center">
        <div class="text-weight-bold">
          Didier Moulard
        </div>
        <div
          class="text-caption text-italic text-grey-8"
          style="line-height: 1rem;"
        >
          Click to show menu
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
    </q-drawer>

    <q-page-container>
      <router-view />
    </q-page-container>
  </q-layout>
</template>
