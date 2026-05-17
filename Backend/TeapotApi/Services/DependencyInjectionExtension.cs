using DataAccess;
using DataAccess.Repositories;
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
        /// <summary>
        /// Configures and registers all teapot application services with the dependency injection container.
        /// </summary>
        /// <param name="services">The service collection to which services will be added.</param>
        /// <returns>The modified service collection for method chaining.</returns>
        public IServiceCollection AddTeapotServices()
        {
            services.AddScoped<IUnitOfWork, UnitOfWork>();
            services.AddHttpClient<ResendEmailSender>();
            services.AddScoped<SmtpEmailSender>();
            services.AddScoped<IEmailSender, ConfiguredEmailSender>();
            services.AddScoped<ITaskDependencyRepository, TaskDependencyRepository>();

            services.AddScoped<IOrganizationAdminService, OrganizationAdminService>();
            services.AddScoped<IOrganizationService, OrganizationService>();
            services.AddScoped<IInvitationService, InvitationService>();
            services.AddScoped<IMembershipService, MembershipService>();
            services.AddScoped<IUserService, UserService>();
            services.AddScoped<IUserTaskService, UserTaskService>();
            services.AddScoped<IWorkProfileService, WorkProfileService>();

            // Planning
            services.AddScoped<DependencyAnalyzer>();
            services.AddScoped<SchedulingAlgorithm>();
            services.AddScoped<IUserTaskPlanner, UserTaskPlanner>();

            return services;
        }
    }
}
