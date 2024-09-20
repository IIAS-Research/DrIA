import { QNotifyOptions, Notify } from 'quasar';

export function notify(type: 'positive' | 'negative' | 'warning' | 'info' | 'ongoing', message: string) {
  const config: QNotifyOptions = {
    message,
    type,
    progress: true,
    actions: [
      {
        icon: 'close', color: 'white', round: true, handler: () => { /* ... */ },
      },
    ],
  };
  Notify.create(config);
}
