<script setup lang="ts">

const propsComponent = defineProps<{
  word: string
}>();

const explanation = {
  prédire: "En machine learning, \"prédire\" signifie utiliser un modèle entraîné sur des données existantes pour estimer ou anticiper la valeur d'une variable cible pour de nouvelles données non vues. La prédiction peut concerner des résultats numériques (régression) ou des catégories (classification). L'objectif est de généraliser les connaissances du modèle pour faire des estimations précises sur des cas futurs.",
  modèle: "En machine learning, un \"modèle\" est une représentation mathématique ou statistique qui capture les relations entre les variables d'entrée et de sortie dans les données. Il est créé à partir de l'entraînement sur des données et est utilisé pour faire des prédictions ou des classifications. Le modèle sert à généraliser les apprentissages pour pouvoir traiter de nouvelles données inconnues.",
  performances: "En machine learning, les \"performances\" désignent la capacité d'un modèle à effectuer des prédictions précises sur de nouvelles données. Elles sont généralement mesurées à l'aide de métriques comme l'exactitude, la précision, le rappel, le F1-score ou l'erreur quadratique moyenne, selon la tâche (classification ou régression). Les performances reflètent la qualité et la fiabilité du modèle à généraliser ses apprentissages au-delà des données d'entraînement.",
  dépendances: "En machine learning, les \"dépendances entre les lignes\" font référence aux relations ou corrélations entre les différentes observations (lignes) d'un ensemble de données. Ces dépendances peuvent biaiser l'entraînement d'un modèle si les observations ne sont pas indépendantes, car le modèle pourrait surapprendre ces relations spécifiques plutôt que de généraliser correctement. Idéalement, les lignes devraient être indépendantes pour garantir des prédictions fiables et éviter le surapprentissage (overfitting).",
  prédictions: "En machine learning, une \"prédiction\" est le résultat généré par un modèle lorsqu'il applique ses connaissances à de nouvelles données pour estimer une valeur cible (par exemple, une catégorie ou un chiffre). C'est la sortie que le modèle propose après avoir été entraîné sur un ensemble de données. La qualité de la prédiction dépend des performances du modèle et de sa capacité à généraliser à partir des données d'entraînement.",
  'biais de fuite de données': "Le \"biais de fuite de données\" ou data leak en machine learning survient lorsque des informations du jeu de données de test (ou futures) sont accidentellement incluses dans l'entraînement du modèle. Cela fausse les résultats en donnant au modèle un accès à des informations qu'il ne devrait pas avoir, ce qui conduit à des performances artificiellement élevées durant l'évaluation. En conséquence, le modèle paraît plus performant qu'il ne l'est réellement et échoue souvent sur des données nouvelles ou réelles.",
  généralisation: "En machine learning, la \"généralisation\" désigne la capacité d'un modèle à bien performer sur des données nouvelles et non vues, après avoir été entraîné sur un ensemble de données spécifique. Un modèle qui généralise bien parvient à capturer les tendances sous-jacentes des données d'entraînement sans surapprendre les détails ou le bruit. L'objectif est que le modèle soit capable de faire des prédictions précises dans des situations futures en s'appuyant sur des patterns pertinents plutôt que sur des particularités des données d'entraînement.",
  entraînement: "En machine learning, \"entraînement\" désigne le processus où un modèle apprend à partir de données en ajustant ses paramètres pour minimiser une erreur ou maximiser la précision. Pendant l'entraînement, le modèle analyse les données d'entrée et leurs résultats associés pour identifier des patterns. L'objectif est que le modèle généralise ces patterns pour effectuer des prédictions sur de nouvelles données."
}[propsComponent.word.toLowerCase()];
</script>

<template>
  <span
    v-if="explanation"
    class='explanation'
  >
    {{ propsComponent.word }}
    <q-tooltip
      class="text-body2"
      anchor="center right"
      self="center left"
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
