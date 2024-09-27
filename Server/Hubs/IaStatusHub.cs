using Server.Services;

namespace SignalRChat.Hubs
{
  public class IaStatusHub : ApplicationHub
  {
    public IaStatusHub(IAWebSocketService iAWebSocketService) : base (iAWebSocketService) {}
  }
}
