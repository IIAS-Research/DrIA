using Server.Hubs;
using Server.Services;

namespace SignalRChat.Hubs
{
  public class IaMetricHub : ApplicationHub
  {
    public IaMetricHub(IaPullingService iaPullingService) : base(iaPullingService) {}
  }
}
