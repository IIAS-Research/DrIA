using Server.Services;

namespace Server.Hubs;

public class IaStatusHub : ApplicationHub
{
  public IaStatusHub(IAWebSocketService iAWebSocketService) : base(iAWebSocketService) { }
}
