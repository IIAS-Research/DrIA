import { date } from "quasar";

export function dateFormat(dateTime: string | Date | undefined): string {
  if (dateTime === undefined) {
    return "";
  }
  return date.formatDate(dateTime, "DD/MM/YYYY");
}
