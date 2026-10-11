using System.Text.Json;
using System.Timers;
using Microsoft.AspNetCore.SignalR;
using Server.DTOs;
using Server.Hubs;
using Server.Models;

namespace Server.Services;

public class IaPullingExplainService : IaPullingService<IaExplainModelHub>
{
  public IaPullingExplainService(IServiceScopeFactory scopeFactory, IHubContext<IaExplainModelHub> hub) : base(scopeFactory, hub) { }

  public IaConnectionExplainPulling CreateIaConnection(string jobWebServiceId, long localModelId)
  {
    lock (_connections)
    {
      if (_connections.FirstOrDefault(c => c.JobWebServiceId == jobWebServiceId) is IaConnectionExplainPulling existing) return existing;
      var connection = new IaConnectionExplainPulling(jobWebServiceId, _hub, _scopeFactory, localModelId);
      _connections.Add(connection);
      return connection;
    }
  }

  public class IaConnectionExplainPulling : IaConnectionPulling<IaExplainModelHub>
  {
    private readonly long _modelId;
    public IaConnectionExplainPulling(string jobWebServiceId, IHubContext<IaExplainModelHub> hub, IServiceScopeFactory scopeFactory, long localModelId)
      : base(jobWebServiceId, hub, scopeFactory) => _modelId = localModelId;

    protected override async Task RunInterval(object source, ElapsedEventArgs elapseArg)
    {
      using var scope = ScopeFactory.CreateScope();
      var ia = scope.ServiceProvider.GetRequiredService<IaWebService>();
      var service = scope.ServiceProvider.GetRequiredService<ModelService>();
      var current = await service.GetModelById(_modelId);
      if (!current.IsSuccess || string.IsNullOrWhiteSpace(current.Data.Explanation) || JsonSerializer.Deserialize<Model.ModelExplanation>(current.Data.Explanation)?.id != JobWebServiceId)
      {
        StopPulling();
        return;
      }
      var response = await ia.GetExplainStatus(JobWebServiceId);
      if (!response.IsSuccess)
      {
        if (response.HttpStatusCode is 404 or 409)
        {
          await SaveState(service, "CRASHED", string.Join("; ", response.Errors));
          await Hub.Clients.Group(JobWebServiceId).SendAsync("StopMessage", string.Join("; ", response.Errors));
          StopPulling();
        }
        else _timer.Start();
        return;
      }
      Status = response.Data.status.current.ToString();
      if (Status is "FINISHED" or "CACHED")
      {
        var complete = await OnProcessFinish();
        if (!complete.IsSuccess) { _timer.Start(); return; }
        await Hub.Clients.Group(JobWebServiceId).SendAsync("ReceiveMessage", Status);
        StopPulling();
      }
      else if (Status == "CRASHED")
      {
        var partial = await OnProcessFinish();
        if (!partial.IsSuccess) await SaveState(service, Status, response.Data.error, response.Data.report, response.Data.cache_error);
        await Hub.Clients.Group(JobWebServiceId).SendAsync("StopMessage", response.Data.error ?? "L’analyse a échoué.");
        StopPulling();
      }
      else
      {
        await SaveState(service, Status, response.Data.error, response.Data.report, response.Data.cache_error);
        if (IsPulling) _timer.Start();
      }
    }

    private async Task SaveState(ModelService service, string status, string? error, JsonElement? report = null, string? cacheError = null)
    {
      var model = await service.GetModelById(_modelId);
      if (!model.IsSuccess) return;
      var stored = string.IsNullOrWhiteSpace(model.Data.Explanation) ? new Model.ModelExplanation() : JsonSerializer.Deserialize<Model.ModelExplanation>(model.Data.Explanation) ?? new();
      stored.id = JobWebServiceId;
      stored.status = status;
      stored.error = error;
      if (report.HasValue) stored.report = report;
      stored.cache_error = cacheError;
      if (!await service.SaveModelExplanationIfCurrent(model.Data, stored, JobWebServiceId)) StopPulling();
    }

    protected override async Task<ResponseService> OnProcessFinish()
    {
      using var scope = ScopeFactory.CreateScope();
      var service = scope.ServiceProvider.GetRequiredService<ModelService>();
      var ia = scope.ServiceProvider.GetRequiredService<IaWebService>();
      var response = new ResponseService();
      var model = await service.GetModeleWithProcessById(_modelId);
      if (!response.EnsureOtherServiceIsSuccess(model)) return response;
      if (string.IsNullOrWhiteSpace(model.Data.Explanation) || JsonSerializer.Deserialize<Model.ModelExplanation>(model.Data.Explanation)?.id != JobWebServiceId)
      {
        StopPulling();
        response.SetSuccessful();
        return response;
      }
      var results = await ia.GetExplainResults(JobWebServiceId);
      if (!response.EnsureOtherServiceIsSuccess(results)) return response;
      var explanation = new Model.ModelExplanation
      {
        id = JobWebServiceId,
        status = results.Data.status.current.ToString(),
        results = results.Data.results,
        report = results.Data.report,
        error = results.Data.error,
        cache_error = results.Data.cache_error
      };
      foreach (var (key, result) in explanation.results)
      {
        if (result.ValueKind != JsonValueKind.Object || !result.TryGetProperty("kind", out var kind)) continue;
        if (kind.GetString() == "plot" && result.TryGetProperty("image", out _))
        {
          var plot = JsonSerializer.Deserialize<Plot>(result.GetRawText());
          if (plot != null) explanation.plots[key] = plot;
        }
        if (kind.GetString() == "shap" && result.TryGetProperty("features", out var features) && features.ValueKind == JsonValueKind.Object)
        {
          explanation.features = features.EnumerateObject().Select(f => new Model.ModelExplanation.ModelExplainFeature { name = f.Name, value = f.Value.Clone() }).ToArray();
        }
      }
      if (model.Data.Process != null)
      {
        var configured = await ia.GetTextExplanation(model.Data.Process.IaRemoteId, model.Data.IaRemoteId);
        if (configured.IsSuccess)
        {
          explanation.configured_report = configured.Data.configured_report;
        }
      }
      if (!await service.SaveModelExplanationIfCurrent(model.Data, explanation, JobWebServiceId)) StopPulling();
      response.SetSuccessful();
      return response;
    }
  }
}
