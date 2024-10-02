import { IProcess } from 'src/components/models';
import { ref } from 'vue';
import { HubConnection, HubConnectionBuilder } from '@microsoft/signalr';

export function useSignalR(
  hubName: 'status' | 'metrics',
  processId: IProcess['iaWebServiceJobId'],
) {
  // refs
  const connection = ref<HubConnection | null>(null);
  const isConnected = ref(false);

  // functions
  async function connect() {
    const url = `${process.env.API}/ws/${hubName}`;
    console.log(url);

    connection.value = new HubConnectionBuilder()
      .withUrl(url)
      .withAutomaticReconnect()
      .build();

    connection.value.onreconnecting((error) => {
      console.log('Reconnecting...', error);
      isConnected.value = false;
    });

    // connection.value.onreconnected((connectionId) => {
    //   console.log('Reconnected:', connectionId);
    //   isConnected.value = true;
    // });

    connection.value.on('ReceiveMessage', (message) => {
      console.log('Received message:', message);
    });

    try {
      await connection.value.start();
      console.log('Connected to SignalR');
      console.log('processId => ', processId);
      await connection.value.invoke('TestWs');
      await connection.value.invoke('JoinGroup', processId);
      isConnected.value = true;
    } catch (error) {
      console.error('Error connecting to SignalR:', error);
    }
  }

  function disconnect() {
    if (!connection.value) {
      return;
    }
    connection.value
      .stop()
      .then(() => {
        console.log('Disconnected from SignalR');
        isConnected.value = false;
      })
      .catch((error: Error) => console.error('Error disconnecting:', error));
  }

  return {
    connect,
    disconnect,
    isConnected,
  };
}
