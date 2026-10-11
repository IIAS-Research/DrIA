<script setup lang="ts">
import { marked } from 'marked';
import { computed, onUnmounted, ref, watch } from 'vue';
import { api } from 'src/boot/axios';
import { IModel, IStructuredBibliography } from '../models';
import RetainedPipelineOverview from '../pipeline/RetainedPipelineOverview.vue';

const props = defineProps<{ model: IModel }>();
const modelReport = ref<Record<string, unknown>>();
const modelAudit = ref<Record<string, unknown>>();
const loadingReports = ref(false);
const loadingPipeline = ref(false);
const reportsRequested = ref(false);
const reportError = ref('');
const auditError = ref('');
const downloadError = ref('');
const downloading = ref<'model' | 'markdown'>();
let requestVersion = 0;
let downloadVersion = 0;
let reportsController: AbortController | undefined;

function object(value: unknown): value is Record<string, unknown> {
  return value !== null && typeof value === 'object' && !Array.isArray(value);
}
const explanations = computed(() => {
  if (props.model.textExplanation?.length) return props.model.textExplanation;
  return Array.isArray(modelReport.value?.steps)
    ? modelReport.value!.steps.filter((item): item is string => typeof item === 'string') : [];
});
const bibliography = computed<IStructuredBibliography[]>(() => {
  if (props.model.bibliography?.structured_bibliography?.length)
    return props.model.bibliography.structured_bibliography;
  if (!Array.isArray(modelReport.value?.bibliography)) return [];
  return modelReport.value!.bibliography.filter(object).map((item) => ({
    step: typeof item.step === 'string' ? item.step : '',
    year: typeof item.year === 'number' ? item.year : 0,
    name: typeof item.name === 'string' ? item.name : 'Bibliographic reference',
    authors: Array.isArray(item.authors) ? item.authors.filter((author): author is string => typeof author === 'string') : [],
    doi: typeof item.doi === 'string' ? item.doi : null,
    publisher: typeof item.publisher === 'string' ? item.publisher : null,
  }));
});
const technicalDocuments = computed(() => [
  { name: 'Complete model report in JSON', value: modelReport.value },
  { name: 'Complete model audit in JSON', value: modelAudit.value },
].filter((item) => item.value));

function json(value: unknown): string {
  return value === undefined ? '' : JSON.stringify(value, null, 2);
}
function referenceUrl(doi: string | null): string | undefined {
  if (!doi) return undefined;
  const trimmed = doi.trim();
  if (/^10\.\d{4,9}\//.test(trimmed)) return 'https://doi.org/' + trimmed;
  if (/^https?:\/\//i.test(trimmed)) return trimmed;
  return undefined;
}

async function loadReports() {
  if (loadingReports.value || loadingPipeline.value) return;
  const version = ++requestVersion;
  reportsController?.abort();
  reportsController = new AbortController();
  const signal = reportsController.signal;
  const modelId = props.model.id;
  loadingReports.value = true;
  reportsRequested.value = true;
  reportError.value = '';
  auditError.value = '';
  const results = await Promise.allSettled([
    modelReport.value ? Promise.resolve({ data: modelReport.value })
      : api.get(`Models/${modelId}/report`, { params: { format: 'json' }, signal }),
    modelAudit.value ? Promise.resolve({ data: modelAudit.value })
      : api.get(`Models/${modelId}/audit`, { signal }),
  ]);
  if (version !== requestVersion) return;
  const [report, audit] = results;
  if (report.status === 'fulfilled' && object(report.value.data)) modelReport.value = report.value.data;
  else reportError.value = 'The model report is currently unavailable.';
  if (audit.status === 'fulfilled' && object(audit.value.data)) modelAudit.value = audit.value.data;
  else auditError.value = 'The model audit is currently unavailable.';
  loadingReports.value = false;
}

async function download(kind: 'model' | 'markdown') {
  if (downloading.value) return;
  const version = downloadVersion;
  downloading.value = kind;
  downloadError.value = '';
  try {
    const response = await api.get(
      `Models/${props.model.id}/${kind === 'model' ? 'download' : 'report'}`,
      { responseType: 'blob', params: kind === 'markdown' ? { format: 'markdown' } : undefined },
    );
    if (version !== downloadVersion) return;
    const url = URL.createObjectURL(response.data);
    const anchor = document.createElement('a');
    anchor.href = url;
    anchor.download = kind === 'model' ? `dria-model-${props.model.id}.pickle` : `dria-model-${props.model.id}-report.md`;
    document.body.appendChild(anchor);
    anchor.click();
    anchor.remove();
    setTimeout(() => URL.revokeObjectURL(url), 1000);
  } catch {
    if (version === downloadVersion) downloadError.value = 'The download is currently unavailable. Try again.';
  } finally {
    if (version === downloadVersion) downloading.value = undefined;
  }
}
watch([() => props.model.id, () => props.model.metrics.id], () => {
  requestVersion += 1;
  downloadVersion += 1;
  reportsController?.abort();
  modelReport.value = undefined;
  modelAudit.value = undefined;
  loadingReports.value = false;
  loadingPipeline.value = false;
  reportsRequested.value = false;
  reportError.value = '';
  auditError.value = '';
  downloadError.value = '';
  downloading.value = undefined;
});
onUnmounted(() => { requestVersion += 1; downloadVersion += 1; reportsController?.abort(); });
</script>

<template>
  <div class="methodology-content">
    <section class="retained-pipeline" aria-label="Selected pipeline for this model">
      <RetainedPipelineOverview
        :model="model"
        @audit-loaded="modelAudit = $event"
        @report-loaded="modelReport = $event"
        @loading-change="loadingPipeline = $event"
      />
    </section>

    <div class="methodology-columns">
      <section class="method-card">
        <div class="method-card-heading"><q-icon name="account_tree" size="20px" /><h3>Step descriptions</h3></div>
        <div v-if="explanations.length" class="step-descriptions">
          <div v-for="(text, index) in explanations" :key="index" class="markdown-content" v-html="marked(text)" />
        </div>
        <p v-else class="method-empty">No step descriptions were saved for this model. The detailed report may provide more information.</p>
      </section>

      <section class="method-card">
        <div class="method-card-heading"><q-icon name="menu_book" size="20px" /><h3>Method references</h3></div>
        <ol v-if="bibliography.length" class="bibliography-list">
          <li v-for="(reference, index) in bibliography" :key="index">
            <span v-if="reference.step" class="reference-step">{{ reference.step }}</span>
            <a v-if="referenceUrl(reference.doi)" :href="referenceUrl(reference.doi)" target="_blank" rel="noopener noreferrer" class="reference-title">
              {{ reference.name }} <q-icon name="north_east" size="13px" />
            </a>
            <span v-else class="reference-title">{{ reference.name }}</span>
            <p v-if="reference.authors?.length">{{ reference.authors.join(', ') }}</p>
            <div class="reference-publication"><span v-if="reference.year">{{ reference.year }}</span><span v-if="reference.publisher">{{ reference.publisher }}</span></div>
          </li>
        </ol>
        <p v-else class="method-empty">No bibliographic references were saved for this model.</p>
      </section>
    </div>

    <section class="model-exports" aria-label="Download model information">
      <div>
        <h3>Save and share</h3>
        <p>Export the trained model or its report to keep a copy of this result.</p>
      </div>
      <div class="export-actions">
        <q-btn flat no-caps color="primary" icon="download" label="Markdown report" :loading="downloading === 'markdown'" :disable="Boolean(downloading) && downloading !== 'markdown'" @click="download('markdown')" />
        <q-btn outline no-caps color="primary" icon="download" label="Download model" :loading="downloading === 'model'" :disable="Boolean(downloading) && downloading !== 'model'" @click="download('model')" />
      </div>
      <p v-if="downloadError" class="download-error" role="alert">{{ downloadError }}</p>
    </section>

    <section class="full-reports" aria-label="Complete model reports">
      <h3>Complete reports and configuration</h3>
      <p>The report also includes the search configuration, which describes the options available to DrIA. The steps selected for this model are shown in the pipeline above.</p>
      <q-btn
        v-if="!modelReport || !modelAudit"
        outline
        no-caps
        color="primary"
        icon="description"
        :label="reportsRequested && (reportError || auditError) ? 'Try loading again' : 'Load complete reports'"
        :loading="loadingReports"
        :disable="loadingPipeline"
        class="full-reports__load"
        @click="loadReports"
      />
      <p v-if="loadingPipeline && (!modelReport || !modelAudit)" class="full-reports__waiting" role="status">Pipeline information is loading.</p>
      <div v-if="reportError || auditError" class="report-errors" role="status">
        <p v-if="reportError">{{ reportError }}</p>
        <p v-if="auditError">{{ auditError }}</p>
      </div>
      <q-list v-if="technicalDocuments.length" separator class="report-list">
        <q-expansion-item v-for="item in technicalDocuments" :key="item.name" :label="item.name" icon="data_object">
          <pre class="report-json">{{ json(item.value) }}</pre>
        </q-expansion-item>
      </q-list>
    </section>
  </div>
</template>

<style scoped>
.methodology-content { min-width: 0; margin-top: 22px; }
.retained-pipeline { padding: 22px; background: var(--project-surface); border: 1px solid var(--project-border); border-radius: 14px; min-width: 0; }
.report-errors { margin: 0 0 16px; padding: 12px 14px; border: 1px solid var(--project-border); background: var(--project-paper); border-radius: 8px; }
.report-errors p { margin: 0; font-size: 12px; line-height: 1.6; color: var(--project-muted); overflow-wrap: anywhere; }
.report-errors p + p { margin-top: 7px; }
.methodology-columns { display: grid; grid-template-columns: minmax(0, 1.3fr) minmax(0, 1fr); gap: 20px; align-items: start; margin-top: 22px; }
.method-card { padding: 22px; min-width: 0; background: var(--project-surface); border: 1px solid var(--project-border); border-radius: 14px; }
.method-card-heading { display: flex; align-items: center; gap: 9px; margin-bottom: 18px; }
.method-card-heading .q-icon { color: var(--q-primary); flex: 0 0 auto; }
.method-card-heading h3 { margin: 0; font-size: 17px; font-weight: 600; line-height: 1.4; }
.step-descriptions { min-width: 0; }
.markdown-content { font-size: 13px; line-height: 1.7; overflow-wrap: anywhere; min-width: 0; }
.markdown-content + .markdown-content { padding-top: 16px; margin-top: 16px; border-top: 1px solid var(--project-border); }
.markdown-content :deep(h1), .markdown-content :deep(h2), .markdown-content :deep(h3), .markdown-content :deep(h4), .markdown-content :deep(h5), .markdown-content :deep(h6) { font-size: 15px; line-height: 1.5; font-weight: 600; margin: 14px 0 8px; }
.markdown-content :deep(p) { margin: 8px 0; }
.markdown-content :deep(> :first-child) { margin-top: 0; }
.markdown-content :deep(> :last-child) { margin-bottom: 0; }
.markdown-content :deep(ul), .markdown-content :deep(ol) { padding-left: 20px; }
.markdown-content :deep(a) { color: var(--q-primary); }
.markdown-content :deep(pre) { max-width: 100%; overflow: auto; white-space: pre-wrap; padding: 12px; border-radius: 6px; background: var(--project-paper); }
.markdown-content :deep(code) { font-size: 12px; white-space: pre-wrap; overflow-wrap: anywhere; }
.markdown-content :deep(table) { display: block; max-width: 100%; overflow-x: auto; border-collapse: collapse; border-spacing: 0; margin: 12px 0; }
.markdown-content :deep(th), .markdown-content :deep(td) { padding: 8px 10px; border: 1px solid var(--project-border); vertical-align: top; }
.markdown-content :deep(th) { background: var(--project-paper); font-weight: 600; }
.markdown-content :deep(img) { max-width: 100%; height: auto; }
.method-empty { font-size: 13px; line-height: 1.6; color: var(--project-muted); margin: 0; }
.bibliography-list { margin: 0; padding: 0; list-style: none; }
.bibliography-list > li + li { margin-top: 17px; padding-top: 17px; border-top: 1px solid var(--project-border); }
.reference-step { display: block; margin-bottom: 5px; font-size: 11px; color: var(--project-muted); overflow-wrap: anywhere; }
.reference-title { color: var(--project-ink); font-size: 13px; font-weight: 600; line-height: 1.6; overflow-wrap: anywhere; text-decoration: none; }
a.reference-title:hover { color: var(--q-primary); text-decoration: underline; }
.reference-title .q-icon { color: var(--q-primary); }
.bibliography-list p { margin: 7px 0 0; font-size: 12px; line-height: 1.5; color: var(--project-muted); overflow-wrap: anywhere; }
.reference-publication { display: flex; flex-wrap: wrap; gap: 5px 10px; margin-top: 6px; color: var(--project-muted); font-size: 11px; line-height: 1.5; }
.model-exports { display: flex; justify-content: space-between; align-items: center; flex-wrap: wrap; gap: 16px; padding: 22px 0; margin-top: 4px; }
.model-exports > div:first-child { flex: 1 1 280px; min-width: 0; }
.model-exports h3, .full-reports > h3 { margin: 0 0 6px; font-size: 17px; font-weight: 600; line-height: 1.4; }
.model-exports p, .full-reports > p { margin: 0; color: var(--project-muted); font-size: 13px; line-height: 1.6; }
.export-actions { display: flex; flex-wrap: wrap; gap: 8px; }
.export-actions .q-btn { border-radius: 8px; }
.download-error { flex: 0 0 100%; overflow-wrap: anywhere; }
.full-reports > p { margin-bottom: 14px; max-width: 850px; }
.full-reports__load { margin-bottom: 14px; border-radius: 8px; }
.full-reports .full-reports__waiting { font-size: 12px; }
.report-list { border: 1px solid var(--project-border); border-radius: 12px; background: var(--project-surface); overflow: hidden; }
.report-json { padding: 18px; margin: 0; max-height: 480px; overflow: auto; font-size: 12px; line-height: 1.6; white-space: pre-wrap; overflow-wrap: anywhere; background: var(--project-paper); }
@media (max-width: 900px) {
  .methodology-columns { grid-template-columns: minmax(0, 1fr); }
}
@media (max-width: 600px) {
  .retained-pipeline, .method-card { padding: 16px; }
  .model-exports { align-items: stretch; }
  .model-exports > div:first-child, .export-actions { flex: 1 1 100%; }
  .export-actions .q-btn { flex: 1 1 100%; }
}
</style>
