import { api } from "src/boot/axios";
import { IProject } from "src/components/models";
import { notify } from "src/utils/notify";

const projectUrl = "Projects";

export async function getUserProjects(): Promise<IProject[]> {
  try {
    const response = (await api.get(`${projectUrl}/me`))?.data;
    return response;
  } catch (error) {
    console.error("Error fetching projects:", error);
    notify("negative", String(error));
    return [];
  }
}
