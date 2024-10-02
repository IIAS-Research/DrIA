using Server.Hubs;
using Server.Services;

namespace SignalRChat.Hubs
{
  public class IaMetricHub : ApplicationHub
  {
    public IaMetricHub(IAWebSocketService iAWebSocketService) : base(iAWebSocketService) {}
  }
}
