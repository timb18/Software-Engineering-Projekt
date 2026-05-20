using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controller;

/// <summary>
/// Provides endpoints for user-management operations that are separate from the regular profile API.
/// </summary>
/// <remarks>
/// This controller handles requests mapped to the "api/users/management" route.
/// It currently exposes password-change functionality for user credentials.
/// </remarks>
/// <param name="managementService">The interface for managing user data and operations.</param>
[Route("api/users/management")]
[ApiController]
[Authorize(AuthenticationSchemes = "Auth0")]
public class UserManagementController(IUserManagementService managementService) : ControllerBase
{
    
    /// <summary>
    /// Initiates a password change operation for a specific user.
    /// The service layer performs the actual Auth0 call and returns a domain-level exception when needed.
    /// </summary>
    /// <param name="changePasswordRequest">The request containing the user's email and new password.</param>
    /// <param name="cancellationToken">Token to observe for request cancellation.</param>
    /// <returns>
    /// NoContent on successful password change.
    /// NotFound if the user specified in the request does not exist.
    /// InternalServerError if an unexpected error occurs during the password change operation.
    /// </returns>
    [HttpPatch("change-password")]
    public async Task<Results<NoContent, BadRequest<string>, NotFound<string>, InternalServerError<string>>> ChangePassword(
        [FromBody] ChangePasswordRequest changePasswordRequest,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await managementService.ChangePasswordAsync(changePasswordRequest, cancellationToken);
            return TypedResults.NoContent();
        }
        catch (ArgumentNullException e)
        {
            return TypedResults.BadRequest(e.Message);
        }
        catch (KeyNotFoundException)
        {
            return TypedResults.NotFound("User not found");
        }
        catch (Exception)
        {
            return TypedResults.InternalServerError("There was an issue with changing the password");
        }
    }
}