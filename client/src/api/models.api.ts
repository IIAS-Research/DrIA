import { api } from "src/boot/axios";
import { IModel } from "src/components/models";
import { notify } from "src/utils/notify";

const projectUrl = "Models";

export async function getModeleById(id: number): Promise<IModel | undefined> {
  try {
    const response = (await api.get(`${projectUrl}/${id}`))?.data;
    return response;
  } catch (error) {
    console.error("Error fetching model:", error);
    notify("negative", String(error));
  }
}

export async function getModeleExplanationById(
  id: number
): Promise<IModel["textExplanation"] | undefined> {
  try {
    const response = (await api.get(`${projectUrl}/${id}/explanation`))?.data;
    return response;
  } catch (error) {
    console.error("Error fetching explanation:", error);
    notify("negative", String(error));
  }
}
