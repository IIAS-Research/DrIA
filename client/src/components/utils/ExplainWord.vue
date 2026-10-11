<!-- eslint-disable max-len -->
<script setup lang="ts">

const propsComponent = defineProps<{
  word: string
}>();

const explanation = {
  predict: 'In machine learning, predicting means using a model trained on existing data to estimate a target value for new, unseen data. Predictions can be numeric values (regression) or categories (classification). The aim is to apply what the model has learned to make accurate estimates for future cases.',
  model: 'In machine learning, a model is a mathematical or statistical representation of the relationships between input and output variables. It is trained on data and used to make predictions or classifications. A model applies what it has learned to new, unseen data.',
  performance: 'Model performance describes how accurately a model predicts outcomes for new data. It is measured using metrics such as accuracy, precision, recall, F1 score or mean squared error, depending on the task. These metrics help assess how well the model generalizes beyond its training data.',
  dependencies: 'Dependencies between rows are relationships or correlations between observations in a dataset. If observations are not independent, the model may learn relationships specific to those rows rather than patterns that generalize. Accounting for these dependencies helps avoid overfitting and unreliable predictions.',
  predictions: 'A prediction is the output a trained model produces when it estimates a target value, such as a category or a number, from new data. Prediction quality depends on model performance and its ability to generalize beyond the training data.',
  'data leakage': 'Data leakage occurs when information from the test set or from the future is accidentally included in model training. It gives the model access to information that should be unavailable, producing artificially high evaluation scores. The model may then perform poorly on new data.',
  generalization: 'Generalization is the ability of a trained model to perform well on new, unseen data. A model that generalizes well captures underlying patterns without learning the details or noise of the training data. This allows it to make useful predictions for future cases.',
  training: 'Training is the process in which a model learns from data by adjusting its parameters to reduce error or improve accuracy. It examines inputs and their known outcomes to identify patterns that can be used to predict outcomes for new data.',
}[propsComponent.word.toLowerCase()];
</script>

<template>
  <span
    v-if="explanation"
    class="explanation"
  >
    {{ propsComponent.word }}
    <q-tooltip
      style="width: 300px;"
      class="text-body2"
      anchor="bottom middle"
      self="top middle"
    >
      {{ explanation }}
    </q-tooltip>
  </span>
  <span v-else>{{ word }}</span>
</template>

<style scoped>
span.explanation {
  color: rgb(25, 118, 210);
  font-weight: bold;
}
</style>
