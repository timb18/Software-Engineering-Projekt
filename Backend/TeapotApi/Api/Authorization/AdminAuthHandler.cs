using Microsoft.AspNetCore.Authorization;

namespace Api.Authorization;

/// <summary>
///     Authorization handler that evaluates the AdminAuthRequirement policy.
///     Checks if the authenticated user has the required "write:orgs" permission from Auth0.
/// </summary>
/// <remarks>
///     This handler is registered as a singleton and called during authorization evaluation
///     when an endpoint is protected by the AdminAuthPolicy. It examines JWT claims from Auth0
///     to determine if the user has admin permissions.
/// </remarks>
public class AdminAuthHandler : AuthorizationHandler<AdminAuthRequirement>
{
    /// <summary>
    ///     Handles the admin authorization requirement by checking user permissions from Auth0 JWT claims.
    /// </summary>
    /// <param name="context">The authorization handler context containing the user principal and requirements</param>
    /// <param name="requirement">The admin authorization requirement to evaluate</param>
    /// <remarks>
    ///     Fails authorization if:
    ///     - User is not authenticated or identity is null
    ///     Succeeds if the user has the "write:orgs" permission in their Auth0 token claims.
    ///     Permission claims are typically provided by Auth0 custom actions or rules.
    /// </remarks>
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