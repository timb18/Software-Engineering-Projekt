using Auth0.ManagementApi;

namespace Services.Users;

/// <summary>
/// Service for managing user operations within Auth0, implementing the IUserManagementService interface.
/// Provides functionality for user-related tasks such as password changes through the Auth0 Management API.
/// </summary>
/// <param name="managementClient">The Auth0 Management API client used for executing user operations.</param>
/// <param name="auth0Config">Configuration settings required to authenticate and access Auth0 management resources.</param>
public class UserManagementService(IManagementApiClient managementClient, Auth0Config auth0Config)
    : IUserManagementService
{
    /// <summary>
    /// Changes the password in Auth0 of a user
    /// </summary>
    /// <param name="changePasswordRequest">Contains e-mail and new password</param>
    /// <param name="cancellationToken"></param>
    /// <exception cref="ArgumentNullException">changePasswordRequest is null</exception>
    /// <exception cref="KeyNotFoundException">no user with the given e-mail could be found</exception>
    public async Task ChangePasswordAsync(ChangePasswordRequest changePasswordRequest,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(changePasswordRequest);
        ArgumentNullException.ThrowIfNull(changePasswordRequest.Email);
        ArgumentNullException.ThrowIfNull(changePasswordRequest.Password);
        
        var users = await managementClient.Users.ListAsync(new ListUsersRequestParameters(),
            cancellationToken: cancellationToken);

        var user = users.CurrentPage.Items.FirstOrDefault(u => u.Email == changePasswordRequest.Email);

        if (user?.UserId is null)
        {
            throw new KeyNotFoundException("User not found.");
        }

        var response = await managementClient.Users.UpdateAsync(user.UserId,
            new UpdateUserRequestContent
                { Password = changePasswordRequest.Password, Connection = auth0Config.ConnectionId },
            cancellationToken: cancellationToken);
    }
}