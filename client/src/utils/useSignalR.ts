import { IProcess } from 'src/components/models';
import { ref } from "vue";
import { HubConnection, HubConnectionBuilder } from "@microsoft/signalr";

export function useSignalR(hubName: "status" | "metrics", processId: IProcess['id']) {
  // refs
  const connection = ref<HubConnection>();

  // functions
  function connect() {
    const url =
      process.env.api?.replace("http", "ws").replace("/api", "") + "/cable";
    connection.value = new HubConnectionBuilder().withUrl(url).build();

    connection.value
      .start()
      .then(() => console.log("Connected to SignalR"))
      .catch((error) => console.error(error));

    connection.value.on("ReceiveMessage", (message, user) => {
      console.log("zob");
    });

    connection.value.invoke("JoinGroup", processId);
  }
  function disconnect() {
    if (!connection.value) {
      return;
    }
    connection.value
      .stop()
      .then(() => console.log("Disconnected from SignalR"))
      .catch((error) => console.error(error));
  }

  return {
    connect,
    disconnect,
  };
}
