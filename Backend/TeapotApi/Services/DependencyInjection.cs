using DataAccess;
using Microsoft.Extensions.DependencyInjection;

namespace Services;

public static class DependencyInjection
{
    public static IServiceCollection AddTeapotServices(this IServiceCollection services)
    {
        services.AddSingleton<IOrganizationRepo, OrganizationRepo>();
        services.AddSingleton<IUserRepo, UserRepo>();
        services.AddSingleton<IMembershipRepo, MembershipRepo>();

        services.AddSingleton<IOrganizationAdminService, OrganizationAdminService>();
        services.AddSingleton<IUserService, UserService>();
        services.AddSingleton<IUserTaskService, UserTaskService>();

        return services;
    }
}
