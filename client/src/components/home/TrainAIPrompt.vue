<script setup lang="ts">
import { QForm, QStepper } from 'quasar';
import { ref } from 'vue';
import { parse } from 'papaparse';
import { IProjectForm } from '../models';
import PipelineProject from '../pipeline/PipelineProject.vue';

const emitsComponent = defineEmits<{(e: 'submitProject', projectForm: IProjectForm, file: File): Promise<void>
}>();

const initialProjectInfos: IProjectForm = {
  title: '',
  description: '',
  csvColumnToPredict: '',
  rowDepedencies: false,
  rowDepedenciesLinked: [],
  datasetHasIdColumn: false,
  datasetHasIdColumnList: [],
};
const maxStepNumber = 5;

const uploadedFile = ref<File | null>(null);
const csvFileHeaderColumns = ref<string[]>([]);
const step = ref(1);
const stepper = ref<QStepper>();
const project = ref({ ...initialProjectInfos });
const formSteppers = ref<QForm[]>([]);
const pipelineComponentRef = ref<InstanceType<typeof PipelineProject>>();

function getCsvFileHeaderColumns() {
  if (!uploadedFile.value) return;

  const reader = new FileReader();

  reader.onload = (event) => {
    const csvData = event.target?.result as string;

    // Parse the CSV data
    parse(csvData, {
      header: true,
      complete: (results) => {
        csvFileHeaderColumns.value = results.meta.fields || []; // Extract column names from the CSV
      },
      error: (error: Error) => {
        console.error('Error parsing CSV file:', error);
      },
    });
  };

  reader.readAsText(uploadedFile.value);
}

function beginStepper() {
  getCsvFileHeaderColumns();
}
async function submitForAITrain() {
  if (uploadedFile.value === null || pipelineComponentRef.value === undefined) {
    return;
  }
  project.value.pipeline = pipelineComponentRef.value.pipeline;
  emitsComponent('submitProject', project.value, uploadedFile.value);
}

async function handleNextStepValidation() {
  if (!formSteppers.value.length) return;
  const currentStepIndex = step.value - 1;
  const currentStepperForm = formSteppers.value[currentStepIndex];
  const isFormValid = await currentStepperForm?.validate();
  if (isFormValid) {
    if (step.value + 1 <= maxStepNumber) {
      stepper.value?.next();
    } else {
      currentStepperForm?.submit();
    }
  }
}

</script>

<template>
  <div
    class="q-pt-md"
  >
    <q-stepper
      v-model="step"
      ref="stepper"
      keep-alive
      contracted
      animated
      color="primary"
      style="min-width: 350px;"
      header-class="header-stepper"
    >
      <q-step
        :name="1"
        :done="step > 1"
        title=""
        icon="upload"
      >
        <q-form
          :ref="(el: QForm) => formSteppers[0] = el"
          class="flex column flex-center"
        >
          <q-file
            v-model="uploadedFile"
            class="q-mt-lg"
            name="csv_test_set"
            accept=".csv, application/vnd.openxmlformats-officedocument.spreadsheetml.sheet, application/vnd.ms-excel"
            :max-file-size="10000000"
            label="Sélectionner un fichier .csv"
            clearable
            counter
            dense
            filled
            bg-color="blue"
            label-color="white"
            style="width: 235px;"
            dark
            @update:model-value="beginStepper"
            :rules="[
              (val) => !! val || 'Fichier obligatoire'
            ]"
          >
            <template #prepend>
              <q-icon
                name="upload"
                color="white"
                size="white"
              />
            </template>
          </q-file>
        </q-form>
      </q-step>

      <q-step
        :name="2"
        title=""
        icon="map"
        :done="step > 2"
      >
        <q-form
          :ref="(el: QForm) => formSteppers[1] = el"
          class="q-gutter-y-sm flex-center column"
          @submit="submitForAITrain"
        >
          <div class="text-body1 text-grey-9 text-weight-medium q-pb-lg">
            Dites nous en plus sur votre projet
          </div>
          <q-input
            v-model="project.title"
            filled
            label="Titre"
            dense
            style="width: 220px;"
            :rules="[(v) => v.length > 2 || 'Minimum 3 charactères requis']"
          />
          <q-input
            v-model="project.description"
            filled
            label="Description"
            dense
            style="width: 220px;"
            :rules="[(v) => v.length > 2 || 'Minimum 3 charactères requis']"
          />
        </q-form>
      </q-step>

      <q-step
        :name="3"
        title=""
        icon="psychology"
        :done="step > 3"
      >
        <q-form
          :ref="(el: QForm) => formSteppers[2] = el"
          class="flex-center column"
        >
          <div class="text-body1 text-grey-9 text-weight-medium q-pb-lg text-center">
            Quelle colonne chercher vous à prédire ?
          </div>
          <q-select
            v-model="project.csvColumnToPredict"
            :options="csvFileHeaderColumns"
            dense
            filled
            :rules="[(v) => Boolean(v) || 'Une séléction est requise']"
          />
        </q-form>
      </q-step>

      <q-step
        :name="4"
        title=""
        icon="psychology"
        :done="step > 4"
      >
        <q-form
          :ref="(el: QForm) => formSteppers[3] = el"
          class="flex-center column"
        >
          <div class="text-body1 text-grey-9 text-weight-medium q-pb-lg text-center">
            Est ce que certains lignes peuvent avoir des dépendances entre elle ?
          </div>
          <q-toggle
            v-model="project.rowDepedencies"
            :label="project.rowDepedencies === true ? 'Oui' : 'Non'"
            :left-label="project.rowDepedencies"
          />

          <template v-if="project.rowDepedencies">
            <div class="text-body1 text-grey-9 text-weight-medium q-pb-lg text-center q-pt-xl">
              Si oui, comment reconnaître les lignes dépendantes ?
            </div>
            <q-select
              v-model="project.rowDepedenciesLinked"
              :options="csvFileHeaderColumns"
              dense
              multiple
              use-chips
              filled
              :rules="[(v) => v.length > 0 || 'Une séléction est requise']"
            />
          </template>
        </q-form>
      </q-step>

      <q-step
        :name="5"
        title="Informations sur la prédiction"
        icon="psychology"
        :done="step > 5"
      >
        <q-form
          :ref="(el: QForm) => formSteppers[4] = el"
          class="flex-center column"
          @submit="submitForAITrain"
        >
          <div class="text-body1 text-grey-9 text-weight-medium q-pb-lg text-center">
            Est ce que votre dataset contient des colonnes
            d'identifiants (numéro permettant d'identifier un patient)
          </div>
          <q-toggle
            v-model="project.datasetHasIdColumn"
            :label="project.datasetHasIdColumn === true ? 'Oui' : 'Non'"
            :left-label="project.datasetHasIdColumn"
          />

          <template v-if="project.datasetHasIdColumn">
            <div class="text-body1 text-grey-9 text-weight-medium q-pb-lg text-center q-pt-xl">
              Si oui, quelles colonnes ?
            </div>
            <q-select
              v-model="project.datasetHasIdColumnList"
              :options="csvFileHeaderColumns"
              dense
              filled
              :rules="[(v) => v.length > 0 || 'Une séléction est requise']"
            />
          </template>

          <q-expansion-item
            label="Options avancées"
            class="full-width bg-grey-3"
          >
            <PipelineProject
              ref="pipelineComponentRef"
              class="bg-grey-3"
            />
          </q-expansion-item>
        </q-form>
      </q-step>

      <template #navigation>
        <q-stepper-navigation class="flex justify-around">
          <q-btn
            v-show="step > 1"
            flat
            color="primary"
            @click="stepper?.previous()"
            label="Retour"
            class="q-ml-sm"
          />
          <q-btn
            @click="handleNextStepValidation"
            color="primary"
            :label="step === 2 ? 'Valider' : 'Suivant'"
          />
        </q-stepper-navigation>
      </template>
    </q-stepper>
  </div>
</template>

<style>
.header-stepper {
  display: none;
}
</style>
