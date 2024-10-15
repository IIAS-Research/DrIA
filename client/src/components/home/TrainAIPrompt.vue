<script setup lang="ts">
import { QForm, QStepper } from 'quasar';
import { onMounted, ref } from 'vue';
import { parse } from 'papaparse';
import { getDefaultPipeline } from 'src/api/ia-web-service.api';
import { IPipeline, IProjectForm } from '../models';
import PipelineProject from '../pipeline/PipelineProject.vue';
import ExplainWord from '../utils/ExplainWord.vue';

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
  survivalModel: false,
};
const maxStepNumber = 6;

const uploadedFile = ref<File | null>(null);
const csvFileHeaderColumns = ref<string[]>([]);
const step = ref(1);
const stepper = ref<QStepper>();
const project = ref({ ...initialProjectInfos });
const formSteppers = ref<QForm[]>([]);
const pipelineComponentRef = ref<InstanceType<typeof PipelineProject>>();
const isPipelineLoading = ref(true);
const pipeline = ref<IPipeline>();

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
  project.value.pipeline = pipelineComponentRef.value.pipelineRefLocalComponent;
  emitsComponent('submitProject', project.value, uploadedFile.value);
}

async function handleNextStepValidation() {
  if (!formSteppers.value.length) return;
  if (step.value > 3 && !project.value.csvColumnToPredict) return; // Stop here if no selection is made
  console.log('ici');
  const currentStepIndex = step.value - 1;
  const currentStepperForm = formSteppers.value[currentStepIndex];
  if (
    currentStepIndex === 2
  && project.value.survivalModel
  && !project.value.survivalModeldatasetTargetTime
  ) {
    return;
  }
  const isFormValid = await currentStepperForm?.validate();
  console.log(isFormValid);
  if (isFormValid) {
    if (step.value + 1 <= maxStepNumber) {
      stepper.value?.next();
    } else {
      currentStepperForm?.submit();
    }
  }
}

async function toggleDepedencies(option: string) {
  project.value.rowDepedenciesLinked = [option];
}

async function toggleIdColumns(option:string) {
  const index = project.value.datasetHasIdColumnList.indexOf(option);
  if (index === -1) {
    project.value.datasetHasIdColumnList.push(option);
  } else {
    project.value.datasetHasIdColumnList.splice(index, 1);
  }
}

onMounted(async () => {
  pipeline.value = await getDefaultPipeline();
});
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
          <div class="text-body1 text-grey-9 text-weight-medium q-pb-lg">
            Laissez-vous guider et créez une IA en quelques cliques !
          </div>
          <q-file
            v-model="uploadedFile"
            class="q-mt-lg"
            name="csv_test_set"
            accept=".csv"
            :max-file-size="1000000000"
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
          <div class="text-body1 text-grey-9 text-weight-bold q-pb-lg">
            Dites-nous en plus sur votre projet
          </div>
          <q-input
            v-model="project.title"
            filled
            label="Titre"
            dense
            style="width: 450px;"
            :rules="[(v) => v.length > 2 || 'Minimum 3 charactères requis']"
          />
          <q-input
            v-model="project.description"
            filled
            label="Description"
            type="textarea"
            dense
            style="width: 450px;"
            :rules="[(v) => v.length > 30 || 'Minimum 30 charactères requis']"
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
          <div class="text-body1 text-grey-9 text-weight-bold question-title">
            Souhaitez-vous créer un modèle de survie ?
          </div>
          <div class="explain">
            Un modèle de survie est un modèle d'intelligence artificielle conçu pour
            prédire la survenue d'un événement tout en tenant compte du moment précis
            où cet événement se produit. Si vous souhaitez construire un tel modèle,
            il est nécessaire d'avoir une colonne dans votre jeu de données qui enregistre
            cette information temporelle.
          </div>

          <q-toggle
            v-model="project.survivalModel"
            :label="project.survivalModel === true ? 'Oui, model de survie !' : 'Non, modèle classqiue !'"
            size="70px"
            @update:model-value="(v) => { v === false ? project.survivalModeldatasetTargetTime = '' : null }"
          />

          <template v-if="project.survivalModel">
            <div class="text-body1 text-grey-9 text-weight-bold question-title">
              Quelle colonne contient l’information temporelle ?
            </div>
            <div class="explain">
              Voici toutes les colonnes disponibles dans le fichier que vous avez sélectionné.
              Veuillez indiquer celle qui contient l'information sur le temps ou la durée
              associée à l'événement que vous souhaitez modéliser.
            </div>

            <div
              class="row wrap button-container"
              style="max-width: 500px;"
            >
              <q-btn
                v-for="(option, index) in csvFileHeaderColumns"
                :key="index"
                :label="option"
                :color="project.survivalModeldatasetTargetTime === option ? 'primary' : 'grey'"
                @click="project.survivalModeldatasetTargetTime = option"
                class="q-mb-sm"
              />
            </div>
          </template>
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
          <div class="text-body1 text-grey-9 text-weight-bold question-title">
            Quelle information souhaitez-vous prédire ?
          </div>
          <div class="explain">
            Vous trouverez ci-dessous toutes les informations contenues dans le fichier que vous avez sélectionné.
            Laquelle souhaitez-vous <ExplainWord word="prédire" /> ?
            Dr IA analysera les relations statistiques entre cette information
            et les autres données pour établir ses prédictions.
          </div>

          <div
            class="row wrap button-container"
            style="max-width: 500px;"
          >
            <q-btn
              v-for="(option, index) in csvFileHeaderColumns"
              :key="index"
              :label="option"
              :color="project.csvColumnToPredict === option ? 'primary' : 'grey'"
              @click="project.csvColumnToPredict = option"
              class="q-mb-sm"
            />
          </div>
        </q-form>
      </q-step>

      <q-step
        :name="5"
        title=""
        icon="psychology"
        :done="step > 5"
      >
        <q-form
          :ref="(el: QForm) => formSteppers[4] = el"
          class="flex-center column"
        >
          <div class="text-body1 text-grey-9 text-weight-bold question-title">
            Existe-t-il une forte dépendance entre certaines lignes du fichier ?
          </div>
          <div class="explain">
            Lors de l'<ExplainWord word="entraînement" /> d'un <ExplainWord word="modèle" />
            d'intelligence artificielle, il est essentiel de prendre en compte les
            <ExplainWord word="dépendances" /> entre les lignes de données. Si ces
            <ExplainWord word="dépendances" /> ne sont pas identifiées, le
            <ExplainWord word="modèle" /> risque de se concentrer sur ces liens au
            lieu d'analyser les véritables éléments prédictifs.
            Par exemple, si plusieurs lignes proviennent du dossier médical d'un
            même patient, le modèle pourrait apprendre à reconnaître le patient
            plutôt que de réaliser de véritables <ExplainWord word="prédictions" />.
            Dans ce cas, les <ExplainWord word="performances" /> du modèle
            risqueraient de chuter lorsqu'il sera utilisé pour
            <ExplainWord word="prédire" /> des résultats pour des patients qu'il ne connaît pas.
          </div>

          <q-toggle
            v-model="project.rowDepedencies"
            :label="project.rowDepedencies === true ? 'Oui, il y a des dépendances !' : 'Non, aucune dépendance !'"
            size="70px"
          />

          <template v-if="project.rowDepedencies">
            <div class="text-body1 text-grey-9 text-weight-bold q-pb-lg q-pt-xl question-title">
              Comment reconnaître les lignes dépendantes ?
            </div>
            <div class="explain">
              Aidez Dr IA à identifier les <ExplainWord word="dépendances" />
              afin d'éviter un <ExplainWord word="biais de fuite de données" />.
              Pour ce faire, votre fichier doit contenir une colonne permettant
              de regrouper les lignes issues d'un même sujet. Par exemple, cela peut
              être un identifiant de patient ou de séjour. Veuillez sélectionner la
              ou les colonnes qui permettent d'identifier ces groupes.
            </div>

            <div
              class="row wrap button-container"
              style="max-width: 500px;"
            >
              <q-btn
                v-for="(option, index) in csvFileHeaderColumns"
                :key="index"
                :label="option"
                :color="project.rowDepedenciesLinked.includes(option) ? 'primary' : 'grey'"
                @click="toggleDepedencies(option)"
                class="q-mb-sm"
              />
            </div>
          </template>
        </q-form>
      </q-step>

      <q-step
        :name="6"
        title="Informations sur la prédiction"
        icon="psychology"
        :done="step > 6"
      >
        <q-form
          :ref="(el: QForm) => formSteppers[5] = el"
          class="flex-center column"
          @submit="submitForAITrain"
        >
          <div class="text-body1 text-grey-9 text-weight-bold q-pb-lg question-title">
            Vos données contient-elle des colonnes d'identifiants ?
          </div>
          <div class="explain">
            Une colonne identifiante est une colonne qui permet d'indentifier
            une ligne de manière unique, comme un numéro de ligne, un IPP ou encore un IEP.
            Pour les mêmes raisons que les dépendances, il est crucial d'informer Dr IA de
            la présence de ces colonnes afin qu'elles ne soient pas utilisées lors
            de l'<ExplainWord word="entraînement" />.
          </div>
          <q-toggle
            v-model="project.datasetHasIdColumn"
            :label="project.datasetHasIdColumn === true ?
              'Oui, il existe des colonnes identifiants' : 'Non, j\'ai déjà tout supprimé !'"
            size="70px"
          />

          <template v-if="project.datasetHasIdColumn">
            <div class="text-body1 text-grey-9 text-weight-bold q-pb-lg q-pt-xl question-title">
              Quelles colonnes ?
            </div>
            <div class="explain">
              Aidez Dr IA à identifier les colonnes identifiantes afin
              d'améliorer sa capacité de <ExplainWord word="généralisation" />.
              Veuillez sélectionner la ou les colonnes qui permettent
              d'identifier chaque ligne individuellement.
            </div>
            <div
              class="row wrap button-container"
              style="max-width: 500px;"
            >
              <q-btn
                v-for="(option, index) in csvFileHeaderColumns"
                :key="index"
                :label="option"
                :color="project.datasetHasIdColumnList.includes(option) ? 'primary' : 'grey'"
                @click="toggleIdColumns(option)"
                class="q-mb-sm"
              />
            </div>
          </template>

          <q-expansion-item
            label="Options avancées"
            class="full-width bg-grey-3"
          >
            <PipelineProject
              ref="pipelineComponentRef"
              v-if="pipeline"
              class="bg-grey-3"
              :pipeline="pipeline"
              @is-pipeline-loading="(status: boolean) => { isPipelineLoading = status }"
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
            :loading="step === 6 && isPipelineLoading"
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

.q-stepper {
  box-shadow: none;
  background: transparent;
  margin-top: 40px;
}

.q-stepper::before {
  content: '';
  display: block;
  width: 200px;
  border-top: 1px rgba(0,0,0,0.5) solid;
  margin: auto;
}

.button-container {
  justify-content: space-evenly;
}

.button-container .q-btn {
  margin: 10px;
}

.explain {
  width: 450px;
  margin-bottom: 15px;
}

.question-title {
  width: 450px;
}

</style>
