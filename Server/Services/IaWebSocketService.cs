using System.Net.WebSockets;
using Microsoft.AspNetCore.SignalR;
using Server.Hubs;
using SocketIOClient;
using System.Text.Json;

namespace Server.Services
{
  public class IAWebSocketService(IHubContext<IaGenerateModelHub> iaStatusHub, IHubContext<IaExplainModelHub> iaMetricHub) : ApplicationService
  {
    private readonly List<IaConnection> _connections = [];
    private readonly IHubContext<IaGenerateModelHub> _iaStatusHub = iaStatusHub;
    private readonly IHubContext<IaExplainModelHub> _iaMetricHub = iaMetricHub;

    public IaConnection? GetIaConnection(string jobWebServiceId) =>
        _connections.FirstOrDefault(c => c.JobWebServiceId == jobWebServiceId);

    public IaConnection CreateIaConnection(string jobWebServiceId, Type hubType, string? socketUrl = null)
    {
      IHubContext<ApplicationHub> hub;

      if (hubType == typeof(IaExplainModelHub))
      {
        hub = _iaMetricHub;
      }
      else
      {
        hub = _iaStatusHub;
      }

      var newConnection = new IaConnection(jobWebServiceId, hub, socketUrl);
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

  public class IaConnection(string jobWebServiceId, IHubContext<ApplicationHub> hub, string? socketUrl = null)
  {
    private SocketIO? _socketIOClient;
    public IHubContext<ApplicationHub> Hub { get; } = hub;
    public string JobWebServiceId { get; } = jobWebServiceId;
    public bool IsConnected { get; private set; }
    private readonly List<string> _connectionIds = [];

    public async Task ConnectIaWebServiceAndSignalRWebSocketsAsync()
    {
      // Cached jobs have no socket. Their status and results remain available over HTTP.
      if (string.IsNullOrWhiteSpace(socketUrl)) return;
      try
      {
        var socketUri = new Uri(socketUrl);
        var query = Microsoft.AspNetCore.WebUtilities.QueryHelpers.ParseQuery(socketUri.Query)
            .Select(pair => new KeyValuePair<string, string>(pair.Key, pair.Value.ToString())).ToList();
        _socketIOClient = new SocketIO(socketUri.GetLeftPart(UriPartial.Authority), new SocketIOOptions
        {
          Query = query,
        });

        _socketIOClient.On("status", async response => await HandleMessageAsync(response));
        _socketIOClient.On("logs", async response => await Hub.Clients.Group(JobWebServiceId).SendAsync("ReceiveLogs", response.GetValue<JsonElement>()));
        _socketIOClient.On("progress", async response => await Hub.Clients.Group(JobWebServiceId).SendAsync("ReceiveProgress", response.GetValue<JsonElement>()));
        _socketIOClient.On("error", async response => await Hub.Clients.Group(JobWebServiceId).SendAsync("StopMessage", response.GetValue<JsonElement>()));
        _socketIOClient.On("disconnect_request", async _ => await HandleDisconnectionAsync());
        _socketIOClient.OnDisconnected += async (_, __) => await HandleDisconnectionAsync();
        _socketIOClient.OnConnected += (_, __) => IsConnected = true;

        _socketIOClient.OnPong += (_, __) => Console.WriteLine("On the pong");
        _socketIOClient.OnReconnectAttempt += (_, __) => Console.WriteLine("OnReconnectAttempt");

        await _socketIOClient.ConnectAsync();
        Console.WriteLine($"IsConnected : {IsConnected}");
        Console.WriteLine($"Connected to IAML for {JobWebServiceId}");
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
      await Hub.Clients.Group(JobWebServiceId).SendAsync("ReceiveMessage", response.GetValue<JsonElement>());

      // TODO: Save informations in DB when JobStatus is different from what we already have in DB

      Console.WriteLine($"Message sent to SignalR: {response}");
    }

    public async Task HandleDisconnectionAsync()
    {
      if (!IsConnected) return;
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
