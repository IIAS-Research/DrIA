using Server.Hubs;
using Server.Services;

namespace SignalRChat.Hubs
{
  public class IaMetricHub : ApplicationHub
  {
    public IaMetricHub(IAWebSocketServiceFuck IAWebSocketServiceFuck) : base(IAWebSocketServiceFuck) {}
  }
}
