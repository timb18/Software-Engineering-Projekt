using DataAccess;
using DataAccess.Repositories;
using Microsoft.Extensions.DependencyInjection;
using Services.Organizations;
using Services.Planning;
using Services.Users;
using Services.WorkProfiles;

namespace Services;

/// <summary>
/// Registers the service-layer dependencies used by the API.
/// </summary>
public static class DependencyInjectionExtension
{
    extension(IServiceCollection services)
    {
        /// <summary>
        /// Adds all Teapot service registrations to the dependency injection container.
        /// </summary>
        public IServiceCollection AddTeapotServices() {

            // Core infrastructure services.
            services.AddScoped<IUnitOfWork, UnitOfWork>();
            services.AddHttpClient<ResendEmailSender>();
            services.AddScoped<SmtpEmailSender>();
            services.AddScoped<IEmailSender, ConfiguredEmailSender>();
            services.AddScoped<ITaskDependencyRepository, TaskDependencyRepository>();
            services.AddScoped<IRecurringBlockerRepository, RecurringBlockerRepository>();

            // Domain services for user, organization, invitation, and membership workflows.
            services.AddScoped<IUserManagementService, UserManagementService>();
            services.AddScoped<IOrganizationAdminService, OrganizationAdminService>();
            services.AddScoped<IOrganizationService, OrganizationService>();
            services.AddScoped<IInvitationService, InvitationService>();
            services.AddScoped<IMembershipService, MembershipService>();
            services.AddScoped<IUserService, UserService>();
            services.AddScoped<IUserTaskService, UserTaskService>();
            services.AddScoped<IWorkProfileService, WorkProfileService>();

            // Planning pipeline components.
            services.AddScoped<DependencyAnalyzer>();
            services.AddScoped<SchedulingAlgorithm>();
            services.AddScoped<IUserTaskPlanner, UserTaskPlanner>();

            return services;
        }
    }
}
