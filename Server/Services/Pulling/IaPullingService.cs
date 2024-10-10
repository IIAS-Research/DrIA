using System.Net.WebSockets;
using System.Timers;
using Microsoft.AspNetCore.SignalR;
using Server.Hubs;
using Server.Models;
using static Server.Services.ApplicationService;

namespace Server.Services
{
  public abstract class IaPullingService<T> : ApplicationService where T : ApplicationHub
  {
    public static readonly string _url = "http://rss00992:32845";
    protected readonly List<IaConnectionPulling<T>> _connections = [];
    protected readonly IServiceScopeFactory _scopeFactory;
    protected readonly IHubContext<T> _hub;

    public IaPullingService(IServiceScopeFactory scopeFactory, IHubContext<T> hub)
    {
      _scopeFactory = scopeFactory;
      _hub = hub;
    }

    public IaConnectionPulling<T>? GetIaConnection(string jobWebServiceId) =>
        _connections.FirstOrDefault(c => c.JobWebServiceId == jobWebServiceId);

    public List<IaConnectionPulling<T>> SearchConnectionsByConnectionId(string connectionId) =>
        _connections.FindAll(c => c.HasConnectionId(connectionId));

    public void DeleteIaConnection(string uuid)
    {
      var connection = GetIaConnection(uuid);
      if (connection != null)
      {
        connection.IsPulling = false;
        _connections.Remove(connection);
      }
    }

    public List<IaConnectionPulling<T>> SearchForConnectionIdInConnection(string connectionId)
    {
      return _connections.FindAll(c => c.HasConnectionId(connectionId));
    }
  }

  public abstract class IaConnectionPulling<T>(string jobWebServiceId, IHubContext<T> hub, IServiceScopeFactory scopeFactory) where T : ApplicationHub
  {
    public IHubContext<T> Hub { get; } = hub;
    public string JobWebServiceId { get; } = jobWebServiceId;
    public bool IsPulling { get; set; } = false;
    public Process.EProcessStatut Status { get; set; }
    private readonly List<string> _connectionIds = [];
    private System.Timers.Timer _timer = new System.Timers.Timer(1000 * 10);
    public IServiceScopeFactory ScopeFactory = scopeFactory;

    public async Task StartPulling()
    {
      IsPulling = true;
      try
      {
        _timer.Enabled = true;
        _timer.AutoReset = false;
        _timer.Elapsed += async (sender, args) => await RunInterval(sender, args);
      }
      catch (Exception ex)
      {
        Console.WriteLine($"Connection failed: {ex.Message}");
      }
    }

    protected abstract Task RunInterval(object source, ElapsedEventArgs elapseArg);

    protected async Task SendErrorsToWs(ResponseService responseService)
    {
      await Hub.Clients.Group(JobWebServiceId).SendAsync("ReceiveMessage", $"Une erreur est survenue : {responseService.Errors}");
      StopPulling();
    }

    protected abstract Task<ResponseService> OnProcessFinish();

    public void StopPulling()
    {
      _timer.Stop();
      _timer.Enabled = false;
      Console.WriteLine($"Stop pulling");
    }

    public void AddConnectionId(string connectionId) => _connectionIds.Add(connectionId);

    public void RemoveConnectionId(string connectionId) => _connectionIds.RemoveAll(id => id == connectionId);

    public bool HasConnectionId(string connectionId) => _connectionIds.Contains(connectionId);

    public int GetConnectionIdsCount() => _connectionIds.Count;
  }
}