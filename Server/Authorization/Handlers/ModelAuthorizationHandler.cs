using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Infrastructure;
using Server.Models;
using Server.Services;

namespace Server.Authorization.Handlers;

public class ModelAuthorizationCrudHandler :
    AuthorizationHandler<OperationAuthorizationRequirement, Model>
{
  private readonly IServiceScopeFactory _scopeFactory;
  public ModelAuthorizationCrudHandler(IServiceScopeFactory scopeFactory)
  {
    _scopeFactory = scopeFactory;
  }

  async protected override Task HandleRequirementAsync(AuthorizationHandlerContext context,
                                                 OperationAuthorizationRequirement requirement,
                                                 Model resource)
  {
    using (var scope = _scopeFactory.CreateScope())
    {
      var modelService = scope.ServiceProvider.GetRequiredService<ModelService>();
      if (Operations.Read.Name == requirement.Name)
      {
        var userId = context.User.Identity?.Name;
        if (userId == null)
        {
          context.Fail();
          return;
        }
        if (!long.TryParse(userId, out long parsedUserId))
        {
          context.Fail();
          return;
        }
        var responseCanAccess = await modelService.UserCanAccessModel(resource, parsedUserId);
        if (!responseCanAccess)
        {
          context.Fail();
          return;
        }
        context.Succeed(requirement);
        return;
      }
      context.Fail();
    }
  }


}
