using Server.Services;

namespace Server.Hubs;

public class IaStatusHub : ApplicationHub
{
  public IaStatusHub(IAWebSocketServiceFuck iAWebSocketServiceFuck) : base(iAWebSocketServiceFuck) { }
}
