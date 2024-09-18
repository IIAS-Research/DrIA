<script setup lang="ts">
import { QForm, QStepper } from 'quasar';
import { ref } from 'vue';
import { parse } from 'papaparse';

const initialProjectInfos = {
  titre: '',
  description: '',
  csvColumnToPredict: '',
};

const uploadedFile = ref<File | null>(null);
const csvFileHeaderColumns = ref<string[]>([]);
const step = ref(1);
const maxStep = ref(2);
const stepper = ref<QStepper>();
const stepperDialog = ref(false);
const project = ref({ ...initialProjectInfos });

const formSteppers = ref<QForm[]>([]);

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
  stepperDialog.value = true;
  getCsvFileHeaderColumns();
}

function resetStepper() {
  stepper.value?.goTo(1);
  uploadedFile.value = null;
  csvFileHeaderColumns.value = [];
  project.value = { ...initialProjectInfos };
}

async function submitForAITrain() {
  console.log('submitted');
  // await
}

async function handleNextStepValidation() {
  if (!formSteppers.value?.length) return;
  const currentStepIndex = step.value - 1;
  const currentStepperForm = formSteppers.value[currentStepIndex];
  const isFormValid = await currentStepperForm?.validate();
  if (isFormValid) {
    if (step.value + 1 <= maxStep.value) {
      stepper.value?.next();
    } else {
      console.log('submit for train to IA');
      resetStepper();
      stepperDialog.value = false;
    }
  }
}

</script>

<template>
  <div class="flex flex-center">
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
    >
      <template #prepend>
        <q-icon
          name="upload"
          color="white"
          size="white"
        />
      </template>
    </q-file>

    <q-dialog
      v-model="stepperDialog"
      @before-hide="resetStepper"
    >
      <q-card style="max-width: 1200px; max-width: 80vw;">
        <q-stepper
          v-model="step"
          ref="stepper"
          color="primary"
          animated
        >
          <q-step
            :name="1"
            title="Dites nous en plus sur votre projet"
            icon="map"
            :done="step > 1"
          >
            <q-form
              :ref="(el: QForm) => formSteppers[0] = el"
              class="q-gutter-y-sm"
              @submit="submitForAITrain"
            >
              <q-input
                v-model="project.titre"
                filled
                label="Titre du projet"
                dense
                :rules="[(v) => v.length > 2 || 'Minimum 3 charactères requis']"
              />
              <q-input
                v-model="project.description"
                filled
                label="Description du projet"
                dense
                :rules="[(v) => v.length > 2 || 'Minimum 3 charactères requis']"
              />
            </q-form>
          </q-step>

          <q-step
            :name="2"
            title="Informations sur la prédiction"
            icon="psychology"
            :done="step > 2"
          >
            <q-form :ref="(el: QForm) => formSteppers[1] = el">
              <q-select
                v-model="project.csvColumnToPredict"
                :options="csvFileHeaderColumns"
                label="Colonne à prédire"
                dense
                filled
                :rules="[(v) => Boolean(v) || 'Une séléction est requise']"
              />
            </q-form>
          </q-step>

          <template #navigation>
            <q-stepper-navigation>
              <q-btn
                @click="handleNextStepValidation"
                color="primary"
                :label="step === 2 ? 'Valider' : 'Suivant'"
              />
              <q-btn
                v-if="step > 1"
                flat
                color="primary"
                @click="stepper?.previous()"
                label="Retour"
                class="q-ml-sm"
              />
            </q-stepper-navigation>
          </template>
        </q-stepper>
      </q-card>
    </q-dialog>
  </div>
</template>
