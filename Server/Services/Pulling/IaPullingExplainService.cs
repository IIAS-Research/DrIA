using System.Net.WebSockets;
using System.Timers;
using Microsoft.AspNetCore.SignalR;
using Server.DAL.Db;
using Server.DTOs;
using Server.Hubs;
using Server.Models;

namespace Server.Services;

public class IaPullingExplainService : IaPullingService<IaExplainModelHub>
{
  public IaPullingExplainService(IServiceScopeFactory scopeFactory, IHubContext<IaExplainModelHub> hub) : base(scopeFactory, hub)
  { }

  public IaConnectionExplainPulling CreateIaConnection(string jobWebServiceId, long localModelId)
  {
    var newConnection = new IaConnectionExplainPulling(jobWebServiceId, _hub, _scopeFactory, localModelId);
    _connections.Add(newConnection);
    return newConnection;
  }

  public override List<IaConnectionPulling<IaExplainModelHub>> GetAllConnections()
  {
    return _connections;
  }

  public class IaConnectionExplainPulling : IaConnectionPulling<IaExplainModelHub>
  {
    private readonly long _localModelId;
    public IaConnectionExplainPulling(string jobWebServiceId, IHubContext<IaExplainModelHub> hub, IServiceScopeFactory scopeFactory, long localModelId)
    : base(jobWebServiceId, hub, scopeFactory)
    {
      _localModelId = localModelId;
    }

    protected override async Task RunInterval(object source, ElapsedEventArgs elapseArg)
    {
      using (var scope = ScopeFactory.CreateScope())
      {
        var iaWebService = scope.ServiceProvider.GetRequiredService<IaWebService>();
        var dbContext = scope.ServiceProvider.GetRequiredService<MainDbContext>();

        var responseGetStatus = await iaWebService.GetExplainStatus(JobWebServiceId);

        if (!responseGetStatus.IsSuccess)
        {
          Console.WriteLine($"Get Job Status in Pulling Explain Failed => {responseGetStatus.Errors}");
          var responseWs = new ResponseService();
          responseWs.EnsureOtherServiceIsSuccess(responseGetStatus);
          await SendErrorsToWs(responseWs);
          return;
        }

        Status = responseGetStatus.Data.status.current.ToString();
        Console.WriteLine($"PULLING EXPLAINS => Job status: {Status}");
        switch (responseGetStatus.Data.status.current)
        {
          case EExplainStatut.QUEUED:
            _timer.Enabled = true;
            break;
          case EExplainStatut.COMPUTING:
            _timer.Enabled = true;
            break;
          case EExplainStatut.FINISHED:
            var onProcessFinishResponse = await OnProcessFinish();

            if (!onProcessFinishResponse.IsSuccess)
            {
              await SendErrorsToWs(onProcessFinishResponse);
              return;
            }
            await Hub.Clients.Group(JobWebServiceId).SendAsync("ReceiveMessage", responseGetStatus.Data.status.current.ToString());
            StopPulling();
            break;
          case EExplainStatut.CRASHED:
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
        var response = new ResponseService();
        var modelService = scope.ServiceProvider.GetRequiredService<ModelService>();
        var iaWebService = scope.ServiceProvider.GetRequiredService<IaWebService>();

        var responseModele = await modelService.GetModelById(_localModelId);
        if (!response.EnsureOtherServiceIsSuccess(responseModele)) return response;

        var responseShapPlot = await iaWebService.GetExplainShapPlots(JobWebServiceId);
        if (!response.EnsureOtherServiceIsSuccess(responseShapPlot)) return response;

        var responseFeatures = await iaWebService.GetExplainFeatures(JobWebServiceId);
        if (!response.EnsureOtherServiceIsSuccess(responseFeatures)) return response;

        var explanation = new Model.ModelExplanation
        {
          plots = responseShapPlot.Data,
          features = responseFeatures.Data.features
        };

        var responseSaveModel = await modelService.SaveModelExplanation(responseModele.Data, explanation);
        if (!response.EnsureOtherServiceIsSuccess(responseSaveModel)) return response;

        response.SetSuccessful();
        return response;
      }
    }
  }
}

