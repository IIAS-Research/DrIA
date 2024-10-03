import { date } from 'quasar';

export function formatToDDMMYYYHHmm(dateParam: Date) {
  return date.formatDate(dateParam, 'DD/MM/YYYY à HH:mm');
}
