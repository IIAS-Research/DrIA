using System.Net.WebSockets;
using Microsoft.AspNetCore.SignalR;
using Server.Hubs;
using SignalRChat.Hubs;
using SocketIOClient;

namespace Server.Services
{
  public class IAWebSocketService(IHubContext<IaStatusHub> iaStatusHub, IHubContext<IaMetricHub> iaMetricHub) : ApplicationService
  {
    public static readonly string _url = "http://rss00992:32845";
    private readonly List<IaConnection> _connections = [];
    private readonly IHubContext<IaStatusHub> _iaStatusHub = iaStatusHub;
    private readonly IHubContext<IaMetricHub> _iaMetricHub = iaMetricHub;

    public IaConnection? GetIaConnection(string jobWebServiceId) =>
        _connections.FirstOrDefault(c => c.JobWebServiceId == jobWebServiceId);

    public IaConnection CreateIaConnection(string jobWebServiceId, Type hubType)
    {
      IHubContext<ApplicationHub> hub;

      if (hubType == typeof(IaMetricHub))
      {
        hub = _iaMetricHub;
      }
      else
      {
        hub = _iaStatusHub;
      }

      var newConnection = new IaConnection(jobWebServiceId, hub);
      _connections.Add(newConnection);
      return newConnection;
    }

    public List<IaConnection> SearchConnectionsByConnectionId(string connectionId) =>
        _connections.FindAll(c => c.HasConnectionId(connectionId));

    public async Task DeleteIaConnection(string uuid)
    {
      var connection = GetIaConnection(uuid);
      if (connection != null)
      {
        await connection.HandleDisconnectionAsync();
        _connections.Remove(connection);
      }
    }

    public List<IaConnection> SearchForConnectionIdInConnection(string connectionId)
    {
      return _connections.FindAll(c => c.HasConnectionId(connectionId));
    }
  }

  public class IaConnection(string jobWebServiceId, IHubContext<ApplicationHub> hub)
  {
    private SocketIO? _socketIOClient;
    public IHubContext<ApplicationHub> Hub { get; } = hub;
    public string JobWebServiceId { get; } = jobWebServiceId;
    public bool IsConnected { get; private set; }
    private readonly List<string> _connectionIds = [];

    public async Task ConnectIaWebServiceAndSignalRWebSocketsAsync()
    {
      Console.WriteLine("Inside ConnectIaWebServiceAndSignalRWebSocketsAsync");
      try
      {
        _socketIOClient = new SocketIO(IAWebSocketService._url, new SocketIOOptions
        {
          Query =
            [
                new("job", JobWebServiceId)
            ]
        });

        _socketIOClient.On("status", async response => await HandleMessageAsync(response));
        _socketIOClient.OnDisconnected += async (_, __) => await HandleDisconnectionAsync();
        _socketIOClient.OnConnected += (_, __) => IsConnected = true;

        await _socketIOClient.ConnectAsync();
        Console.WriteLine($"IsConnected : {IsConnected}");
        Console.WriteLine($"Connected to: {IAWebSocketService._url}?job={JobWebServiceId}");
      }
      catch (Exception ex)
      {
        Console.WriteLine($"Connection failed: {ex.Message}");
        IsConnected = false;
      }
    }

    private async Task HandleMessageAsync(SocketIOResponse response)
    {
      Console.WriteLine($"Inside HandleMessageAsync: {response}");
      await Hub.Clients.Group(JobWebServiceId).SendAsync("ReceiveMessage", response.GetValue<string>());
      // TODO mettre à jour la db
      Console.WriteLine($"Message sent to SignalR: {response}");

    }

    public async Task HandleDisconnectionAsync()
    {
      foreach (var connectionId in _connectionIds)
      {
        await Hub.Groups.RemoveFromGroupAsync(connectionId, JobWebServiceId);
        Console.WriteLine($"Disconnected from {JobWebServiceId}, SignalR connections removed.");
      }

      IsConnected = false;
      if (_socketIOClient != null)
      {
        await _socketIOClient.DisconnectAsync();
      }
      _socketIOClient = null;
    }

    public void AddConnectionId(string connectionId) => _connectionIds.Add(connectionId);

    public void RemoveConnectionId(string connectionId) => _connectionIds.RemoveAll(id => id == connectionId);

    public bool HasConnectionId(string connectionId) => _connectionIds.Contains(connectionId);

    public int GetConnectionIdsCount() => _connectionIds.Count;
  }
}