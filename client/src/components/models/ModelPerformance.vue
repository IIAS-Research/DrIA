<script setup lang="ts">
import { QCarousel } from "quasar";
import { ref } from "vue";
import { IModel } from "../models";

// props
const propsComponent = defineProps<{
  model: IModel;
}>();

// refs
const slide = ref(1);
const carousel = ref<InstanceType<typeof QCarousel>>();
</script>

<template>
  <div class="flex column">
    <h4 class="no-margin q-py-lg text-weight-medium">Performances</h4>
    <div class="flex row items-start">
      <div class="flex column q-gutter-y-sm col-2">
        <q-item
          v-for="item in Object.entries(
            propsComponent.model.metrics.metrics_train
          )"
          :key="item[0]"
          clickable
          class="bg-grey-4"
          style="width: 300px"
        >
          <q-item-section>
            <div class="flex row items-center justify-between">
              <div class="text-weight-medium">{{ item[0] }} :</div>
              <div class="text-weight-medium">{{ item[1] }}</div>
            </div>
          </q-item-section>
          <q-item-section side>
            <q-icon name="info" color="blue">
              <!-- <q-tooltip>Surement du texte</q-tooltip> -->
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
        >
          <q-carousel-slide
            :name="1"
            img-src="https://cdn.quasar.dev/img/mountains.jpg"
          />
        </q-carousel>
        <div class="flex column carousel-card rounded-borders q-pa-md">
          <h6 class="no-margin q-pt-sm text-uppercase text-weight-medium">
            Titre du graphique
          </h6>
          <div>
            Lorem Ipsum is simply dummy text of the printing and typesetting
            industry. Lorem Ipsum has been the industry's standard dummy text
            ever since the 1500s, when an unknown printer took a galley of type
            and scrambled it to make a type specimen book. It has survived not
            only five centuries, but also the leap into electronic typesetting,
            remaining essentially unchanged.
          </div>
          <div class="flex row items-center q-pt-md">
            <div class="q-carousel__navigation-inner flex flex-center no-wrap">
              <button
                @click="console.log('la bibite')"
                class="q-btn q-btn-item non-selectable no-outline q-btn--flat q-btn--round q-btn--actionable q-focusable q-hoverable q-btn--dense q-carousel__navigation-icon"
                :class="
                  true
                    ? 'q-carousel__navigation-icon--inactive'
                    : 'q-carousel__navigation-icon--active'
                "
                tabindex="0"
                type="button"
                style="font-size: 10px"
              >
                <span class="q-focus-helper"></span>
                <span
                  class="q-btn__content text-center col items-center q-anchor--skip justify-center row"
                >
                  <i class="q-icon" aria-hidden="true" role="img">
                    <svg viewBox="0 0 24 24">
                      <path
                        d="M12,2A10,10 0 0,0 2,12A10,10 0 0,0 12,22A10,10 0 0,0 22,12A10,10 0 0,0 12,2Z"
                      ></path>
                    </svg>
                  </i>
                </span>
              </button>
            </div>
          </div>
        </div>
      </div>
    </div>
  </div>
</template>

<style scoped>
.carousel-card {
  border: 1px solid #bbb;
}
</style>
