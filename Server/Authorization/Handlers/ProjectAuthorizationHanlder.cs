using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Infrastructure;
using Server.Models;

namespace Server.Authorization.Handlers;

public class ProjectAuthorizationCrudHandler :
    AuthorizationHandler<OperationAuthorizationRequirement, Project>
{
    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context,
                                                   OperationAuthorizationRequirement requirement,
                                                   Project resource)
    {
        if (Operations.Read.Name == requirement.Name)
        {
            context.Succeed(requirement);
        }
        if (Operations.ReadOnlyOwn.Name == requirement.Name && context.User.Identity?.Name == resource.User?.Id.ToString())
        {
            context.Succeed(requirement);
        }
        return Task.CompletedTask;
    }

}
