<script setup lang="ts">
import 'src/css/project-creation.scss';
import { getDefaultPipeline } from 'src/api/ia-web-service.api';
import { getProcessPipeline, runNewProcessWithNewPipeline } from 'src/api/processes.api';
import { IPipeline } from 'src/components/models';
import PipelineProject from 'src/components/pipeline/PipelineProject.vue';
import { computed, onMounted, ref } from 'vue';
import { useRoute, useRouter } from 'vue-router';
import { formatToDDMMYYYHHmm } from 'src/utils/date';

const route = useRoute();
const router = useRouter();
const projectId = parseInt(route.params.id as string, 10);
const processId = parseInt(route.params.processId as string, 10);
const pipelineComponentRef = ref<InstanceType<typeof PipelineProject>>();
const pipeline = ref<IPipeline>();
const running = ref(false);
const loading = ref(true);
const editorLoading = ref(false);
const configurationValid = ref(false);
const sourceDate = computed(() => typeof route.query.sourceDate === 'string' && Number.isFinite(Date.parse(route.query.sourceDate))
  ? formatToDDMMYYYHHmm(route.query.sourceDate) : undefined);
const projectLocation = computed(() => ({
  name: 'project-show', params: { id: projectId }, query: { view: 'configuration', processId: String(processId) },
}));

async function fetchProcessPipeline() {
  loading.value = true;
  configurationValid.value = false;
  try {
    pipeline.value = projectId && processId
      ? await getProcessPipeline(projectId, processId)
      : await getDefaultPipeline();
  } finally {
    loading.value = false;
  }
}

async function runForkedPipeline() {
  if (!pipelineComponentRef.value || running.value || editorLoading.value || !configurationValid.value) return;
  running.value = true;
  try {
    const study = await pipelineComponentRef.value.validateConfiguration();
    if (!study) return;
    const response = await runNewProcessWithNewPipeline({
      projectId, pipeline: study, maxDuration: study.options?.max_duration ?? 60,
    });
    if (response) {
      await router.push({
        name: 'project-show', params: { id: projectId }, query: { view: 'overview', jobId: response.id },
      });
    }
  } finally {
    running.value = false;
  }
}

onMounted(fetchProcessPipeline);
</script>

<template>
  <q-page class="fork-page project-workspace-colors">
    <div class="fork-workspace">
      <q-btn flat dense no-caps icon="arrow_back" label="Retour à la configuration" :to="projectLocation" class="back-link" />

      <header class="fork-heading">
        <span class="section-eyebrow">Nouvel entraînement</span>
        <h1>Configurer les pipelines</h1>
        <p>Composez les chemins que DrIA pourra explorer, puis réglez chaque étape pour la prochaine recherche.</p>
        <span class="source-context"><q-icon name="history" size="16px" />Configuration de l’entraînement nº {{ processId }}<template v-if="sourceDate"> · {{ sourceDate }}</template></span>
      </header>

      <template v-if="pipeline">
        <section class="configuration-section" aria-label="Éditeur des pipelines">
          <div class="editor-frame" :class="{ 'editor-running': running }">
            <PipelineProject
              ref="pipelineComponentRef"
              :pipeline="pipeline"
              @validity-change="configurationValid = $event"
              @is-pipeline-loading="editorLoading = $event"
            />
          </div>
        </section>

        <footer class="fork-actions">
          <div>
            <strong>Prêt à lancer un nouvel entraînement ?</strong>
            <p v-if="!configurationValid">Complétez les réglages signalés dans l’éditeur avant de poursuivre.</p>
            <p v-else>DrIA vérifiera cette configuration, puis ajoutera les résultats au projet.</p>
          </div>
          <q-btn
            unelevated no-caps color="primary" icon="play_arrow" label="Lancer le nouvel entraînement"
            :loading="running" :disable="loading || editorLoading || !configurationValid"
            @click="runForkedPipeline"
          />
        </footer>
      </template>
      <div v-else-if="loading" class="fork-state" role="status">
        <q-spinner color="primary" size="32px" /><p>Chargement de la configuration…</p>
      </div>
      <div v-else class="fork-state">
        <q-icon name="cloud_off" size="32px" /><h2>La configuration n’a pas pu être chargée</h2>
        <q-btn outline no-caps color="primary" icon="refresh" label="Réessayer" @click="fetchProcessPipeline" />
      </div>
    </div>
  </q-page>
</template>

<style scoped>
.fork-page { padding: 28px 32px 48px; color: var(--project-ink); }
.fork-workspace { width: 100%; max-width: 1440px; min-width: 0; margin: 0 auto; }
.back-link { color: var(--q-primary); margin-bottom: 22px; }
.section-eyebrow { color: var(--q-primary); font-size: 11px; font-weight: 700; letter-spacing: .09em; text-transform: uppercase; }
.fork-heading h1 { margin: 8px 0 10px; font-size: clamp(26px, 2.8vw, 34px); line-height: 1.2; font-weight: 600; letter-spacing: -.025em; }
.fork-heading > p { margin: 0; color: var(--project-muted); font-size: 14px; line-height: 1.6; max-width: 830px; }
.source-context { display: flex; align-items: center; gap: 7px; margin-top: 16px; font-size: 12px; color: var(--project-muted); }
.source-context .q-icon { color: var(--q-primary); }
.configuration-section { margin-top: 24px; min-width: 0; }
.editor-frame { min-width: 0; max-width: 100%; border: 1px solid var(--project-border); border-radius: 14px; background: var(--project-surface); }
.editor-frame :deep(.study-editor) { box-shadow: none; border: 0; border-radius: inherit; }
.editor-running { pointer-events: none; opacity: .7; }
.fork-actions { display: flex; align-items: center; justify-content: space-between; gap: 24px; margin-top: 22px; padding: 22px 0; border-top: 1px solid var(--project-border); }
.fork-actions > div { min-width: 0; }
.fork-actions strong { font-size: 14px; font-weight: 600; line-height: 1.5; }
.fork-actions p { margin: 5px 0 0; font-size: 12px; line-height: 1.6; color: var(--project-muted); }
.fork-actions .q-btn { flex-shrink: 0; border-radius: 9px; min-height: 44px; }
.fork-state { padding: 70px 20px; text-align: center; color: var(--project-muted); }
.fork-state h2 { font-size: 20px; line-height: 1.4; font-weight: 600; color: var(--project-ink); }
@media (max-width: 800px) {
  .fork-page { padding: 20px 16px 36px; }
  .fork-actions { flex-direction: column; align-items: stretch; gap: 18px; }
}
</style>
