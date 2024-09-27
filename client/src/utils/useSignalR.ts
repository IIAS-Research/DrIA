import { IActionCableData } from 'src/components/models'
import { createConsumer } from '@rails/actioncable'
import { ref } from 'vue'

export function useCable(channel = '', params = {}, onReceived: (data: any) => void, onConnected: () => void, onDisconnected: () => void) {
  // refs
  const cable = ref();
  const subscription = ref();

  // functions
  function connect() {
    const url = process.env.api?.replace('http', 'ws').replace('/api', '') + '/cable'
    cable.value = createConsumer(url)
    const channelEnvironment = (process.env.channelPrefixWS ?? '') + channel
    const aboParams = { channel: channelEnvironment, ...params }
    subscription.value = cable.value.subscriptions.create(aboParams, {
      connected() {
        onConnected()
      },
      disconnected() {
        onDisconnected()
      },
      received(data: IActionCableData) {
        onReceived(data)
      }
    })
  }
  function disconnect()
  {
    if (!cable.value) {
      return;
    }
    subscription.value.disconnected();
    cable.value.subscriptions.remove(subscription.value);
    cable.value.disconnect();
  }
  
  return {
    connect,
    disconnect
  }
}
