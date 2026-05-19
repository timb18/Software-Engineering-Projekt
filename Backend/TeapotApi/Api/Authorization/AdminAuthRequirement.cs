using Microsoft.AspNetCore.Authorization;

namespace Api.Authorization;

/// <summary>
/// Defines the authorization requirement for administrative operations on organizations.
/// This requirement enforces that only users with the "write:orgs" permission can access admin endpoints.
/// </summary>
/// <remarks>
/// Used in endpoints that require organization-level administration privileges.
/// The AdminAuthHandler validates this requirement by checking Auth0 JWT claims.
/// </remarks>
public class AdminAuthRequirement : IAuthorizationRequirement
{
    /// <summary>
    /// The policy name for admin authorization. Used in [Authorize] attributes on admin endpoints.
    /// Example: [Authorize(Policy = AdminAuthRequirement.PolicyName)]
    /// </summary>
    public const string PolicyName = "AdminAuthPolicy";
    
    /// <summary>
    /// The required Auth0 permission string for admin access to organization operations.
    /// This permission must be present in the user's JWT token claims.
    /// </summary>
    public const string RequiredPermission = "write:orgs";
}