using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Server.DAL.Db;
using Server.Models;

namespace Server.Services;

// Restore local persistence work after an API restart; it does not depend on SignalR clients.
public class IaPullingRecoveryService(IServiceScopeFactory scopeFactory, IaPullingModelService models, IaPullingExplainService explanations, ILogger<IaPullingRecoveryService> logger) : BackgroundService
{
  protected override async Task ExecuteAsync(CancellationToken stoppingToken)
  {
    var initial = true;
    while (!stoppingToken.IsCancellationRequested)
    {
      try
      {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MainDbContext>();
        var jobs = await db.Processes.Where(p => p.Status == Process.EProcessStatut.QUEUED || p.Status == Process.EProcessStatut.RUNNING || (initial && (p.Status == Process.EProcessStatut.FINISHED || p.Status == Process.EProcessStatut.CACHED))).Select(p => new { p.IaRemoteId, p.Status, p.StatusSnapshotJson, HasModels = p.Models.Any(), MissingFile = p.Models.Any(m => !db.ModelFiles.Any(f => f.ModelId == m.Id)) }).ToListAsync(stoppingToken);
        foreach (var job in jobs)
        {
          if (job.Status is Process.EProcessStatut.FINISHED or Process.EProcessStatut.CACHED)
          {
            if (ResultsPersisted(job.StatusSnapshotJson) || (job.StatusSnapshotJson == null && job.HasModels && !job.MissingFile)) continue;
          }
          await models.CreateIaConnection(job.IaRemoteId).StartPulling();
        }
        var baselines = await db.Projects.Select(p => new { p.Id, p.MetricsBaselineJson }).ToListAsync(stoppingToken);
        foreach (var project in baselines)
        {
          if (TryGetPendingId(project.MetricsBaselineJson, out var id, recoverBaselineResults: true)) await models.CreateIaConnection(id!, project.Id).StartPulling();
        }
        var explainers = await db.Models.Where(m => m.Explanation != null).Select(m => new { m.Id, m.Explanation }).ToListAsync(stoppingToken);
        foreach (var model in explainers)
        {
          if (TryGetPendingId(model.Explanation!, out var id)) await explanations.CreateIaConnection(id!, model.Id).StartPulling();
        }
        initial = false;
      }
      catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
      {
        logger.LogWarning(ex, "Impossible de reprendre le suivi IAML ; nouvel essai dans 30 secondes.");
      }
      await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
    }
  }

  private static bool ResultsPersisted(string? json)
  {
    if (json == null) return false;
    try
    {
      using var document = JsonDocument.Parse(json);
      return document.RootElement.TryGetProperty("results_persisted", out var persisted) && persisted.ValueKind == JsonValueKind.True;
    }
    catch (JsonException) { return false; }
  }

  private static bool TryGetPendingId(string json, out string? id, bool recoverBaselineResults = false)
  {
    id = null;
    if (string.IsNullOrWhiteSpace(json)) return false;
    try
    {
      using var document = JsonDocument.Parse(json);
      var root = document.RootElement;
      if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("id", out var idValue) || !root.TryGetProperty("status", out var status)) return false;
      var state = status.ValueKind == JsonValueKind.Object && status.TryGetProperty("current", out var current) ? current.GetString() : status.ValueKind == JsonValueKind.String ? status.GetString() : null;
      if (state is not ("QUEUED" or "RUNNING" or "COMPUTING"))
      {
        if (!recoverBaselineResults || state is not ("FINISHED" or "CACHED")) return false;
        if (ResultsPersisted(json)) return false;
        // Baselines stored before the marker existed are complete when their
        // metrics were saved. Resume only the interrupted finalisation window.
        if (root.TryGetProperty("metrics", out var metrics) &&
            ((metrics.ValueKind == JsonValueKind.Array && metrics.GetArrayLength() > 0) ||
             (metrics.ValueKind == JsonValueKind.Object && metrics.EnumerateObject().Any()))) return false;
      }
      id = idValue.GetString();
      return !string.IsNullOrEmpty(id);
    }
    catch (JsonException) { return false; }
  }
}
