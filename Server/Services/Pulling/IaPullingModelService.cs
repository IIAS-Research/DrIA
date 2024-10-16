using System.Net.WebSockets;
using System.Timers;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Server.DAL.Db;
using Server.Hubs;
using Server.Models;
using Server.Repositories;

namespace Server.Services;
public class IaPullingModelService : IaPullingService<IaGenerateModelHub>
{
  public IaPullingModelService(IServiceScopeFactory scopeFactory, IHubContext<IaGenerateModelHub> hub) : base(scopeFactory, hub)
  { }

  public IaConnectionModelPulling CreateIaConnection(string jobWebServiceId)
  {
    var newConnection = new IaConnectionModelPulling(jobWebServiceId, _hub, _scopeFactory);
    _connections.Add(newConnection);
    return newConnection;
  }

  public class IaConnectionModelPulling : IaConnectionPulling<IaGenerateModelHub>
  {
    public IaConnectionModelPulling(string jobWebServiceId, IHubContext<IaGenerateModelHub> hub, IServiceScopeFactory scopeFactory)
    : base(jobWebServiceId, hub, scopeFactory) { }

    protected override async Task RunInterval(object source, ElapsedEventArgs elapseArg)
    {
      using (var scope = ScopeFactory.CreateScope())
      {
        var iaWebService = scope.ServiceProvider.GetRequiredService<IaWebService>();
        var dbContext = scope.ServiceProvider.GetRequiredService<MainDbContext>();

        var responseGetStatus = await iaWebService.GetJobStatus(JobWebServiceId);

        if (!responseGetStatus.IsSuccess)
        {
          Console.WriteLine($"Get Job Status in Pulling Failed => {responseGetStatus.Errors}");
          var responseWs = new ResponseService();
          responseWs.EnsureOtherServiceIsSuccess(responseGetStatus);
          await SendErrorsToWs(responseWs);
          StopPulling();
          return;
        }

        Status = responseGetStatus.Data.status.current.ToString();

        // If status has changed, update it in DB
        var currentProcess = await dbContext.Processes.FirstOrDefaultAsync(p => p.IaRemoteId == responseGetStatus.Data.id);

        if (currentProcess != null && responseGetStatus.Data.status.current != currentProcess.Status)
        {
          currentProcess.Status = responseGetStatus.Data.status.current;
          await dbContext.SaveChangesAsync();
        }

        Console.WriteLine($"PULLING => Job status: {Status}");
        switch (responseGetStatus.Data.status.current)
        {
          case Process.EProcessStatut.QUEUED:
            _timer.Enabled = true;
            break;
          case Process.EProcessStatut.RUNNING:
            _timer.Enabled = true;
            break;
          case Process.EProcessStatut.FINISHED:
            var onProcessFinishResponse = await OnProcessFinish();

            if (!onProcessFinishResponse.IsSuccess)
            {
              var responseWs = new ResponseService();
              responseWs.EnsureOtherServiceIsSuccess(onProcessFinishResponse);
              await SendErrorsToWs(responseWs);
              return;
            }
            await Hub.Clients.Group(JobWebServiceId).SendAsync("ReceiveMessage", responseGetStatus.Data.status.current.ToString());
            StopPulling();
            break;
          case Process.EProcessStatut.CANCELED:
            await Hub.Clients.Group(JobWebServiceId).SendAsync("StopMessage", $"Process terminé avec status : {Status}");
            StopPulling();
            break;
          case Process.EProcessStatut.CRASHED:
            await Hub.Clients.Group(JobWebServiceId).SendAsync("StopMessage", $"Process terminé avec status : {Status}");
            StopPulling();
            break;
          case Process.EProcessStatut.TIMEOUT:
            await Hub.Clients.Group(JobWebServiceId).SendAsync("StopMessage", $"Process terminé avec status : {Status}");
            StopPulling();
            break;
        }
      }
    }

    protected override async Task<ResponseService> OnProcessFinish()
    {
      using (var scope = ScopeFactory.CreateScope())
      {
        var processRepository = scope.ServiceProvider.GetRequiredService<ProcessRepository>();
        var modelService = scope.ServiceProvider.GetRequiredService<ModelService>();
        var iaWebService = scope.ServiceProvider.GetRequiredService<IaWebService>();

        var responseOnProcessFinish = new ResponseService();
        // Get metrics
        var responseGetMetric = await iaWebService.RetrieveJobMetric(JobWebServiceId);
        if (!responseGetMetric.IsSuccess)
        {
          return responseOnProcessFinish;
        }

        List<Model> modelList = new List<Model>();

        // For each model, download the model file (binary), generate bibliography and save infos in DB
        foreach (var model in responseGetMetric.Data.metrics)
        {
          var idModelProperty = model.GetType().GetProperty("id");
          var idValue = idModelProperty.GetValue(model);

          // Ensure the idValue is not null and can be parsed to an int
          if (idValue != null && Int32.TryParse(idValue.ToString(), out int remoteModelId))
          {
            if (idModelProperty != null)
            {
              // Download model file
              var downloadModelFileResponse = await iaWebService.DownloadModelFile(responseGetMetric.Data.id, remoteModelId);

              // Generate bibliography of the model
              var responseBibliography = await iaWebService.GetBibliography(responseGetMetric.Data.id, remoteModelId);

              // Get text explanation
              var responseTextExplanation = await iaWebService.GetTextExplanation(responseGetMetric.Data.id, remoteModelId);

              // Save all this previous stuff in DB
              var process = await processRepository.GetProcessByIaRemoteId(responseGetMetric.Data.id);
              if (process == null || !responseBibliography.IsSuccess) return responseOnProcessFinish;

              var createModelResponse = await modelService.CreateModel(process.Id, model, responseBibliography.Data, remoteModelId);
              if (!createModelResponse.IsSuccess) return responseOnProcessFinish;

              await modelService.SaveModelTextExplanation(createModelResponse.Data, responseTextExplanation.Data.explanations);

              await modelService.CreateModelFile(downloadModelFileResponse.Data, createModelResponse.Data.Id);


              // Add the saved model to the modelList
              modelList.Add(createModelResponse.Data);
            }
          }
        }

        if (modelList.Count > 0) responseOnProcessFinish.SetSuccessful();
        return responseOnProcessFinish;
      }
    }

  }
}