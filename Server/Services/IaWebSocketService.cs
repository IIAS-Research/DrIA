using System.Net.WebSockets;
using System.Text;

namespace Server.Services;

public class IAWebSocketService : ApplicationService
{
  public static readonly Uri _url = new Uri("ws://rss00992:32845");
  public List<IaConnection> Connecions = [];

  public class IaConnection
  {
    private ClientWebSocket _clientWebSocket = new ClientWebSocket();
    public required string Uuid;

    public IaConnection(string uuid)
    {
      Uuid = uuid;
    }

    public async Task ConnectAsync()
    {
      try
      {
        await _clientWebSocket.ConnectAsync(_url, CancellationToken.None);
        await ReceiveMessages();
      }
      catch (Exception exception)
      {
        Console.WriteLine(exception.Message);
      }
    }

    private async Task ReceiveMessages()
    {
      var buffer = new byte[1024];

      while (_clientWebSocket.State == WebSocketState.Open)
      {
        var result = await _clientWebSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
        if (result.MessageType == WebSocketMessageType.Text)
        {
          var message = Encoding.UTF8.GetString(buffer, 0, result.Count);
          Console.WriteLine($"Message reçu : {message}");
        }
      }
    }

    public async Task SendMessage(string message)
    {
      if (_clientWebSocket.State == WebSocketState.Open)
      {
        var buffer = Encoding.UTF8.GetBytes(message);
        await _clientWebSocket.SendAsync(new ArraySegment<byte>(buffer), WebSocketMessageType.Text, true, CancellationToken.None);
      }
    }

    public async Task CloseAsync()
    {
      await _clientWebSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Standard close", CancellationToken.None);
    }
  }

}
