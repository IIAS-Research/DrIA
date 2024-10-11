<script setup lang="ts">
import { useSignalR } from "src/utils/useSignalR";
import { QCarousel, useQuasar } from "quasar";
import { ref, watchEffect, onUnmounted, onMounted } from "vue";
import { getModeleById, startModeleExplanationById } from "src/api/models.api";
import { IExplanation, IModel, IPlot, ProcessStatus } from "../models";

// props
const propsComponent = defineProps<{
  model: IModel;
}>();

// consts
const signalR = useSignalR("explainModel");
const $q = useQuasar();

// refs
const slide = ref(1);
const carousel = ref<InstanceType<typeof QCarousel>>();
const currentExplainIndex = ref(0);
const isLoadingExplanation = ref(false);
const currentExplain = ref<IPlot | undefined>(
  propsComponent.model.metrics.plots.at(currentExplainIndex.value)
);
const explanation = ref<IExplanation>();

// functions
function changeExplain(index: number) {
  currentExplain.value = propsComponent.model.metrics.plots[index];
  currentExplainIndex.value = index;
  carousel.value?.goTo(currentExplainIndex.value);
}
async function getExplanation() {
  isLoadingExplanation.value = true;
  try {
    const response = await startModeleExplanationById(propsComponent.model.id);
    if (!response?.id) {
      return;
    }
    signalR.connect(response.id, propsComponent.model.id);
  } catch (error) {
    console.error(error);
    $q.notify({
      type: "negative",
      message: "Une erreur est survenue",
    });
  }
}

// watchs
watchEffect(async () => {
  const messageReceived = signalR.receivedMessageRef.value as ProcessStatus;
  if (messageReceived !== "FINISHED") return;
  signalR.disconnect();
  isLoadingExplanation.value = false;
  const responseExplain = await getModeleById(propsComponent.model.id);
  explanation.value = JSON.parse(
    responseExplain?.explanation ?? ""
  ) as IExplanation;
});

// lifeCycle
onMounted(() => {
  if (propsComponent.model.explanation) {
    explanation.value = JSON.parse(
      propsComponent.model.explanation
    ) as IExplanation;
  }
});
onUnmounted(() => {
  signalR.disconnect();
});
</script>

<template>
  <div class="flex column flex-center">
    <div class="flex-center column">
      <h4 class="no-margin q-pb-lg text-weight-medium">
        Importance des variables
      </h4>
      <div v-if="!explanation" class="flex">
        <q-btn @click="getExplanation" :loading="isLoadingExplanation" label="Voir l'explication" color="yellow-8" />
      </div>

      <div v-else class="flex row items-start justify-center">
        <div class="flex column q-gutter-y-md col-2">
          <q-item v-for="item in Object.entries(explanation.plots)" :key="item[0]" clickable
            class="bg-secondary text-white shadow-3" style="width: 300px">
            <q-item-section>
              <div class="flex row items-center justify-between">
                <div class="text-weight-medium">
                  {{ item[0] }}
                  :
                </div>
                <div class="text-weight-medium">
                  {{ item[1].title }}
                </div>
              </div>
            </q-item-section>
          </q-item>
        </div>

        <div class="q-pl-lg col-7">
          <q-carousel v-model="slide" :autoplay="false" ref="carousel" swipeable animated infinite
            class="carousel-slide rounded-borders q-card">
            <q-carousel-slide v-for="(_, index) in propsComponent.model.metrics.plots" :key="index" :name="index"
              class="flex flex-center">
              <img class="full-height" :src="'data:image/png;base64,' +
        propsComponent.model.metrics.plots[currentExplainIndex].image
        " />
            </q-carousel-slide>
          </q-carousel>
          <q-card class="flex column carousel-card rounded-borders q-pa-md">
            <h6 class="no-margin q-pt-sm q-pb-sm text-uppercase text-weight-medium">
              {{ currentExplain?.title }}
            </h6>
            <div>
              {{ currentExplain?.description }}
            </div>
            <div class="flex row items-center q-pt-md">
              <div class="q-carousel__navigation-inner flex flex-center no-wrap">
                <button v-for="(_, index) in propsComponent.model.metrics.plots" :key="index"
                  @click="changeExplain(index)"
                  class="q-btn q-btn-item non-selectable no-outline q-btn--flat q-btn--round q-btn--actionable q-focusable q-hoverable q-btn--dense q-carousel__navigation-icon"
                  :class="index == currentExplainIndex
        ? 'q-carousel__navigation-icon--inactive text-grey-7'
        : 'q-carousel__navigation-icon--active text-grey-4'
        " tabindex="0" type="button" style="font-size: 10px">
                  <span class="q-focus-helper" />
                  <span class="q-btn__content text-center col items-center q-anchor--skip justify-center row">
                    <i class="q-icon" aria-hidden="true" role="img">
                      <svg viewBox="0 0 24 24">
                        <path d="M12,2A10,10 0 0,0 2,12A10,10 0 0,0 12,22A10,10 0 0,0 22,12A10,10 0 0,0 12,2Z" />
                      </svg>
                    </i>
                  </span>
                </button>
              </div>
            </div>
          </q-card>
        </div>
      </div>
    </div>
  </div>
</template>

<style scoped>
.carousel-card {
  border: 1px solid #bbb;
}

.carousel-slide {
  border-left: 1px solid #bbb;
  border-right: 1px solid #bbb;
  border-top: 1px solid #bbb;
}
</style>
