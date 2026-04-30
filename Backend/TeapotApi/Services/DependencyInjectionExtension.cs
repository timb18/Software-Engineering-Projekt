using DataAccess;
using Microsoft.Extensions.DependencyInjection;
using Services.Organizations;
using Services.Planning;
using Services.Users;
using Services.WorkProfiles;

namespace Services;

public static class DependencyInjectionExtension
{
    extension(IServiceCollection services)
    {
        public IServiceCollection AddTeapotServices() {
            services.AddScoped<IOrganizationAdminService, OrganizationAdminService>();
            services.AddScoped<IOrganizationService, OrganizationService>();
            services.AddScoped<IInvitationService, InvitationService>();
            services.AddScoped<IMembershipService, MembershipService>();
            services.AddScoped<IUserService, UserService>();
            services.AddScoped<IUserTaskService, UserTaskService>();
            services.AddScoped<IWorkProfileService, WorkProfileService>();

            return services;
        }
    }
}
