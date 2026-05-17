using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controller;

/// <summary>
/// Handles HTTP requests for user profile management operations.
/// </summary>
/// <param name="userService">The user service used to perform operations on user data.</param>
[Route("api/user/{userId:guid}/profile")]
[ApiController]
public class UserController(IUserService userService) : ControllerBase
{
    /// <summary>
    /// Retrieves the profile information for a specified user.
    /// </summary>
    /// <param name="userId">The unique identifier of the user whose profile is to be retrieved.</param>
    /// <param name="cancellationToken">Token to observe for request cancellation.</param>
    /// <returns>
    /// OkObjectResult containing the user profile data if the user exists and profile is successfully retrieved.
    /// NotFound if the specified user does not exist.
    /// BadRequest if an unexpected error occurred during profile retrieval.
    /// </returns>
    [HttpGet("")]
    [ProducesResponseType(typeof(UserProfileResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetProfile(Guid userId, CancellationToken cancellationToken)
    {
        try
        {
            var profile = await userService.GetProfileAsync(userId, cancellationToken);
            return Ok(ToResponse(profile));
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    /// <summary>
    /// Updates the profile information for a specified user.
    /// </summary>
    /// <param name="userId">The unique identifier of the user whose profile is to be updated.</param>
    /// <param name="request">The request containing the new profile details including display name, email, image URL, and timezone.</param>
    /// <param name="cancellationToken">Token to observe for request cancellation.</param>
    /// <returns>
    /// OkObjectResult containing the updated user profile data if the request is valid and the user is successfully updated.
    /// BadRequest if the request contains invalid data, such as an invalid email address.
    /// NotFound if the specified user does not exist.
    /// </returns>
    [HttpPut("")]
    public async
        Task<Results<Ok<UserProfileResponse>, BadRequest<string>, NotFound<string>, InternalServerError<string>>>
        UpdateProfile(
            Guid userId,
            [FromBody] UpdateUserProfileRequest request,
            CancellationToken cancellationToken)
    {
        try
        {
            var profile = await userService.UpdateProfileAsync(
                userId,
                new UpdateUserProfileCommand(
                    request.DisplayName,
                    request.Email,
                    request.ProfileImageUrl,
                    request.Timezone,
                    request.BreakColor,
                    request.OrgColors),
                cancellationToken);


            return TypedResults.Ok(ToResponse(profile));
        }
        catch (ArgumentException ex)
        {
            return TypedResults.BadRequest(ex.Message);
        }
        catch (KeyNotFoundException)
        {
            return TypedResults.NotFound("couldn't find User");
        }
        catch (Exception e)
        {
            return TypedResults.InternalServerError(e.Message);
        }
    }

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
    [HttpPatch("password")]
    public async Task<Results<NoContent, BadRequest<string>, NotFound<string>, InternalServerError<string>>>
        ChangePassword(
            [FromBody] ChangePasswordRequest changePasswordRequest,
            CancellationToken cancellationToken = default)
    {
        try
        {
            await userService.ChangePasswordAsync(changePasswordRequest, cancellationToken);
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

    private static UserProfileResponse ToResponse(UserProfileDto profile) => new(
        profile.Id,
        profile.Email,
        profile.Timezone,
        profile.BreakColor,
        profile.OrgColors);
}

public sealed record UpdateUserProfileRequest(
    string? DisplayName,
    string? Email,
    string? ProfileImageUrl,
    string? Timezone,
    string? BreakColor = null,
    string? OrgColors = null);

public sealed record UserProfileResponse(
    Guid Id,
    string Email,
    string Timezone,
    string? BreakColor = null,
    string? OrgColors = null);