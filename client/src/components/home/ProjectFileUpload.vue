<script setup lang="ts">
import { computed, ref } from 'vue';

const props = defineProps<{
  loading?: boolean;
  disabled?: boolean;
  error?: string;
  filename?: string;
}>();
const emit = defineEmits<{
  (event: 'select-file', file: File): void;
  (event: 'reject-file', message: string): void;
  (event: 'cancel'): void;
}>();
const fileInput = ref<HTMLInputElement>();
const dragDepth = ref(0);
const dragging = computed(() => dragDepth.value > 0 && !props.disabled && !props.loading);

function selectFiles(files: FileList | null) {
  if (props.disabled || props.loading || !files?.length) return;
  if (files.length !== 1) {
    emit('reject-file', 'Choose one file to start your study.');
    return;
  }
  if (!/\.csv$/i.test(files[0].name)) {
    emit('reject-file', 'Choose a CSV file.');
    return;
  }
  emit('select-file', files[0]);
}

function chooseFile(event: Event) {
  const input = event.target as HTMLInputElement;
  selectFiles(input.files);
  input.value = '';
}

function dragEnter(event: DragEvent) {
  if (event.dataTransfer?.types.includes('Files')) dragDepth.value += 1;
}

function dropFile(event: DragEvent) {
  dragDepth.value = 0;
  selectFiles(event.dataTransfer?.files ?? null);
}
</script>

<template>
  <div class="import-entry">
    <div class="import-introduction">
      <div class="import-eyebrow">New study</div>
      <h1>Start with <span>your data</span></h1>
      <p>Upload your file. DrIA will guide you through each step.</p>
    </div>

    <div
      class="import-dropzone"
      :class="{ 'is-dragging': dragging, 'is-reading': loading }"
      :aria-busy="loading ? 'true' : 'false'"
      @dragenter.prevent="dragEnter"
      @dragover.prevent
      @dragleave.prevent="dragDepth = Math.max(0, dragDepth - 1)"
      @drop.prevent="dropFile"
    >
      <input
        ref="fileInput"
        type="file"
        accept=".csv,text/csv"
        class="file-input"
        tabindex="-1"
        aria-label="Upload my data"
        :disabled="disabled || loading"
        @change="chooseFile"
      >
      <div class="import-icon" aria-hidden="true">
        <q-spinner v-if="loading" color="secondary" size="30px" />
        <q-icon v-else :name="dragging ? 'file_download' : 'upload_file'" size="30px" />
      </div>
      <template v-if="loading">
        <h2>Reading your data…</h2>
        <p class="import-file-name">{{ filename }}</p>
        <q-btn flat no-caps color="primary" label="Cancel" :disable="disabled" @click="emit('cancel')" />
      </template>
      <template v-else>
        <h2>{{ dragging ? 'Drop your file to get started' : 'Drop your CSV file here' }}</h2>
        <p>or choose it from your computer</p>
        <q-btn
          unelevated
          no-caps
          color="primary"
          label="Choose a file"
          class="import-button"
          :disable="disabled"
          @click="fileInput?.click()"
        />
      </template>
      <div class="import-format">CSV · Up to 100 MB</div>
    </div>

    <div v-if="error" class="import-error" role="alert">
      <q-icon name="error_outline" size="18px" />
      <span>{{ error }}</span>
    </div>
    <p class="import-guidance">One observation per row, a name for each column.<br>Next, choose what the model should predict.</p>
  </div>
</template>

<style scoped lang="scss">
.import-entry { width: 100%; max-width: 620px; margin: 0 auto; text-align: center; }
.import-introduction { margin-bottom: 30px; }
.import-eyebrow { color: var(--q-primary); font-size: 11px; font-weight: 500; letter-spacing: .16em; text-transform: uppercase; }
.import-introduction h1 { margin: 12px 0; color: var(--project-ink); font-size: clamp(26px, 3vw, 34px); font-weight: 500; line-height: 1.3; letter-spacing: -.025em; }
.import-introduction h1 span { color: var(--q-secondary); }
.import-introduction p { max-width: 430px; margin: 0 auto; color: var(--project-muted); font-size: 14px; line-height: 1.7; }
.import-dropzone { position: relative; padding: 32px 24px 24px; border: 1px dashed var(--project-border); border-radius: 18px; background: var(--project-paper); box-shadow: 0 12px 40px var(--project-shadow); transition: border-color .18s ease, background-color .18s ease, box-shadow .18s ease; }
.import-dropzone.is-dragging { border-color: var(--q-primary); background: var(--project-selection-surface); box-shadow: 0 0 0 4px var(--project-shadow); }
.import-icon { display: flex; align-items: center; justify-content: center; width: 64px; height: 64px; margin: 0 auto 20px; border-radius: 18px; background: var(--project-brand); color: var(--project-ink); }
.import-dropzone h2 { margin: 0; color: var(--project-ink); font-size: 18px; font-weight: 500; line-height: 1.5; }
.import-dropzone p { margin: 6px 0 22px; color: var(--project-muted); font-size: 13px; line-height: 1.5; }
.import-button { min-height: 44px; padding: 0 26px; border-radius: 7px; font-size: 14px; }
.import-format { margin-top: 22px; color: var(--project-muted); font-size: 11px; }
.import-dropzone .import-file-name { overflow-wrap: anywhere; }
.import-guidance { margin: 20px 0 0; color: var(--project-muted); font-size: 12px; line-height: 1.8; }
.import-error { display: flex; align-items: flex-start; justify-content: center; gap: 8px; margin-top: 16px; color: var(--q-negative); font-size: 13px; line-height: 1.6; }
.import-error .q-icon { flex-shrink: 0; margin-top: 2px; }
.file-input { position: absolute; width: 1px; height: 1px; margin: -1px; padding: 0; overflow: hidden; clip: rect(0, 0, 0, 0); white-space: nowrap; border: 0; }
@media (max-width: 599px) {
  .import-introduction { margin-bottom: 24px; }
  .import-introduction p { font-size: 13px; }
  .import-dropzone { padding: 26px 16px 22px; border-radius: 14px; }
  .import-dropzone h2 { font-size: 16px; }
}
@media (prefers-reduced-motion: reduce) {
  .import-dropzone { transition: none; }
}
</style>
