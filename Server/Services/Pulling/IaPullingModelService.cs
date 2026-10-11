using System.Text.Json;
using System.Text.Json.Nodes;
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
  public IaPullingModelService(IServiceScopeFactory scopeFactory, IHubContext<IaGenerateModelHub> hub) : base(scopeFactory, hub) { }

  public IaConnectionModelPulling CreateIaConnection(string jobWebServiceId, long? baselineProjectId = null)
  {
    lock (_connections)
    {
      if (_connections.FirstOrDefault(c => c.JobWebServiceId == jobWebServiceId) is IaConnectionModelPulling existing) return existing;
      var connection = new IaConnectionModelPulling(jobWebServiceId, _hub, _scopeFactory, baselineProjectId);
      _connections.Add(connection);
      return connection;
    }
  }

  public class IaConnectionModelPulling : IaConnectionPulling<IaGenerateModelHub>
  {
    private readonly long? _baselineProjectId;
    public IaConnectionModelPulling(string jobWebServiceId, IHubContext<IaGenerateModelHub> hub, IServiceScopeFactory scopeFactory, long? baselineProjectId = null)
      : base(jobWebServiceId, hub, scopeFactory) => _baselineProjectId = baselineProjectId;

    protected override async Task RunInterval(object source, ElapsedEventArgs elapseArg)
    {
      using var scope = ScopeFactory.CreateScope();
      var ia = scope.ServiceProvider.GetRequiredService<IaWebService>();
      var db = scope.ServiceProvider.GetRequiredService<MainDbContext>();
      var response = await ia.GetJobStatus(JobWebServiceId);
      if (!response.IsSuccess)
      {
        if (response.HttpStatusCode is 404 or 409)
        {
          Status = "CRASHED";
          if (_baselineProjectId.HasValue)
          {
            var project = await db.Projects.FindAsync(_baselineProjectId.Value);
            if (project != null) project.MetricsBaselineJson = JsonSerializer.Serialize(new { id = JobWebServiceId, status = new { current = Status }, metrics = Array.Empty<object>(), error = string.Join("; ", response.Errors), http_status = response.HttpStatusCode });
          }
          else
          {
            var process = await db.Processes.FirstOrDefaultAsync(p => p.IaRemoteId == JobWebServiceId);
            if (process != null)
            {
              var unavailableSnapshot = JsonNode.Parse(process.StatusSnapshotJson ?? "{}") as JsonObject ?? new();
              var modelFiles = await db.Models.Where(m => m.ProcessId == process.Id)
                .Select(m => db.ModelFiles.Any(f => f.ModelId == m.Id)).ToListAsync();
              if (!modelFiles.Any(hasFile => hasFile)) process.Status = Process.EProcessStatut.CRASHED;
              unavailableSnapshot["id"] = JobWebServiceId;
              var status = unavailableSnapshot["status"] as JsonObject;
              if (status == null) { status = new JsonObject(); unavailableSnapshot["status"] = status; }
              status["current"] = process.Status.ToString();
              unavailableSnapshot["availability_error"] = string.Join("; ", response.Errors);
              unavailableSnapshot["http_status"] = response.HttpStatusCode;
              if (unavailableSnapshot["error"] == null) unavailableSnapshot["error"] = string.Join("; ", response.Errors);
              if (unavailableSnapshot["results_persisted"] == null)
                unavailableSnapshot["results_persisted"] = modelFiles.Count > 0 && modelFiles.All(hasFile => hasFile);
              process.StatusSnapshotJson = unavailableSnapshot.ToJsonString();
            }
          }
          await db.SaveChangesAsync();
          await Hub.Clients.Group(JobWebServiceId).SendAsync("StopMessage", string.Join("; ", response.Errors));
          StopPulling();
        }
        else _timer.Start(); // A temporary outage does not make a running job fail.
        return;
      }

      Status = response.Data.status.current.ToString();
      var snapshot = JsonNode.Parse(JsonSerializer.Serialize(response.Data))!.AsObject();
      snapshot["status"]!["current"] = Status;
      if (_baselineProjectId.HasValue)
      {
        var project = await db.Projects.FindAsync(_baselineProjectId.Value);
        if (project == null) { StopPulling(); return; }
        var previous = JsonNode.Parse(project.MetricsBaselineJson);
        snapshot["metrics"] = previous is JsonObject && previous["metrics"] != null ? previous["metrics"]!.DeepClone() : new JsonArray();
        project.MetricsBaselineJson = snapshot.ToJsonString();
      }
      else
      {
        var process = await db.Processes.FirstOrDefaultAsync(p => p.IaRemoteId == JobWebServiceId);
        if (process == null) { StopPulling(); return; }
        process.Status = response.Data.status.current;
        process.StatusSnapshotJson = snapshot.ToJsonString();
      }
      await db.SaveChangesAsync();
      await Hub.Clients.Group(JobWebServiceId).SendAsync("JobStatus", snapshot.Deserialize<JsonElement>());

      if (Status is "FINISHED" or "CACHED")
      {
        var completed = await OnProcessFinish();
        if (!completed.IsSuccess)
        {
          // Keep trying incomplete local downloads without duplicating candidates.
          _timer.Start();
          return;
        }
        await Hub.Clients.Group(JobWebServiceId).SendAsync("ReceiveMessage", Status);
        StopPulling();
      }
      else if (Status is "CRASHED" or "CANCELED" or "TIMEOUT")
      {
        await Hub.Clients.Group(JobWebServiceId).SendAsync("StopMessage", response.Data.error ?? $"Traitement terminé : {Status}");
        StopPulling();
      }
      else _timer.Start();
    }

    protected override async Task<ResponseService> OnProcessFinish()
    {
      using var scope = ScopeFactory.CreateScope();
      var ia = scope.ServiceProvider.GetRequiredService<IaWebService>();
      var db = scope.ServiceProvider.GetRequiredService<MainDbContext>();
      var response = new ResponseService();
      var metrics = await ia.RetrieveJobMetric(JobWebServiceId);
      if (!response.EnsureOtherServiceIsSuccess(metrics)) return response;

      if (_baselineProjectId.HasValue)
      {
        var project = await db.Projects.FindAsync(_baselineProjectId.Value);
        if (project == null) { response.AddError("Projet introuvable."); return response; }
        var snapshot = JsonNode.Parse(project.MetricsBaselineJson)!.AsObject();
        snapshot["metrics"] = JsonSerializer.SerializeToNode(metrics.Data.metrics);
        snapshot["results_persisted"] = true;
        project.MetricsBaselineJson = snapshot.ToJsonString();
        await db.SaveChangesAsync();
        response.SetSuccessful();
        return response;
      }

      var repository = scope.ServiceProvider.GetRequiredService<ProcessRepository>();
      var service = scope.ServiceProvider.GetRequiredService<ModelService>();
      var process = await repository.GetProcessByIaRemoteId(JobWebServiceId);
      if (process == null) { response.AddError("Traitement introuvable."); return response; }
      if (metrics.Data.metrics.Length == 0) { response.AddError("Aucun candidat disponible."); return response; }

      foreach (var metric in metrics.Data.metrics)
      {
        var existing = await db.Models.FirstOrDefaultAsync(m => m.ProcessId == process.Id && m.IaRemoteId == metric.id);
        if (existing != null && await db.ModelFiles.AnyAsync(f => f.ModelId == existing.Id))
        {
          if (existing.ReportJson == null || existing.AuditJson == null || existing.ReportMarkdown == null) await SaveReports(service, ia, existing);
          continue;
        }
        var file = await ia.DownloadModelFile(JobWebServiceId, metric.id);
        if (!response.EnsureOtherServiceIsSuccess(file)) return response;
        var bibliography = await ia.GetBibliography(JobWebServiceId, metric.id);
        if (!response.EnsureOtherServiceIsSuccess(bibliography)) return response;
        var created = await service.CreateModel(process.Id, metric, bibliography.Data, metric.id);
        if (!response.EnsureOtherServiceIsSuccess(created)) return response;
        var explanation = await ia.GetTextExplanation(JobWebServiceId, metric.id);
        if (explanation.IsSuccess) await service.SaveModelTextExplanation(created.Data, explanation.Data.explanations);
        var saved = await service.CreateModelFile(file.Data, created.Data.Id);
        if (!response.EnsureOtherServiceIsSuccess(saved)) return response;
        await SaveReports(service, ia, created.Data);
      }
      var completeSnapshot = JsonNode.Parse(process.StatusSnapshotJson ?? "{}")!.AsObject();
      completeSnapshot["results_persisted"] = true;
      process.StatusSnapshotJson = completeSnapshot.ToJsonString();
      await db.SaveChangesAsync();
      response.SetSuccessful();
      return response;
    }

    private async Task SaveReports(ModelService service, IaWebService ia, Model model)
    {
      var report = await ia.GetModelReport(JobWebServiceId, model.IaRemoteId);
      var audit = await ia.GetModelAudit(JobWebServiceId, model.IaRemoteId);
      var markdown = await ia.GetModelMarkdownReport(JobWebServiceId, model.IaRemoteId);
      var text = markdown.IsSuccess ? System.Text.Encoding.UTF8.GetString(await IaWebService.IFormFileToByteArray(markdown.Data)) : null;
      await service.SaveReports(model, report.IsSuccess ? report.Data : null, audit.IsSuccess ? audit.Data : null, text);
    }
  }
}
