<script setup lang="ts">
// import jsonImportedFile from 'src/../fake_file_sample/default_pipeline_sample.json';
import { onMounted, ref } from 'vue';
import { QSplitter, QTab, QTabPanel } from 'quasar';
import { getDefaultPipeline } from 'src/api/ia-web-service.api';
import { IMetaOrderedStep, IStep } from '../models';

const pipeline = ref<IMetaOrderedStep>();
const tab = ref<QTabPanel['name']>();
const splitterModel = ref<QSplitter['modelValue']>(25);

defineExpose({
  pipeline,
});

function chooseRightInputType(valueToProceed: IStep['configuration'][0]) {
  const valueType = typeof valueToProceed.value;
  const qInputTypes = ['string', 'number', 'bigint'];
  const qToggleType = ['boolean'];
  if (valueToProceed.categorical?.length) return 'QSelect';
  if (qInputTypes.includes(valueType)) return 'QInput';
  if (qToggleType.includes(valueType)) return 'QToggle';
  return 'QInput';
}

async function initPipeline() {
  pipeline.value = await getDefaultPipeline();
  if (!pipeline.value) return;
  tab.value = `${pipeline.value.children[0].name}-0`;
}

function resetValueWithDefault(stepConf: IStep['configuration'][0]) {
  stepConf.value = stepConf.default;
}

onMounted(async () => {
  await initPipeline();
});
</script>

<template>
  <q-card v-if="pipeline">
    <q-splitter
      v-model="splitterModel"
      style="width: 1000px; height: 500px"
    >
      <template #before>
        <q-tabs
          v-model="tab"
          vertical
          class="text-teal"
          active-bg-color="teal-2"
        >
          <q-tab
            v-for="(metaStep, metaIndex) in pipeline.children"
            :key="metaIndex"
            :name="`${metaStep.name}-${metaIndex}`"
            :label="`${metaStep.name}-${metaIndex}`"
            :style="{height: `${(500 / pipeline.children.length)}px`}"
          />
        </q-tabs>
      </template>

      <template #after>
        <q-tab-panels
          v-model="tab"
          animated
          swipeable
          vertical
          transition-prev="jump-up"
          transition-next="jump-up"
        >
          <q-tab-panel
            v-for="(panelMetaStep, panelMetaStepIndex) in pipeline.children"
            :key="panelMetaStepIndex"
            :name="`${panelMetaStep.name}-${panelMetaStepIndex}`"
            class="q-gutter-y-md"
          >
            <div
              v-for="(step, stepIndex) in panelMetaStep.children"
              :key="stepIndex"
              class="q-pa-md relative-position"
              style="border: 1px solid rgb(0, 0, 0, 0.2);"
              :class="{'bg-grey-4' : stepIndex % 2 === 0}"
            >
              <q-toggle
                v-model="pipeline.children[panelMetaStepIndex].children[stepIndex].enable"
                :disable="!pipeline.children[panelMetaStepIndex].children[stepIndex].can_be_disable"
                :label="pipeline.children[panelMetaStepIndex].children[stepIndex].enable ? 'Activé' : 'Desactivé'"
                class="absolute-top-right q-pr-md"
              />
              <div class="text-subtitle2 q-mb-xs">
                {{ step.name }}
              </div>
              <p>{{ step.description }}</p>

              <div v-if="Object.keys(step.configuration)?.length">
                <div
                  v-for="([key, confObj], confIndex) in Object.entries(step.configuration)"
                  :key="confIndex"
                >
                  <q-input
                    v-if="chooseRightInputType(confObj) === 'QInput'"
                    v-model="pipeline.children[panelMetaStepIndex].children[stepIndex]
                      .configuration[key].value"
                    :label="`${key} (Default value: ${confObj.default})`"
                    :disable="!pipeline.children[panelMetaStepIndex].children[stepIndex].enable"
                    clearable
                    @clear="resetValueWithDefault(confObj)"
                  />

                  <q-toggle
                    v-if="chooseRightInputType(confObj) === 'QToggle'"
                    v-model="pipeline.children[panelMetaStepIndex].children[stepIndex]
                      .configuration[key].value"
                    :label="`${key} (Default value: ${confObj.default})`"
                    :disable="!pipeline.children[panelMetaStepIndex].children[stepIndex].enable"
                    class="q-mt-sm"
                  />

                  <q-select
                    v-if="chooseRightInputType(confObj) === 'QSelect'"
                    v-model="pipeline.children[panelMetaStepIndex].children[stepIndex]
                      .configuration[key].value"
                    :options="pipeline.children[panelMetaStepIndex].children[stepIndex]
                      .configuration[key].categorical"
                    :disable="!pipeline.children[panelMetaStepIndex].children[stepIndex].enable"
                    :label="`${key} (Default value: ${confObj.default})`"
                    filled
                    class="q-my-md"
                    clearable
                    @clear="resetValueWithDefault(confObj)"
                  />
                </div>
              </div>
            </div>
          </q-tab-panel>
        </q-tab-panels>
      </template>
    </q-splitter>
  </q-card>
  <div
    class="text-caption text-italic"
    v-else
  >
    Aucune pipeline disponible
  </div>
</template>

<!-- TODO: Categorial -> Q-Select, Range -> input avec lazy rules -->
