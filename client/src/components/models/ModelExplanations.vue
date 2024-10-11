<script setup lang="ts">
import { marked } from 'marked';
import { IModel } from '../models';

// props
const propsComponent = defineProps<{
  model: IModel;
}>();

</script>

<template>
  <div class="flex column flex-center">
    <div class="flex row items-start justify-center">
      <div class="flex-center column col-5 q-mr-md">
        <h4 class="no-margin q-py-lg text-weight-medium">
          Explication des traitements
        </h4>

        <q-card class="q-pa-md q-mt-xs flex column">
          <div
            v-for="(text, index) in propsComponent.model.textExplanation"
            :key="index"
          >
            <div v-html="marked(text)" />
          </div>
        </q-card>
      </div>

      <div class="flex-center column col-5 q-ml-md">
        <h4 class="no-margin q-py-lg text-weight-medium">
          Bibliographie
        </h4>
        <q-list class="q-gutter-y-md">
          <q-item
            v-for="(biblio, index) in propsComponent.model.bibliography
              ?.structured_bibliography"
            :key="index"
            clickable
            class="bg-grey-1 shadow-2"
          >
            <div class="flex column">
              <div class="flex row items-center">
                <div class="text-body1 text-weight-medium">
                  Name :
                </div>
                <div class="q-pl-sm">
                  {{ biblio.name }}
                </div>
              </div>
              <div class="flex row items-center">
                <div class="text-body1 text-weight-medium">
                  Year :
                </div>
                <div class="q-pl-sm">
                  {{ biblio.year }}
                </div>
              </div>
              <div class="flex row items-center">
                <div class="text-body1 text-weight-medium">
                  Step :
                </div>
                <div class="q-pl-sm">
                  {{ biblio.step }}
                </div>
              </div>
              <div class="flex row items-center">
                <div class="text-body1 text-weight-medium">
                  DOI :
                </div>
                <div class="q-pl-sm">
                  {{ biblio.doi ?? "???" }}
                </div>
              </div>
              <div class="flex row items-center">
                <div class="text-body1 text-weight-medium">
                  Author :
                </div>
                <div class="q-pl-sm">
                  {{ biblio.author ?? "???" }}
                </div>
              </div>
              <div class="flex row items-center">
                <div class="text-body1 text-weight-medium">
                  Publisher :
                </div>
                <div class="q-pl-sm">
                  {{ biblio.publisher ?? "???" }}
                </div>
              </div>
            </div>
          </q-item>
        </q-list>
      </div>
    </div>
  </div>
</template>

<style>
h2 {
  font-size: 30px;
  margin: 10px 0px !important;
  font-weight: 400;
}
h3 {
  font-size: 26px;
  margin: 10px 0px !important;
  font-weight: 400;
}
th {
  border: 1px solid #000;
  padding: 10px;
  background-color: #c7f1ff;
}
td {
  border: 1px solid #000;
  padding: 10px;
  vertical-align: top;
}
table {
  border-spacing: 2px;
}
</style>
