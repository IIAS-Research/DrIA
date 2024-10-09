<script setup lang="ts">
// import jsonImportedFile from 'src/../fake_file_sample/default_pipeline_sample.json';
import { onMounted, ref } from 'vue';
import { QSplitter, QTab, QTabPanel } from 'quasar';
import { getDefaultPipeline } from 'src/api/ia-web-service.api';
import { IPipeline } from '../models';

const props = defineProps<{
  pipeline: IPipeline;
}>();

const pipelineRefLocalComponent = ref<IPipeline>({ ...props.pipeline });
const tab = ref<QTabPanel['name']>('');
const innerTab = ref<QTabPanel['name']>('');
const splitterModel = ref<QSplitter['modelValue']>(50);
const tabsSplitterModel = ref<QSplitter['modelValue']>(50);

defineExpose({
  pipelineRefLocalComponent,
});

const emits = defineEmits<{
  isPipelineLoading: [pipelineLoadStatus: boolean];
}>();

function chooseRightInputType(valueToProceed: IPipeline['configuration'][0]) {
  const valueType = typeof valueToProceed.value;
  const qInputTypes = ['string', 'number', 'bigint'];
  const qToggleType = ['boolean'];
  if (valueToProceed.categorical?.length) return 'QSelect';
  if (qInputTypes.includes(valueType)) return 'QInput';
  if (qToggleType.includes(valueType)) return 'QToggle';
  return 'QInput';
}

async function initPipeline() {
  // if (props.pipeline) {
  //   pipelineRefLocalComponent.value = { ...props.pipeline };
  //   console.log('la props est la on fetch pas');
  // } else {
  //   console.log('dans le else');
  //   pipelineRefLocalComponent.value = await getDefaultPipeline();
  // }
  if (!pipelineRefLocalComponent.value) return;
  console.log(pipelineRefLocalComponent.value);
  emits('isPipelineLoading', false);
  tab.value = `${pipelineRefLocalComponent.value.children[0].step}-0`;
}

function resetValueWithDefault(stepConf: IPipeline['configuration'][0]) {
  stepConf.value = stepConf.default;
}

function autoSelectFirstChildOnChange(val: QTabPanel['name'] = `${pipelineRefLocalComponent.value?.children[0].step}-0`) {
  innerTab.value = `${pipelineRefLocalComponent.value?.children.find((s, i) => `${s.step}-${i}` === val)?.children[0].step}-0`;
}

onMounted(async () => {
  await initPipeline();
  autoSelectFirstChildOnChange();
});

</script>

<template>
  <q-card v-if="pipelineRefLocalComponent">
    <q-splitter v-model="splitterModel">
      <template #before>
        <q-splitter
          v-model="tabsSplitterModel"
          style="height: 500px;"
        >
          <template #before>
            <q-tabs
              v-model="tab"
              vertical
              class="text-teal full-width"
              active-bg-color="teal-2"
              @update:model-value="autoSelectFirstChildOnChange"
            >
              <q-tab
                v-for="(metaStep, metaIndex) in pipelineRefLocalComponent.children"
                :key="metaIndex"
                :name="`${metaStep.step}-${metaIndex}`"
                :label="metaStep.name"
                :style="{ height: `${(500 / pipelineRefLocalComponent.children.length)}px` }"
              />
            </q-tabs>
          </template>
          <template #after>
            <q-tabs
              v-if="tab"
              v-model="innerTab"
              vertical
              class="text-teal full-width"
              active-bg-color="teal-3"
            >
              <q-tab
                v-for="(step, stepIndex) in pipelineRefLocalComponent?.children
                  ?.filter((s, i) => `${s.step}-${i}` === tab)?.[0]?.children || []"
                :key="stepIndex"
                :name="`${step.step}-${stepIndex}`"
                :label="step.name"
                :style="{ height: `${(500 / pipelineRefLocalComponent.children.length)}px` }"
              />
            </q-tabs>
          </template>
        </q-splitter>
      </template>

      <template #after>
        <q-tab-panels
          v-model="innerTab"
          animated
          swipeable
          vertical
          transition-prev="jump-up"
          transition-next="jump-up"
          class="q-mr-md"
        >
          <q-tab-panel
            v-for="(step, stepIndex) in pipelineRefLocalComponent?.children
              ?.filter((s, i) => `${s.step}-${i}` === tab)?.[0]?.children || []"
            :key="stepIndex"
            :name="`${step.step}-${stepIndex}`"
            class="q-gutter-y-md"
            style="max-height: 500px;"
          >
            <div
              v-if="Object.keys(step.configuration)?.length"
              class="q-pb-sm"
            >
              <q-toggle
                v-model="step.enable"
                :disable="!step.can_be_disable"
                :label="step.enable ? 'Activé' : 'Desactivé'"
                class="q-pr-md"
              />
              <div
                v-for="([key, confObj], confIndex) in Object.entries(step.configuration)"
                :key="confIndex"
              >
                <q-input
                  v-if="chooseRightInputType(confObj) === 'QInput'"
                  v-model="confObj.value"
                  :label="`${key} (Default value: ${confObj.default})
                    ${confObj.range?.length ? 'Min val: ' + confObj.range[0] + ', Max val: ' + confObj.range[1] : ''}`"
                  :disable="!step.enable"
                  :rules="[
                    (val) => confObj.range?.length
                      ? (parseFloat(val) >= confObj.range[0] && parseFloat(val) <= confObj.range[1])
                        || `La valeur n'est pas comprise dans les bornes attendues`
                      : true
                  ]"
                  clearable
                  @clear="resetValueWithDefault(confObj)"
                />

                <q-toggle
                  v-if="chooseRightInputType(confObj) === 'QToggle'"
                  v-model="confObj.value"
                  :label="`${key} (Default value: ${confObj.default})`"
                  :disable="!step.enable"
                  class="q-mt-sm"
                />

                <q-select
                  v-if="chooseRightInputType(confObj) === 'QSelect'"
                  v-model="confObj.value"
                  :options="confObj.categorical?.map(c => c ? c : 'Aucun')"
                  :disable="!step.enable"
                  :label="`${key} (Default value: ${confObj.default})`"
                  filled
                  class="q-my-md"
                  clearable
                  @clear="resetValueWithDefault(confObj)"
                />
              </div>
            </div>
            <div
              v-else
              class="text-italic text-caption"
            >
              Aucune configuration disponible pour cette option
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
    Aucune pipelineRefLocalComponent disponible
  </div>
</template>
