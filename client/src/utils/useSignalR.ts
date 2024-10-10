import { ref } from "vue";
import { HubConnection, HubConnectionBuilder } from "@microsoft/signalr";

export function useSignalR(hubName: "status" | "metrics", processId: string) {
  // refs
  const connection = ref<HubConnection | null>(null);
  const isConnected = ref(false);
  const receivedMessageRef = ref<any>();
  const errorDialog = ref(false);

  // functions
  function disconnect() {
    if (!connection.value) {
      return;
    }
    connection.value
      .stop()
      .then(() => {
        console.log("Disconnected from SignalR");
        isConnected.value = false;
      })
      .catch((error: Error) => console.error("Error disconnecting:", error));
  }

  async function connect() {
    const url = `${process.env.API}/ws/${hubName}`;
    connection.value = new HubConnectionBuilder()
      .withUrl(url)
      .withAutomaticReconnect()
      .build();

    // connection.value.onreconnecting((error) => {
    //   console.log('Reconnecting...', error);
    //   isConnected.value = false;
    // });

    // connection.value.onreconnected((connectionId) => {
    //   console.log('Reconnected:', connectionId);
    //   isConnected.value = true;
    // });

    connection.value.on("ReceiveMessage", (message) => {
      console.log("Received message:", message);
      receivedMessageRef.value = message;
    });

    connection.value.on("StopMessage", (message) => {
      console.log("Stop message:", message);
      errorDialog.value = true;
      disconnect();
    });

    try {
      await connection.value.start();
      console.log("Connected to SignalR");
      console.log("processId => ", processId);
      await connection.value.invoke("TestWs");
      await connection.value.invoke("JoinGroup", processId);
      isConnected.value = true;
    } catch (error) {
      console.error("Error connecting to SignalR:", error);
    }
  }

  return {
    connect,
    disconnect,
    isConnected,
    receivedMessageRef,
    errorDialog,
  };
}
