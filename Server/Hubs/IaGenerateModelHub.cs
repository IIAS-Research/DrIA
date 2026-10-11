using Server.Services;
using Server.DAL.Db;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace Server.Hubs;

public class IaGenerateModelHub : ApplicationHub
{
  private readonly IaPullingModelService _iaPullingService;
  private readonly MainDbContext _dbContext;
  public IaGenerateModelHub(IaPullingModelService iaPullingService, MainDbContext dbContext)
  {
    _iaPullingService = iaPullingService;
    _dbContext = dbContext;
  }
  public virtual async Task JoinGroup(string jobWebServiceId)
  {
    try
    {
      if (!long.TryParse(Context.User?.Identity?.Name, out var userId) ||
          !await _dbContext.Processes.AnyAsync(process => process.IaRemoteId == jobWebServiceId && process.Project!.UserId == userId))
      {
        throw new HubException("Ce traitement est introuvable ou inaccessible.");
      }
      Console.WriteLine("Triggered JoinGroup");
      var connection = _iaPullingService.GetIaConnection(jobWebServiceId);
      connection ??= _iaPullingService.CreateIaConnection(jobWebServiceId);

      await Groups.AddToGroupAsync(Context.ConnectionId, jobWebServiceId);
      Console.WriteLine($"Connection {Context.ConnectionId} added to group {jobWebServiceId}");
      connection.AddConnectionId(Context.ConnectionId);

      if (!connection.IsPulling)
      {
        Console.WriteLine("Before await connection.StartPulling() when connection is not already pulling");
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
