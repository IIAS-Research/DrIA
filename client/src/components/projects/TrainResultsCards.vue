<script setup lang="ts">
import { computed } from "vue";
import ModelCardContent from "components/projects/ModelCardContent.vue";
import ModelInlineContent from "components/projects/ModelInlineContent.vue";
import { useRouter } from "vue-router";
import { IModel, IProcess, IProject } from "../models";
import { compareMetrics, displayServiceText, formatScore, metricKey } from "src/utils/iaml";
import ProcessCardContent from "./ProcessCardContent.vue";
import { displayServiceName } from 'src/utils/service-label';

const router = useRouter();
const props = defineProps<{
  processes: IProcess[];
  projectId: number;
  isLoading: boolean;
  baselineModel?: IProject["metricsBaseline"];
}>();

const bestModel = computed(() => props.processes
  .flatMap((process) => process.models ?? [])
  .slice().sort((a, b) => compareMetrics(a.metrics, b.metrics))[0]);

function getParentProcessOfModel(model: IModel): IProcess {
  return props.processes.find((process) => process.models?.some((candidate) => candidate.id === model.id))!;
}

function forkPipeline() {
  if (!bestModel.value) return;
  const processId = getParentProcessOfModel(bestModel.value)?.id;
  if (!processId) return;
  router.push({ name: "pipeline-fork", params: { id: props.projectId, processId } });
}

function seeBest() {
  if (!bestModel.value) return;
  const processId = getParentProcessOfModel(bestModel.value)?.id;
  if (!processId) return;
  router.push({ name: "model-show", params: { id: props.projectId, processId, modelId: bestModel.value.id } });
}

const baseline = computed(() => props.baselineModel?.metrics?.[0]);
const sortedMetrics = computed(() => {
  if (!baseline.value) return [];
  const scores = baseline.value.metrics_test ?? baseline.value.metrics_train ?? {};
  return Object.entries(scores)
    .sort((a, b) => Number(b[0] === metricKey(baseline.value!)) - Number(a[0] === metricKey(baseline.value!)));
});
const baselinePending = computed(() => ["QUEUED", "RUNNING"].includes(props.baselineModel?.status?.current ?? ""));
const baselineSource = computed(() => {
  const metrics = baseline.value;
  const greater = metrics?.objective?.greater_is_better ?? (metrics ? metrics.metrics_info?.[metricKey(metrics)]?.greater_is_better : undefined);
  const direction = greater === undefined ? "" : greater ? "Maximize." : "Minimize.";
  const source = metrics?.metrics_test ? "Holdout test set."
    : "Scores on training data, measured in-sample and separate from cross-validation.";
  return `${source} ${direction}`;
});
</script>

<template>
  <div>
    <div class="row q-col-gutter-sm">
      <div class="col-12 col-md-6 result-column" style="align-self: stretch">
        <q-card class="bg-teal-4 full-height result-card">
          <q-card-section>
            <div class="text-h6 text-white">Selected DrIA model</div>

            <ModelCardContent
              v-if="bestModel"
              :process="getParentProcessOfModel(bestModel)"
              :project-id="projectId"
              :model="bestModel"
              :baseline="false"
              display-best-model-name
              class="text-white"
            />
          </q-card-section>

          <q-card-actions align="right" class="model-actions">
            <q-btn
              :loading="isLoading"
              :disable="!bestModel"
              icon="fork_right"
              label="Fork pipeline"
              color="red-4"
              @click="forkPipeline"
            />
            <q-btn
              :loading="isLoading"
              :disable="!bestModel"
              icon="visibility"
              label="See model details"
              color="primary"
              @click="seeBest"
            />
          </q-card-actions>
        </q-card>
      </div>
      <div class="col-12 col-md-6 result-column" style="align-self: stretch">
        <q-card class="bg-grey-4 full-height result-card">
          <q-card-section>
            <div class="text-h6 text-grey-8">Baseline model</div>

            <div class="baseline-description">
              <q-icon name="help" class="q-mb-xs">
                <q-tooltip class="baseline-tooltip">
                  <div>{{ baselineSource }}</div>
                  <div v-if="baselineModel?.error" class="q-mt-xs">{{ displayServiceText(baselineModel.error) }}</div>
                </q-tooltip>
              </q-icon>
              Results of a model without DrIA optimization
            </div>
            <div class="q-mt-md" v-if="baselineModel">
              <q-chip v-if="baselineModel.status && !baseline" :icon="baselinePending ? 'hourglass_top' : ['FINISHED', 'CACHED'].includes(baselineModel.status.current) ? 'check' : 'error'">
                {{ baselineModel.status.current }}
                <q-tooltip v-if="baselineModel.error" class="baseline-tooltip">{{ displayServiceText(baselineModel.error) }}</q-tooltip>
              </q-chip>
              <q-item
                v-for="item in sortedMetrics"
                :key="item[0]"
                clickable
                class="row custom-q-item"
              >
                <q-item-section>
                  <div class="metric-line">
                    <div class="text-weight-medium metric-value">
                      {{ formatScore(item[1]) }}
                    </div>
                    <div class="text-weight-medium metric-label">
                      {{ displayServiceName(baseline?.metrics_info?.[item[0]]?.name ?? item[0]) }}
                    </div>
                  </div>
                </q-item-section>
              </q-item>
            </div>
          </q-card-section>
        </q-card>
      </div>
    </div>

    <q-card v-if="processes?.length" class="q-mt-md">
      <q-card-section class="text-h6"> All training runs </q-card-section>

      <q-list bordered separator>
        <q-expansion-item
          v-for="process in processes.slice().reverse()"
          :key="process.id"
          expand-separator
          header-class="bg-blue-2"
          rev
        >
          <template #header>
            <ProcessCardContent :process="process" :is-loading="isLoading" />
          </template>

          <template v-for="(model, index) in process.models ?? []" :key="model.id">
            <q-card-section :class="index === 0 ? 'bg-green-2' : 'bg-blue-1'">
              <ModelInlineContent
                :process-id="process.id"
                :project-id="projectId"
                :model="model"
              />
            </q-card-section>
            <q-separator />
          </template>
        </q-expansion-item>
      </q-list>
    </q-card>
  </div>
</template>

<style scoped>
.result-column,
.result-card,
.baseline-description {
  min-width: 0;
}

.result-card,
.baseline-description {
  overflow-wrap: anywhere;
}

.model-actions {
  flex-wrap: wrap;
  gap: 8px;
  min-width: 0;
}

.model-actions > .q-btn {
  min-width: 0;
  max-width: 100%;
  margin-left: 0;
}

.model-actions :deep(.q-btn__content) {
  white-space: normal;
  overflow-wrap: anywhere;
}

.custom-q-item {
  min-height: 28px;
  height: auto;
  padding-top: 2px;
  padding-bottom: 2px;
}

.metric-line {
  display: grid;
  grid-template-columns: fit-content(60%) minmax(0, 1fr);
  align-items: baseline;
  gap: 8px;
  min-width: 0;
}

.metric-label,
.metric-value {
  min-width: 0;
  overflow-wrap: anywhere;
}

.metric-label {
  text-align: right;
}

.metric-value {
  font-variant-numeric: tabular-nums;
}

.baseline-tooltip {
  max-width: min(420px, calc(100vw - 32px));
  white-space: pre-wrap;
  overflow-wrap: anywhere;
}
</style>
