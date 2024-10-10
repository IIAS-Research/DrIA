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
  public class IaPullingService(IHubContext<IaStatusHub> iaStatusHub, IHubContext<IaMetricHub> iaMetricHub, IServiceScopeFactory scopeFactory) : ApplicationService
  {
    public static readonly string _url = "http://rss00992:32845";
    private readonly List<IaConnectionPulling> _connections = [];
    private readonly IHubContext<IaStatusHub> _iaStatusHub = iaStatusHub;
    private readonly IHubContext<IaMetricHub> _iaMetricHub = iaMetricHub;
    private readonly IServiceScopeFactory _scopeFactory = scopeFactory;

    public IaConnectionPulling? GetIaConnection(string jobWebServiceId) =>
        _connections.FirstOrDefault(c => c.JobWebServiceId == jobWebServiceId);

    public IaConnectionPulling CreateIaConnection(string jobWebServiceId, Type hubType)
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

      var newConnection = new IaConnectionPulling(jobWebServiceId, hub, _scopeFactory);
      _connections.Add(newConnection);
      return newConnection;
    }

    public List<IaConnectionPulling> SearchConnectionsByConnectionId(string connectionId) =>
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

    public List<IaConnectionPulling> SearchForConnectionIdInConnection(string connectionId)
    {
      return _connections.FindAll(c => c.HasConnectionId(connectionId));
    }
  }

  public class IaConnectionPulling(string jobWebServiceId, IHubContext<ApplicationHub> hub, IServiceScopeFactory scopeFactory)
  {
    public IHubContext<ApplicationHub> Hub { get; } = hub;
    public string JobWebServiceId { get; } = jobWebServiceId;
    public bool IsPulling { get; set; } = false;
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
        var iaWebService = scope.ServiceProvider.GetRequiredService<IaWebService>();
        var dbContext = scope.ServiceProvider.GetRequiredService<MainDbContext>();

        Console.WriteLine("Interval elapsed so RunInterval method has been triggered");
        var responseGetStatus = await iaWebService.GetJobStatus(JobWebServiceId);

        if (!responseGetStatus.IsSuccess)
        {
          await SendErrorsToWs(responseGetStatus);
          return;
        }

        Status = responseGetStatus.Data.status.current;

        // If status has changed, update it in DB
        var currentProcess = await dbContext.Processes.FirstOrDefaultAsync(p => p.IaRemoteId == responseGetStatus.Data.id);

        if (currentProcess != null && responseGetStatus.Data.status.current != currentProcess.Status)
        {
          currentProcess.Status = Status;
          await dbContext.SaveChangesAsync();
        }

        Console.WriteLine($"PULLING => Job status: {Status}");

        switch (Status)
        {
          case Process.EProcessStatut.QUEUED:
            _timer.Enabled = true;
            break;
          case Process.EProcessStatut.RUNNING:
            _timer.Enabled = true;
            break;
          case Process.EProcessStatut.FINISHED:
            var onProcessFinishResponse = await OnProcessFinish(responseGetStatus.Data);

            if (!onProcessFinishResponse.IsSuccess)
            {
              await SendErrorsToWs(onProcessFinishResponse);
              return;
            }
            StopPulling();
            await Hub.Clients.Group(JobWebServiceId).SendAsync("ReceiveMessage", responseGetStatus.Data.status.current.ToString());
            break;
          case Process.EProcessStatut.CANCELED:
            StopPulling();
            await Hub.Clients.Group(JobWebServiceId).SendAsync("StopMessage", $"Process terminé avec status : {Status}");
            break;
          case Process.EProcessStatut.CRASHED:
            StopPulling();
            await Hub.Clients.Group(JobWebServiceId).SendAsync("StopMessage", $"Process terminé avec status : {Status}");
            break;
          case Process.EProcessStatut.TIMEOUT:
            StopPulling();
            await Hub.Clients.Group(JobWebServiceId).SendAsync("StopMessage", $"Process terminé avec status : {Status}");
            break;
        }
      }
    }

    private async Task SendErrorsToWs<T>(ResponseService<T> responseService)
    {
      await Hub.Clients.Group(JobWebServiceId).SendAsync("ReceiveMessage", $"Une erreur est survenue : {responseService.Errors}");
      StopPulling();
    }

    private async Task<ResponseService<List<Model>>> OnProcessFinish(JobStatusOutDTO jobStatusOutDTO)
    {
      using (var scope = ScopeFactory.CreateScope())
      {
        var processRepository = scope.ServiceProvider.GetRequiredService<ProcessRepository>();
        var modelService = scope.ServiceProvider.GetRequiredService<ModelService>();
        var iaWebService = scope.ServiceProvider.GetRequiredService<IaWebService>();

        var responseOnProcessFinish = new ResponseService<List<Model>>();
        // Get metrics
        var responseGetMetric = await iaWebService.RetrieveJobMetric(JobWebServiceId);
        if (!responseGetMetric.IsSuccess)
        {
          Console.WriteLine("Erreur fetch metrics : " + responseGetMetric.Errors.ToString());
          return responseOnProcessFinish;
        } 

        List<Model> modelList = new List<Model>();

        // For each model, download the model file (binary), generate bibliography and save infos in DB
        foreach (var model in responseGetMetric.Data.metrics)
        {
          var idModelProperty = model.GetType().GetProperty("id");
          var idValue = idModelProperty.GetValue(model);

          // Ensure the idValue is not null and can be parsed to an int
          if (idValue != null && Int32.TryParse(idValue.ToString(), out int modelId))
          {
            if (idModelProperty != null)
            {
              // Download model file
              var downloadModelFileResponse = await iaWebService.DownloadModelFile(responseGetMetric.Data.id, modelId);

              // Generate bibliography of the model
              var responseBibliography = await iaWebService.GetBibliography(responseGetMetric.Data.id, modelId);

              // Save all this previous stuff in DB
              var process = await processRepository.GetProcessByIaRemoteId(responseGetMetric.Data.id);
              if (process == null || !responseBibliography.IsSuccess) return responseOnProcessFinish;

              var createModelResponse = await modelService.CreateModel(process.Id, model, responseBibliography.Data, model.id);
              if (!createModelResponse.IsSuccess) return responseOnProcessFinish;

              await modelService.CreateModelFile(downloadModelFileResponse.Data, createModelResponse.Data.Id);


              // Add the saved model to the modelList
              modelList.Add(createModelResponse.Data);
            }
          }
        }

        if (modelList.Count > 0) responseOnProcessFinish.SetSuccessful(modelList);
        return responseOnProcessFinish;
      }
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