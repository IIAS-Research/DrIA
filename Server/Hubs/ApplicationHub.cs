using Microsoft.AspNetCore.SignalR;
using Server.Services;

namespace Server.Hubs
{
  public abstract class ApplicationHub(IaPullingService iaPullingService) : Hub
  {

    private readonly IaPullingService _iaPullingService = iaPullingService;

    public void TestWs()
    {
      Console.WriteLine("Triggered TestWs");
    }

    public async Task JoinGroup(string jobWebServiceId)
    {
      try
      {
        Console.WriteLine("Triggered JoinGroup");
        var connection = _iaPullingService.GetIaConnection(jobWebServiceId);
        connection ??= _iaPullingService.CreateIaConnection(jobWebServiceId, GetType());

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
}
