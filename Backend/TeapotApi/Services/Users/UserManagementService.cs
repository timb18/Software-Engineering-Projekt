using Auth0.ManagementApi;

namespace Services.Users;

/// <summary>
///     Auth0-backed implementation of user management operations.
/// </summary>
public class UserManagementService(IManagementApiClient managementClient, Auth0Config auth0Config)
    : IUserManagementService
{
    /// <summary>
    ///     Changes the password of a user in Auth0.
    /// </summary>
    /// <param name="changePasswordRequest">Contains the email address and new password.</param>
    /// <param name="cancellationToken">Cancellation token used for the Auth0 API request.</param>
    /// <exception cref="ArgumentNullException">Thrown when the request or one of its required values is null.</exception>
    /// <exception cref="KeyNotFoundException">Thrown when no Auth0 user with the given email address can be found.</exception>
    public async Task ChangePasswordAsync(ChangePasswordRequest changePasswordRequest,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(changePasswordRequest);
        ArgumentNullException.ThrowIfNull(changePasswordRequest.Email);
        ArgumentNullException.ThrowIfNull(changePasswordRequest.Password);

        // Auth0 password updates require the user identifier, so the account must be looked up first.
        var users = await managementClient.Users.ListAsync(new ListUsersRequestParameters(),
            cancellationToken: cancellationToken);

        var user = users.CurrentPage.Items.FirstOrDefault(u => u.Email == changePasswordRequest.Email);

        if (user?.UserId is null) throw new KeyNotFoundException("User not found.");

        await managementClient.Users.UpdateAsync(user.UserId,
            new UpdateUserRequestContent
                { Password = changePasswordRequest.Password, Connection = auth0Config.ConnectionId },
            cancellationToken: cancellationToken);
    }
}