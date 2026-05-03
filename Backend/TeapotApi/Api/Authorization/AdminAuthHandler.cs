using Microsoft.AspNetCore.Authorization;

namespace Api.Authorization;

public class AdminAuthHandler : AuthorizationHandler<AdminAuthRequirement>
{
    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context,
        AdminAuthRequirement requirement)
    {
        if (context.User.Identity is not { IsAuthenticated: true })
        {
            context.Fail();
            return;
        }

        var userPermissions = context.User.FindAll("permissions").Select(permission => permission.Value);

        if (userPermissions.Contains(AdminAuthRequirement.RequiredPermission)) context.Succeed(requirement);
    }
}