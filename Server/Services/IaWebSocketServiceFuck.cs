using System.Net.WebSockets;
using System.Timers;
using Microsoft.AspNetCore.SignalR;
using Server.Hubs;
using Server.Models;
using Server.Repositories;
using SignalRChat.Hubs;
using static Server.Services.ApplicationService;

namespace Server.Services
{
  public class IAWebSocketServiceFuck(IHubContext<IaStatusHub> iaStatusHub, IHubContext<IaMetricHub> iaMetricHub, ProcessRepository processRepository, IaWebService iaWebService) : ApplicationService
  {
    public static readonly string _url = "http://rss00992:32845";
    private readonly List<IaConnectionFuck> _connections = [];
    private readonly IHubContext<IaStatusHub> _iaStatusHub = iaStatusHub;
    private readonly IHubContext<IaMetricHub> _iaMetricHub = iaMetricHub;
    private readonly ProcessRepository _processRepository = processRepository;
    private IaWebService _iaWebService = iaWebService;

    public IaConnectionFuck? GetIaConnection(string jobWebServiceId) =>
        _connections.FirstOrDefault(c => c.JobWebServiceId == jobWebServiceId);

    public IaConnectionFuck CreateIaConnection(string jobWebServiceId, Type hubType)
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

      var newConnection = new IaConnectionFuck(jobWebServiceId, hub, _processRepository, _iaWebService);
      _connections.Add(newConnection);
      return newConnection;
    }

    public List<IaConnectionFuck> SearchConnectionsByConnectionId(string connectionId) =>
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

    public List<IaConnectionFuck> SearchForConnectionIdInConnection(string connectionId)
    {
      return _connections.FindAll(c => c.HasConnectionId(connectionId));
    }
  }

  public class IaConnectionFuck(string jobWebServiceId, IHubContext<ApplicationHub> hub, ProcessRepository processRepository, IaWebService iaWebService)
  {
    public IHubContext<ApplicationHub> Hub { get; } = hub;
    public string JobWebServiceId { get; } = jobWebServiceId;
    public bool IsPulling { get; set; } = false;
    public ProcessRepository ProcessRepository = processRepository;
    public IaWebService IaWebService = iaWebService;
    public Process.EProcessStatut Statut { get; set;} = Process.EProcessStatut.QUEUED;
    private readonly List<string> _connectionIds = [];
    private System.Timers.Timer _timer = new System.Timers.Timer(1000 * 60);


    public async Task StartPulling()
    {
      IsPulling = true;
      try
      {
        _timer.Enabled = true;
        _timer.AutoReset = false;
        _timer.Elapsed += RunInterval;
      }
      catch (Exception ex)
      {
        Console.WriteLine($"Connection failed: {ex.Message}");
      }
    }

    private async void RunInterval(object source, ElapsedEventArgs elapseArg)
    {
      var responseGetStatus = await IaWebService.GetJobStatus(JobWebServiceId);
      if (!responseGetStatus.IsSuccess)
      {
        await SendErrorsToWs(responseGetStatus);
        return;
      }

      switch(Statut)
      {
        case Process.EProcessStatut.QUEUED:
        case Process.EProcessStatut.RUNNING:
          // Bah rien en fait
          break;
        case Process.EProcessStatut.FINISHED:
          var responseGetMetric = await IaWebService.RetrieveJobMetric(JobWebServiceId);
          if (!responseGetMetric.IsSuccess)
          {
            await SendErrorsToWs(responseGetMetric);
            return;
          }
          // get metrics + save

          // je comprends plus rien à ce projet de mort
          await Hub.Clients.Group(JobWebServiceId).SendAsync("ReceiveMessage", "");
          break;
        case Process.EProcessStatut.CANCELED:
        case Process.EProcessStatut.CRASHED:
        case Process.EProcessStatut.TIMEOUT:
          await Hub.Clients.Group(JobWebServiceId).SendAsync("ReceiveMessage", $"Process terminé avec status : {Statut}");
          StopPulling();
          break;
      }
      await ProcessRepository.UpdateProcessStatus(JobWebServiceId, Statut);
    }

    private async Task SendErrorsToWs<T>(ResponseService<T> responseService)
    {
      await Hub.Clients.Group(JobWebServiceId).SendAsync("ReceiveMessage", $"Une erreur est survenue : {responseService.Errors}");
      StopPulling();
    }

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