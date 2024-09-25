<script setup lang="ts">
import jsonImportedFile from 'src/../fake_file_sample/default_pipeline_sample.json';
import { ref } from 'vue';
import { QSplitter, QTab, QTabPanel } from 'quasar';
import { IMetaOrderedStep } from '../models';

const pipeline = ref<IMetaOrderedStep>(jsonImportedFile as IMetaOrderedStep);
const tab = ref<QTabPanel['name']>(`${pipeline.value.children[0].name}-0`);
const splitterModel = ref<QSplitter['modelValue']>(25);

function chooseRightInputType(valueToProceed: any) {
  const valueType = typeof valueToProceed;
  const qInputTypes = ['string', 'number', 'bigint'];
  const qToggleType = ['boolean'];
  if (qInputTypes.includes(valueType)) return 'QInput';
  if (qToggleType.includes(valueType)) return 'QToggle';
  return 'QInput';
}

defineExpose({
  pipeline,
});
</script>

<template>
  <q-card>
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
              class="q-pa-md"
              style="border: 1px solid rgb(0, 0, 0, 0.2);"
              :class="{'bg-grey-4' : stepIndex % 2 === 0}"
            >
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
                    v-if="chooseRightInputType(confObj.value) === 'QInput'"
                    v-model="pipeline.children[panelMetaStepIndex].children[stepIndex]
                      .configuration[key].value"
                    :label="key"
                  />

                  <q-toggle
                    v-if="chooseRightInputType(confObj.value) === 'QToggle'"
                    v-model="pipeline.children[panelMetaStepIndex].children[stepIndex]
                      .configuration[key].value"
                    :label="key"
                    class="q-mt-sm"
                  />
                </div>
              </div>
            </div>
          </q-tab-panel>
        </q-tab-panels>
      </template>
    </q-splitter>
  </q-card>
</template>
