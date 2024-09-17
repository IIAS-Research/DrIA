import { boot } from 'quasar/wrappers';
import VueTyper from 'vue3-typer';

export default boot(({ app }) => {
  app.component('VueTyper', VueTyper);
});
