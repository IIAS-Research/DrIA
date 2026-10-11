using System.Timers;
using Microsoft.AspNetCore.SignalR;
using Server.Hubs;
using static Server.Services.ApplicationService;

namespace Server.Services;

public abstract class IaPullingService<T> : ApplicationService where T : ApplicationHub
{
  protected readonly List<IaConnectionPulling<T>> _connections = [];
  protected readonly IServiceScopeFactory _scopeFactory;
  protected readonly IHubContext<T> _hub;

  protected IaPullingService(IServiceScopeFactory scopeFactory, IHubContext<T> hub)
  {
    _scopeFactory = scopeFactory;
    _hub = hub;
  }

  public IaConnectionPulling<T>? GetIaConnection(string jobWebServiceId)
  {
    lock (_connections) return _connections.FirstOrDefault(c => c.JobWebServiceId == jobWebServiceId);
  }

  public List<IaConnectionPulling<T>> SearchConnectionsByConnectionId(string connectionId) =>
    GetAllConnections().FindAll(c => c.HasConnectionId(connectionId));

  public List<IaConnectionPulling<T>> SearchForConnectionIdInConnection(string connectionId) =>
    SearchConnectionsByConnectionId(connectionId);

  public void DeleteIaConnection(string jobServiceId)
  {
    lock (_connections)
    {
      var connection = _connections.FirstOrDefault(c => c.JobWebServiceId == jobServiceId);
      if (connection == null) return;
      connection.StopPulling();
      _connections.Remove(connection);
    }
  }

  public virtual List<IaConnectionPulling<T>> GetAllConnections()
  {
    lock (_connections) return [.. _connections];
  }
}

// Polling persists results even when no browser is connected. Repeated starts are harmless.
public abstract class IaConnectionPulling<T> where T : ApplicationHub
{
  public IHubContext<T> Hub { get; }
  public string Status { get; set; } = "QUEUED";
  public string JobWebServiceId { get; }
  public bool IsPulling { get; private set; }
  protected readonly System.Timers.Timer _timer = new(2000) { AutoReset = false };
  protected IServiceScopeFactory ScopeFactory { get; }
  private readonly HashSet<string> _connectionIds = [];
  private readonly object _stateLock = new();

  protected IaConnectionPulling(string jobWebServiceId, IHubContext<T> hub, IServiceScopeFactory scopeFactory)
  {
    JobWebServiceId = jobWebServiceId;
    Hub = hub;
    ScopeFactory = scopeFactory;
    _timer.Elapsed += async (sender, args) =>
    {
      try { await RunInterval(sender!, args); }
      catch (Exception ex)
      {
        Console.WriteLine($"Polling {JobWebServiceId}: {ex.Message}");
        if (IsPulling) _timer.Start();
      }
    };
  }

  public Task StartPulling()
  {
    lock (_stateLock)
    {
      if (IsPulling) return Task.CompletedTask;
      IsPulling = true;
      _timer.Start();
    }
    return Task.CompletedTask;
  }

  protected abstract Task RunInterval(object source, ElapsedEventArgs elapseArg);
  protected abstract Task<ResponseService> OnProcessFinish();

  protected async Task SendErrorsToWs(ResponseService response)
  {
    await Hub.Clients.Group(JobWebServiceId).SendAsync("StopMessage", string.Join("; ", response.Errors));
    StopPulling();
  }

  public void StopPulling()
  {
    lock (_stateLock) { IsPulling = false; _timer.Stop(); }
  }

  public void AddConnectionId(string id) { lock (_connectionIds) _connectionIds.Add(id); }
  public void RemoveConnectionId(string id) { lock (_connectionIds) _connectionIds.Remove(id); }
  public bool HasConnectionId(string id) { lock (_connectionIds) return _connectionIds.Contains(id); }
  public int GetConnectionIdsCount() { lock (_connectionIds) return _connectionIds.Count; }
}
