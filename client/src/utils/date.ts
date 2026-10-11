import { date } from 'quasar';

export function formatToDDMMYYYHHmm(dateParam: Date | string | undefined) {
  return date.formatDate(dateParam, 'MMM D, YYYY HH:mm');
}
