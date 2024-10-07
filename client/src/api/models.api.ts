import { IModel } from "src/components/models";
import { notify } from "src/utils/notify";

const projectUrl = "Models";

export async function getModeleById(id: number): Promise<IModel | undefined> {
  return await {
    id: 1,
    date: '2024/10/07',
    metrics: {
      id: 1,
      model: "Nom du modèle",
      metrics_info: {
        test: {
          name: "name",
          desc: "qsdqsd",
        },
      },
      main_metrics: "dfsdfsdf",
      metrics_train: {
        okay: 0.98,
        "okay google": 0.5,
        adjimé: 0.97,
      },
      metrics_test: undefined,
    },
    explanation: [],
    bibliography: undefined,
  };
  // try {
  //   const response = (await api.get(`${projectUrl}/${id}`))?.data;
  //   return response;
  // } catch (error) {
  //   console.error("Error fetching model:", error);
  //   notify("negative", String(error));
  // }
}
