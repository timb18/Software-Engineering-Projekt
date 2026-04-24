using Microsoft.AspNetCore.Mvc;
using Services;

namespace Api.Controller;

[Route("api/[controller]")]
[ApiController]
public class AuthController(IUserService userService) : ControllerBase
{
    /// <summary>
    /// Finds or creates a user by email and ensures they have a personal work profile.
    /// Call this once after Auth0 login. Returns userId and workProfileId for subsequent API calls.
    /// </summary>
    [HttpPost("ensure")]
    [ProducesResponseType(typeof(EnsureUserResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> EnsureUser(
        [FromBody] EnsureUserRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Email))
            return BadRequest("Email is required.");

        var (userId, workProfileId) = await userService.EnsureUserAsync(request.Email, cancellationToken);
        return Ok(new EnsureUserResponse(userId, workProfileId));
    }
}

public record EnsureUserRequest(string Email);
public record EnsureUserResponse(Guid UserId, Guid WorkProfileId);