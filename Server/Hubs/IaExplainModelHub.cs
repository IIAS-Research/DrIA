using Server.Services;

namespace Server.Hubs;

public class IaExplainModelHub : ApplicationHub
{
  private readonly IaPullingExplainService _iaPullingService;
  public IaExplainModelHub(IaPullingExplainService iaPullingService)
  {
    _iaPullingService = iaPullingService;
  }
  public virtual async Task JoinGroup(string jobWebServiceId, long localModelId)
  {
    try
    {
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
