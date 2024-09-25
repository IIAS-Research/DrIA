using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Infrastructure;
using Server.Models;

namespace Server.Authorization.Handlers;

public class ProjectAuthorizationCrudHandler :
    AuthorizationHandler<OperationAuthorizationRequirement, Project>
{
    protected async override Task HandleRequirementAsync(AuthorizationHandlerContext context,
                                                   OperationAuthorizationRequirement requirement,
                                                   Project resource)
    {
        if (requirement.Name == Operations.Create.Name)
        {
            
        }
        return;
    }

}
