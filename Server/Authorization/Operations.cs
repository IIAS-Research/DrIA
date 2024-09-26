using Microsoft.AspNetCore.Authorization.Infrastructure;

namespace Server.Authorization;

public static class Operations
{
    public readonly static OperationAuthorizationRequirement Create =
        new OperationAuthorizationRequirement { Name = nameof(Create) };
    public readonly static OperationAuthorizationRequirement Read =
        new OperationAuthorizationRequirement { Name = nameof(Read) };
    public readonly static OperationAuthorizationRequirement Update =
        new OperationAuthorizationRequirement { Name = nameof(Update) };
    public readonly static OperationAuthorizationRequirement Delete =
        new OperationAuthorizationRequirement { Name = nameof(Delete) };
    public readonly static OperationAuthorizationRequirement ReadOnlyOwn =
        new OperationAuthorizationRequirement { Name = nameof(ReadOnlyOwn) };
}
