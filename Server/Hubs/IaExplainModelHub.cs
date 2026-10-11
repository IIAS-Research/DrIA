using Server.Services;
using Server.DAL.Db;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace Server.Hubs;

public class IaExplainModelHub : ApplicationHub
{
  private readonly IaPullingExplainService _iaPullingService;
  private readonly MainDbContext _dbContext;
  public IaExplainModelHub(IaPullingExplainService iaPullingService, MainDbContext dbContext)
  {
    _iaPullingService = iaPullingService;
    _dbContext = dbContext;
  }
  public virtual async Task JoinGroup(string jobWebServiceId, long localModelId)
  {
    try
    {
      if (!long.TryParse(Context.User?.Identity?.Name, out var userId))
      {
        throw new HubException("Authentification requise.");
      }
      var explanation = await _dbContext.Models
          .Where(model => model.Id == localModelId && model.Process!.Project!.UserId == userId)
          .Select(model => model.Explanation)
          .FirstOrDefaultAsync();
      if (string.IsNullOrWhiteSpace(explanation))
      {
        throw new HubException("Cette analyse est introuvable ou inaccessible.");
      }
      using var document = JsonDocument.Parse(explanation);
      var metadata = document.RootElement;
      var storedId = metadata.TryGetProperty("id", out var id) ? id.GetString()
          : metadata.TryGetProperty("explainer_id", out var explainerId) ? explainerId.GetString() : null;
      if (storedId != jobWebServiceId)
      {
        throw new HubException("Cette analyse est introuvable ou inaccessible.");
      }
      Console.WriteLine("Triggered JoinGroup");
      var connection = _iaPullingService.GetIaConnection(jobWebServiceId);
      connection ??= _iaPullingService.CreateIaConnection(jobWebServiceId, localModelId);

      await Groups.AddToGroupAsync(Context.ConnectionId, jobWebServiceId);
      Console.WriteLine($"Connection {Context.ConnectionId} added to group {jobWebServiceId}");
      connection.AddConnectionId(Context.ConnectionId);

      if (!connection.IsPulling)
      {
        await connection.StartPulling();
      }
    }
    catch (Exception ex)
    {
      Console.WriteLine($"Error in JoinGroup: {ex.Message}");
      throw;
    }
  }

  public override async Task OnDisconnectedAsync(Exception? exception)
  {
    try
    {
      foreach (var connection in _iaPullingService.SearchForConnectionIdInConnection(Context.ConnectionId))
      {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, connection.JobWebServiceId);
        connection.RemoveConnectionId(Context.ConnectionId);

        if (connection.IsPulling == false)
        {
          Console.WriteLine($"No more active connections for {connection.JobWebServiceId}, cleaning up.");
          _iaPullingService.DeleteIaConnection(connection.JobWebServiceId);
        }
      }

      await base.OnDisconnectedAsync(exception);
    }
    catch (Exception ex)
    {
      Console.WriteLine($"Error in OnDisconnectedAsync: {ex.Message}");
      throw;
    }
  }
}
