import { date } from 'quasar';

export function formatToDDMMYYYHHmm(dateParam: Date | string | undefined) {
  return date.formatDate(dateParam, 'DD/MM/YYYY HH:mm');
}
