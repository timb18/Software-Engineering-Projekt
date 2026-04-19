using DataAccess;
using Microsoft.Extensions.DependencyInjection;

namespace Services;

public static class DependencyInjectionExtension
{
    extension(IServiceCollection services)
    {
        public IServiceCollection AddTeapotServices() {
            services.AddSingleton<IOrganizationRepo, FakeOrganizationRepo>();
            services.AddSingleton<IUserRepo, FakeUserRepo>();
            services.AddSingleton<IMembershipRepo, FakeMembershipRepo>();

            services.AddSingleton<IOrganizationAdminService, OrganizationAdminService>();
            services.AddSingleton<IUserService, UserService>();
            services.AddSingleton<IUserTaskService, UserTaskService>();

            return services;
        }
    }
}
