using Microsoft.AspNetCore.Authorization;

namespace Api.Authorization;

public class AdminAuthRequirement : IAuthorizationRequirement
{
    public const string PolicyName = "AdminAuthPolicy";
    public const string RequiredPermission = "write:orgs";
}