using Microsoft.AspNetCore.Mvc;
using Services.Users;

namespace Api.Controller;

[Route("api/user/{userId:guid}/profile")]
[ApiController]
public class UserController(IUserService userService) : ControllerBase
{
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
                    request.Timezone),
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
        profile.Timezone);
}

public sealed record UpdateUserProfileRequest(
    string DisplayName,
    string Email,
    string? ProfileImageUrl,
    string? Timezone);

public sealed record UserProfileResponse(
    Guid Id,
    string Username,
    string DisplayName,
    string Email,
    string? ProfileImageUrl,
    string Timezone);
