<script setup lang="ts">
import { QCarousel } from "quasar";
import { ref, toRef } from "vue";
import { getModeleExplanationById } from "src/api/models.api";
import { IModel, IPlot } from "../models";

// props
const propsComponent = defineProps<{
  model: IModel;
}>();

// refs
const slide = ref(1);
const carousel = ref<InstanceType<typeof QCarousel>>();
const currentExplainIndex = ref(0);
const isLoadingExplanation = ref(false);
const explanation = toRef(propsComponent.model.explanation);
const currentExplain = ref<IPlot | undefined>(
  propsComponent.model.metrics.plots.at(currentExplainIndex.value)
);

// functions
function changeExplain(index: number) {
  currentExplain.value = propsComponent.model.metrics.plots[index];
  currentExplainIndex.value = index;
  carousel.value?.goTo(currentExplainIndex.value);
}
async function getExplanation() {
  isLoadingExplanation.value = true;
  explanation.value =
    (await getModeleExplanationById(propsComponent.model.id)) ?? [];
  isLoadingExplanation.value = false;
}
</script>

<template>
  <div class="flex column flex-center">
    <div class="flex-center column">
      <h4 class="no-margin q-pb-lg text-weight-medium">
        Importance des variables
      </h4>
      <div v-if="propsComponent.model.explanation === null" class="flex">
        <q-btn
          @click="getExplanation"
          :loading="isLoadingExplanation"
          label="Voir l'explication"
          color="yellow-8"
        />
      </div>

      <div v-else class="flex row items-start justify-center">
        <div class="flex column q-gutter-y-md col-2">
          <q-item
            v-for="item in Object.entries(
              propsComponent.model.metrics.metrics_train
            )"
            :key="item[0]"
            clickable
            class="bg-secondary text-white shadow-3"
            style="width: 300px"
          >
            <q-item-section>
              <div class="flex row items-center justify-between">
                <div class="text-weight-medium">
                  {{ propsComponent.model.metrics.metrics_info[item[0]].name }}
                  :
                </div>
                <div class="text-weight-medium">
                  {{ item[1] }}
                </div>
              </div>
            </q-item-section>
            <q-item-section side>
              <q-icon name="info" color="info">
                <q-tooltip anchor="center right" self="center left">{{
                  propsComponent.model.metrics.metrics_info[item[0]].desc
                }}</q-tooltip>
              </q-icon>
            </q-item-section>
          </q-item>
        </div>

        <div class="q-pl-lg col-7">
          <q-carousel
            v-model="slide"
            :autoplay="false"
            ref="carousel"
            swipeable
            animated
            infinite
            class="carousel-slide rounded-borders q-card"
          >
            <q-carousel-slide
              v-for="(_, index) in propsComponent.model.metrics.plots"
              :key="index"
              :name="index"
              class="flex flex-center"
            >
              <img
                class="full-height"
                :src="
                  'data:image/png;base64,' +
                  propsComponent.model.metrics.plots[index].image
                "
              />
            </q-carousel-slide>
          </q-carousel>
          <q-card class="flex column carousel-card rounded-borders q-pa-md">
            <h6
              class="no-margin q-pt-sm q-pb-sm text-uppercase text-weight-medium"
            >
              {{ currentExplain?.title }}
            </h6>
            <div>
              {{ currentExplain?.description }}
            </div>
            <div class="flex row items-center q-pt-md">
              <div
                class="q-carousel__navigation-inner flex flex-center no-wrap"
              >
                <button
                  v-for="(_, index) in propsComponent.model.metrics.plots"
                  :key="index"
                  @click="changeExplain(index)"
                  class="q-btn q-btn-item non-selectable no-outline q-btn--flat q-btn--round q-btn--actionable q-focusable q-hoverable q-btn--dense q-carousel__navigation-icon"
                  :class="
                    index == currentExplainIndex
                      ? 'q-carousel__navigation-icon--inactive text-grey-7'
                      : 'q-carousel__navigation-icon--active text-grey-4'
                  "
                  tabindex="0"
                  type="button"
                  style="font-size: 10px"
                >
                  <span class="q-focus-helper" />
                  <span
                    class="q-btn__content text-center col items-center q-anchor--skip justify-center row"
                  >
                    <i class="q-icon" aria-hidden="true" role="img">
                      <svg viewBox="0 0 24 24">
                        <path
                          d="M12,2A10,10 0 0,0 2,12A10,10 0 0,0 12,22A10,10 0 0,0 22,12A10,10 0 0,0 12,2Z"
                        />
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
