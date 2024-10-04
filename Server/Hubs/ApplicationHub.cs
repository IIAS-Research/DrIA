using Microsoft.AspNetCore.SignalR;
using Server.Services;

namespace Server.Hubs
{
  public abstract class ApplicationHub(IAWebSocketServiceFuck iAWebSocketServiceFuck) : Hub
  {
    // private readonly IAWebSocketServiceFuck _iaWebSocketServiceFuck = iAWebSocketServiceFuck;

    // public void TestWs()
    // {
    //   Console.WriteLine("Triggered TestWs");
    // }

    // public async Task JoinGroup(string jobWebServiceId)
    // {
    //   try
    //   {
    //     Console.WriteLine("Triggered JoinGroup");
    //     var connection = _iaWebSocketServiceFuck.GetIaConnection(jobWebServiceId);
    //     connection ??= _iaWebSocketServiceFuck.CreateIaConnection(jobWebServiceId, GetType());

    //     await Groups.AddToGroupAsync(Context.ConnectionId, jobWebServiceId);
    //     Console.WriteLine($"Connection {Context.ConnectionId} added to group {jobWebServiceId}");
    //     connection.AddConnectionId(Context.ConnectionId);

    //     if (!connection.IsConnected)
    //     {
    //       Console.WriteLine("Before await connection.ConnectAsync()");
    //       await connection.ConnectIaWebServiceAndSignalRWebSocketsAsync();
    //     }
    //   }
    //   catch (Exception ex)
    //   {
    //     Console.WriteLine($"Error in JoinGroup: {ex.Message}");
    //     throw;
    //   }
    // }

    // public override async Task OnDisconnectedAsync(Exception? exception)
    // {
    //   try
    //   {
    //     foreach (var connection in _iaWebSocketServiceFuck.SearchForConnectionIdInConnection(Context.ConnectionId))
    //     {
    //       await Groups.RemoveFromGroupAsync(Context.ConnectionId, connection.JobWebServiceId);
    //       connection.RemoveConnectionId(Context.ConnectionId);

    //       if (connection.GetConnectionIdsCount() == 0)
    //       {
    //         Console.WriteLine($"No more active connections for {connection.JobWebServiceId}, cleaning up.");
    //         await _iaWebSocketServiceFuck.DeleteIaConnection(connection.JobWebServiceId);
    //       }
    //     }

    //     await base.OnDisconnectedAsync(exception);
    //   }
    //   catch (Exception ex)
    //   {
    //     Console.WriteLine($"Error in OnDisconnectedAsync: {ex.Message}");
    //     throw;
    //   }
    // }

    private readonly IAWebSocketServiceFuck _iaWebSocketServiceFuck = iAWebSocketServiceFuck;

    public void TestWs()
    {
      Console.WriteLine("Triggered TestWs");
    }

    public async Task JoinGroup(string jobWebServiceId)
    {
      try
      {
        Console.WriteLine("Triggered JoinGroup");
        var connection = _iaWebSocketServiceFuck.GetIaConnection(jobWebServiceId);
        connection ??= _iaWebSocketServiceFuck.CreateIaConnection(jobWebServiceId, GetType());

        await Groups.AddToGroupAsync(Context.ConnectionId, jobWebServiceId);
        Console.WriteLine($"Connection {Context.ConnectionId} added to group {jobWebServiceId}");
        connection.AddConnectionId(Context.ConnectionId);

        if (!connection.IsPulling)
        {
          Console.WriteLine("Before await connection.ConnectAsync()");
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
        foreach (var connection in _iaWebSocketServiceFuck.SearchForConnectionIdInConnection(Context.ConnectionId))
        {
          await Groups.RemoveFromGroupAsync(Context.ConnectionId, connection.JobWebServiceId);
          connection.RemoveConnectionId(Context.ConnectionId);

          if (connection.IsPulling == false)
          {
            Console.WriteLine($"No more active connections for {connection.JobWebServiceId}, cleaning up.");
            _iaWebSocketServiceFuck.DeleteIaConnection(connection.JobWebServiceId);
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
