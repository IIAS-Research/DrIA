using System.Net.WebSockets;
using System.Timers;
using Microsoft.AspNetCore.SignalR;
using Server.Hubs;
using static Server.Services.ApplicationService;

namespace Server.Services
{
  public abstract class IaPullingService<T> : ApplicationService where T : ApplicationHub
  {
    protected readonly List<IaConnectionPulling<T>> _connections = [];
    protected readonly IServiceScopeFactory _scopeFactory;
    protected readonly IHubContext<T> _hub;
    protected readonly System.Timers.Timer _timerForClean = new System.Timers.Timer(1000 * 60 * 5);

    public IaPullingService(IServiceScopeFactory scopeFactory, IHubContext<T> hub)
    {
      _scopeFactory = scopeFactory;
      _hub = hub;
      StartCleanProcess();
    }

    private void StartCleanProcess()
    {
      _timerForClean.Enabled = true;
      _timerForClean.Elapsed += async (sender, args) =>
      {
        var connectionsToDelete = GetAllConnections().FindAll(iac => iac.IsPulling == false);
        foreach (var connection in connectionsToDelete)
        {
          DeleteIaConnection(connection.JobWebServiceId);
          Console.WriteLine($"Cleaning connection done for job {connection.JobWebServiceId}");
        }
      };
    }

    public IaConnectionPulling<T>? GetIaConnection(string jobWebServiceId) =>
        _connections.FirstOrDefault(c => c.JobWebServiceId == jobWebServiceId);

    public List<IaConnectionPulling<T>> SearchConnectionsByConnectionId(string connectionId) =>
        _connections.FindAll(c => c.HasConnectionId(connectionId));

    public void DeleteIaConnection(string jobServiceId)
    {
      var connection = GetIaConnection(jobServiceId);
      if (connection != null)
      {
        connection.StopPulling();
        _connections.Remove(connection);
      }
    }

    public List<IaConnectionPulling<T>> SearchForConnectionIdInConnection(string connectionId)
    {
      return _connections.FindAll(c => c.HasConnectionId(connectionId));
    }

    public virtual List<IaConnectionPulling<T>> GetAllConnections()
    {
      return _connections;
    }
  }

  public abstract class IaConnectionPulling<T>(string jobWebServiceId, IHubContext<T> hub, IServiceScopeFactory scopeFactory) where T : ApplicationHub
  {
    public IHubContext<T> Hub { get; } = hub;
    public string Status { get; set; } = "Waiting for interval";
    public string JobWebServiceId { get; } = jobWebServiceId;
    public bool IsPulling { get; set; } = false;
    private readonly List<string> _connectionIds = [];
    protected System.Timers.Timer _timer = new System.Timers.Timer(1000 * 10);
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
        Console.WriteLine($"Error occured in interval for job {JobWebServiceId}: {ex.Message}");
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
      Console.WriteLine($"Stop pulling for job {JobWebServiceId}");
      _timer.Stop();
      _timer.Enabled = false;
      IsPulling = false;
    }

    public void AddConnectionId(string connectionId) => _connectionIds.Add(connectionId);

    public void RemoveConnectionId(string connectionId) => _connectionIds.RemoveAll(id => id == connectionId);

    public bool HasConnectionId(string connectionId) => _connectionIds.Contains(connectionId);

    public int GetConnectionIdsCount() => _connectionIds.Count;
  }
}