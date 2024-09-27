using System.Net.WebSockets;
using System.Text;
using Microsoft.AspNetCore.SignalR;
using SignalRChat.Hubs;

namespace Server.Services;

public class IAWebSocketService : ApplicationService
{
  public static readonly Uri _url = new Uri("ws://rss00992:32845");
  private readonly List<IaConnection> Connections = [];
  private readonly IHubContext<IaStatusHub> _iaStatusHub;
  private readonly IHubContext<IaMetricHub> _iaMetricHub;
  public IAWebSocketService(IHubContext<IaStatusHub> iaStatusHub, IHubContext<IaMetricHub> iaMetricHub)
  {
    _iaStatusHub = iaStatusHub;
    _iaMetricHub = iaMetricHub;
  }

  public IaConnection? GetIaConnection(string uuid)
  {
    return Connections.FirstOrDefault(c => c.Uuid == uuid);
  }

  public IaConnection CreateIaConnection(string uuid, Type type)
  {
    IHubContext<ApplicationHub> hub = _iaStatusHub;
    if (type == typeof(IaStatusHub))
    {
      hub = _iaStatusHub;
    }
    if (type == typeof(IaMetricHub))
    {
      hub = _iaMetricHub;
    }
    var newConnection = new IaConnection { Uuid = uuid, Hub = hub };
    Connections.Add(newConnection);
    return newConnection;
  }

  public List<IaConnection> SearchForConnectionIdInConnection(string connectionId)
  {
    return Connections.FindAll(c => c.ConnectionIdExist(connectionId));
  }

  async public Task DeleteIaConnection(string uuid)
  {
    var connection = GetIaConnection(uuid);
    if (connection == null)
    {
      return;
    }
    await connection.CloseAsync();
    Connections.RemoveAll(c => c.Uuid == uuid);
  }

  public class IaConnection
  {
    private ClientWebSocket _clientWebSocket = new ClientWebSocket();
    public required IHubContext<ApplicationHub> Hub;
    public required string Uuid;
    public bool IsConnected = false;
    private List<string> ConnectionIds = [];

    public async Task ConnectAsync()
    {
      try
      {
        await _clientWebSocket.ConnectAsync(_url, CancellationToken.None);
        await ReceiveMessages();
        IsConnected = true;
      }
      catch (Exception exception)
      {
        Console.WriteLine(exception.Message);
        IsConnected = false;
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
          await Hub.Clients.Group(Uuid).SendAsync(message);

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
      IsConnected = false;
    }

    public bool ConnectionIdExist(string connectionId)
    {
      return ConnectionIds.FindIndex(c => c == connectionId) > -1;
    }

    public void AddConnectionId(string connectionId)
    {
      ConnectionIds.Add(connectionId);
    }

    public void RemoveConnectionId(string connectionId)
    {
      ConnectionIds.RemoveAll(c => c == connectionId);
    }

    public int GetConnectionIdsCount()
    {
      return ConnectionIds.Count;
    }
  }

}
