using Microsoft.AspNetCore.SignalR;
using Server.Services;

namespace SignalRChat.Hubs
{
  public abstract class ApplicationHub : Hub
  {
    private readonly IAWebSocketService _iaWebSocketService;

    public ApplicationHub(IAWebSocketService iAWebSocketService)
    {
      _iaWebSocketService = iAWebSocketService;
    }

    public async Task JoinGroup(string uuid)
    {
      var connection = _iaWebSocketService.GetIaConnection(uuid);
      if (connection == null)
      {
        connection = _iaWebSocketService.CreateIaConnection(uuid, GetType());
      }
      await Groups.AddToGroupAsync(Context.ConnectionId, uuid);
      connection.AddConnectionId(Context.ConnectionId);
      if (!connection.IsConnected)
      {
        await connection.ConnectAsync();
      }
    }

    public async Task LeaveGroup(string uuid)
    {
      var connection = _iaWebSocketService.GetIaConnection(uuid);
      if (connection == null)
      {
        return;
      }
      connection.RemoveConnectionId(Context.ConnectionId);
      if (connection.GetConnectionIdsCount() > 0)
      {
        await _iaWebSocketService.DeleteIaConnection(uuid);
      }
    }

    public async override Task OnDisconnectedAsync(Exception? exception)
    {
      var disconnectConnectionId = Context.ConnectionId;
      var connections = _iaWebSocketService.SearchForConnectionIdInConnection(disconnectConnectionId);
      connections.ForEach(async (connection) => {
        connection.RemoveConnectionId(disconnectConnectionId);
        if (connection.GetConnectionIdsCount() > 0)
        {
          await _iaWebSocketService.DeleteIaConnection(connection.Uuid);
        }
      });
    }
  }
}
