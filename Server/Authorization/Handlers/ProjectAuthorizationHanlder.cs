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
        if (Operations.Admin.Name == requirement.Name)
        { 
            var isAdminClaim = context.User.FindFirst(c => c.Type == "isAdmin");
            if (isAdminClaim != null && bool.TryParse(isAdminClaim.Value, out bool isAdmin))
            {
                context.Succeed(requirement);
            }
        }

        if (Operations.Read.Name == requirement.Name)
        {
            context.Succeed(requirement);
        }

        if (Operations.ReadOnlyOwn.Name == requirement.Name && context.User.Identity?.Name == resource.UserId.ToString())
        {
            context.Succeed(requirement);
        }
        return Task.CompletedTask;
    }

}
