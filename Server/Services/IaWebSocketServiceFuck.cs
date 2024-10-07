using System.Net.WebSockets;
using System.Timers;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Server.DAL.Db;
using Server.DTOs;
using Server.Hubs;
using Server.Models;
using Server.Repositories;
using SignalRChat.Hubs;
using static Server.Services.ApplicationService;

namespace Server.Services
{
  public class IAWebSocketServiceFuck(IHubContext<IaStatusHub> iaStatusHub, IHubContext<IaMetricHub> iaMetricHub, ProcessRepository processRepository, IaWebService iaWebService, ModelService modelService, IServiceScopeFactory scopeFactory) : ApplicationService
  {
    public static readonly string _url = "http://rss00992:32845";
    private readonly List<IaConnectionFuck> _connections = [];
    private readonly IHubContext<IaStatusHub> _iaStatusHub = iaStatusHub;
    private readonly IHubContext<IaMetricHub> _iaMetricHub = iaMetricHub;
    private readonly ProcessRepository _processRepository = processRepository;
    private readonly ModelService _modelService = modelService;
    private readonly IaWebService _iaWebService = iaWebService;
    private readonly IServiceScopeFactory _scopeFactory = scopeFactory;


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

      var newConnection = new IaConnectionFuck(jobWebServiceId, hub, _processRepository, _iaWebService, _scopeFactory);
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

  public class IaConnectionFuck(string jobWebServiceId, IHubContext<ApplicationHub> hub, ProcessRepository processRepository, IaWebService iaWebService, IServiceScopeFactory scopeFactory)
  {
    public IHubContext<ApplicationHub> Hub { get; } = hub;
    public string JobWebServiceId { get; } = jobWebServiceId;
    public bool IsPulling { get; set; } = false;
    public ProcessRepository ProcessRepository = processRepository;
    public IaWebService IaWebService = iaWebService;
    public Process.EProcessStatut Status { get; set; }
    private readonly List<string> _connectionIds = [];
    private readonly System.Timers.Timer _timer = new System.Timers.Timer(1000 * 10);
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

    private async Task RunInterval(object source, ElapsedEventArgs elapseArg)
    {
      using (var scope = ScopeFactory.CreateScope())
      {
        var dbContext = scope.ServiceProvider.GetRequiredService<MainDbContext>();

        Console.WriteLine("Interval elapsed so RunInterval method has been triggered");
        var responseGetStatus = await IaWebService.GetJobStatus(JobWebServiceId);

        if (!responseGetStatus.IsSuccess)
        {
          await SendErrorsToWs(responseGetStatus);
          return;
        }

        Status = responseGetStatus.Data.status.current;

        // await UpdateProcessStatusIfNeeded(responseGetStatus.Data);

        // If status has changed, update it in DB
        var currentProcess = await dbContext.Processes.FirstOrDefaultAsync(p => p.IaRemoteId == responseGetStatus.Data.id);

        if (currentProcess != null && responseGetStatus.Data.status.current != currentProcess.Status)
        {
          currentProcess.Status = Status;
          await dbContext.SaveChangesAsync();
        }

        Console.WriteLine($"Job status: {Status}");

        switch (Status)
        {
          case Process.EProcessStatut.QUEUED:
            _timer.Enabled = true;
            break;
          case Process.EProcessStatut.RUNNING:
            _timer.Enabled = true;
            break;
          case Process.EProcessStatut.FINISHED:
            var responseGetMetric = await IaWebService.RetrieveJobMetric(JobWebServiceId);
            Console.WriteLine(responseGetMetric);
            // var jobModels = await SaveModelInfos(responseGetMetric);
            // var responseGenerateBibliography = await IaWebService.GenerateBibliography(JobWebServiceId);
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
            Console.WriteLine(responseGetStatus);
            break;
          case Process.EProcessStatut.CRASHED:
            Console.WriteLine(responseGetStatus);
            break;
          case Process.EProcessStatut.TIMEOUT:
            Console.WriteLine("Avant le stop pulling");
            await Hub.Clients.Group(JobWebServiceId).SendAsync("ReceiveMessage", $"Process terminé avec status : {Status}");
            StopPulling();
            break;
        }
      }
    }

    private async Task SendErrorsToWs<T>(ResponseService<T> responseService)
    {
      await Hub.Clients.Group(JobWebServiceId).SendAsync("ReceiveMessage", $"Une erreur est survenue : {responseService.Errors}");
      StopPulling();
    }

    private async Task SaveModelInfos(ResponseService<GetJobMetricsOutDTO> responseService)
    {
      Console.WriteLine($"responseService. in SaveModelInfos: {responseService.Data}");
      foreach (var model in responseService.Data.Metrics)
      {
        var idModelProperty = model.GetType().GetProperty("id")?.ToString();
        if (idModelProperty != null)
        {
          var downloadModelFileResponse = await IaWebService.DownloadModelFile(responseService.Data.IaRemoteId, idModelProperty);
          // var saveModelResponse = await ModelService.SaveModelAndItsFile(model, downloadModelFileResponse);
        }
      }
    }

    // private async Task UpdateProcessStatusIfNeeded(JobStatusOutDTO jobStatus)
    // {
    //   var processStatus = await ProcessRepository.GetProcessStatusById(jobStatus.id);
    //   if (jobStatus.status != processStatus)
    //   {
    //     await ProcessRepository.UpdateProcessStatus(jobStatus.id, jobStatus.status);
    //   }
    // }

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