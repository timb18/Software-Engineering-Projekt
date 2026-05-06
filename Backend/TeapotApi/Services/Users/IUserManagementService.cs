namespace Services.Users;

public interface IUserManagementService
{
    Task ChangePasswordAsync(ChangePasswordRequest changePasswordRequest, CancellationToken cancellationToken = default);
}

public record Auth0Config(
    string Domain,
    string Audience,
    string ClientId,
    string ClientSecret,
    string ConnectionId,
    string ManagementAudience);
    
public record ChangePasswordRequest(string Email, string Password);