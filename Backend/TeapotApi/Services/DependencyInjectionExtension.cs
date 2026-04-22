using DataAccess;
using Microsoft.Extensions.DependencyInjection;

namespace Services;

public static class DependencyInjectionExtension
{
    extension(IServiceCollection services)
    {
        public IServiceCollection AddTeapotServices() {
            services.AddScoped<IOrganizationAdminService, OrganizationAdminService>();
            services.AddScoped<IUserService, UserService>();
            services.AddScoped<IUserTaskService, UserTaskService>();
            
            return services;
        }
    }
}
