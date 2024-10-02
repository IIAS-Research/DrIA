using Microsoft.AspNetCore.SignalR;
using Server.Services;

namespace Server.Hubs
{
  public abstract class ApplicationHub(IAWebSocketService iAWebSocketService) : Hub
  {
    private readonly IAWebSocketService _iaWebSocketService = iAWebSocketService;

    public void TestWs()
    {
      Console.WriteLine("Triggered TestWs");
    }

    public async Task JoinGroup(string jobWebServiceId)
    {
      try
      {
        Console.WriteLine("Triggered JoinGroup");
        var connection = _iaWebSocketService.GetIaConnection(jobWebServiceId);
        connection ??= _iaWebSocketService.CreateIaConnection(jobWebServiceId, GetType());

        await Groups.AddToGroupAsync(Context.ConnectionId, jobWebServiceId);
        Console.WriteLine($"Connection {Context.ConnectionId} added to group {jobWebServiceId}");
        connection.AddConnectionId(Context.ConnectionId);

        if (!connection.IsConnected)
        {
          Console.WriteLine("Before await connection.ConnectAsync()");
          await connection.ConnectIaWebServiceAndSignalRWebSocketsAsync();
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
        foreach (var connection in _iaWebSocketService.SearchForConnectionIdInConnection(Context.ConnectionId))
        {
          await Groups.RemoveFromGroupAsync(Context.ConnectionId, connection.JobWebServiceId);
          connection.RemoveConnectionId(Context.ConnectionId);

          if (connection.GetConnectionIdsCount() == 0)
          {
            Console.WriteLine($"No more active connections for {connection.JobWebServiceId}, cleaning up.");
            await _iaWebSocketService.DeleteIaConnection(connection.JobWebServiceId);
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

    // // Handle reconnection scenarios
    // public override async Task OnConnectedAsync()
    // {
    //   Console.WriteLine($"Client connected: {Context.ConnectionId}");
    //   await base.OnConnectedAsync();
    // }
  }
}
