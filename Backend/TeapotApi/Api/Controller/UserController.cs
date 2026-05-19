using Microsoft.AspNetCore.Mvc;
using Services.Users;

namespace Api.Controller;

[Route("api/user/{userId:guid}/profile")]
[ApiController]
public class UserController(IUserService userService) : ControllerBase
{
    /// <summary>
    /// Retrieves the public profile data for a user.
    /// The endpoint is used by the profile page and by other UI screens that need display information.
    /// </summary>
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
    /// Updates editable profile fields such as display name, email, avatar URL, and UI preferences.
    /// Returns the updated profile so the frontend can refresh its local state immediately.
    /// </summary>
    [HttpPut("")]
    [ProducesResponseType(typeof(UserProfileResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateProfile(
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
                    request.BlockerColor,
                    request.OrgColors),
                cancellationToken);

            return Ok(ToResponse(profile));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    private static UserProfileResponse ToResponse(UserProfileDto profile) => new(
        profile.Id,
        profile.Username,
        profile.DisplayName,
        profile.Email,
        profile.ProfileImageUrl,
        profile.Timezone,
        profile.BreakColor,
        profile.BlockerColor,
        profile.OrgColors);
}

public sealed record UpdateUserProfileRequest(
    string DisplayName,
    string Email,
    string? ProfileImageUrl,
    string? Timezone,
    string? BreakColor = null,
    string? BlockerColor = null,
    string? OrgColors = null);

public sealed record UserProfileResponse(
    Guid Id,
    string Username,
    string DisplayName,
    string Email,
    string? ProfileImageUrl,
    string Timezone,
    string? BreakColor = null,
    string? BlockerColor = null,
    string? OrgColors = null);
