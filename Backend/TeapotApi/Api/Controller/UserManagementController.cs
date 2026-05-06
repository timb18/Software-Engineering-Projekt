using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controller;

/// <summary>
/// Provides endpoints for managing user operations through the API.
/// </summary>
/// <remarks>
/// This controller handles requests mapped to the "api/user/management" route.
/// It exposes functionality for updating user credentials specifically through password modification.
/// </remarks>
/// <param name="managementService">The interface for managing user data and operations.</param>
[Route("api/user/management")]
public class UserManagementController(IUserManagementService managementService) : ControllerBase
{
    
    /// <summary>
    /// Initiates a password change operation for a specific user.
    /// </summary>
    /// <param name="changePasswordRequest">The request containing the user's email and new password.</param>
    /// <param name="cancellationToken">Token to observe for request cancellation.</param>
    /// <returns>
    /// NoContent on successful password change.
    /// NotFound if the user specified in the request does not exist.
    /// InternalServerError if an unexpected error occurred during the password change operation.
    /// </returns>
    [HttpPatch("change-password")]
    public async Task<Results<NoContent, NotFound<string>, InternalServerError<string>>> ChangePassword(
        [FromBody] ChangePasswordRequest changePasswordRequest,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await managementService.ChangePasswordAsync(changePasswordRequest, cancellationToken);
            return TypedResults.NoContent();
        }
        catch (KeyNotFoundException e)
        {
            return TypedResults.NotFound("User not found");
        }
        catch (Exception e)
        {
            return TypedResults.InternalServerError("There was an issue with changing the password");
        }
    }
}