using DataAccess.Models;
using DataAccess.Repositories;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controller;

/// <summary>
/// Handles user authentication and registration operations for the API.
/// </summary>
/// <remarks>
/// This controller provides endpoints for ensuring users have work profiles after authentication,
/// and registering new users to the system.
/// </remarks>
[Route("api/[controller]")]
[ApiController]
public class AuthController : ControllerBase
{
    private readonly IUserRepository _userRepository;
    private readonly IUserService _userService;

    public AuthController(IUserService userService, IUserRepository userRepository)
    {
        _userService = userService;
        _userRepository = userRepository;
    }

    /// <summary>
    /// Finds or creates a user by email.
    /// Call this once after Auth0 login. Returns userId and the active workProfileId when one exists.
    /// </summary>
    [HttpPost("ensure")]
    [ProducesResponseType(typeof(EnsureUserResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> EnsureUser(
        [FromBody] EnsureUserRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Email))
            return BadRequest("Email is required.");

        var (userId, workProfileId) = await _userService.EnsureUserAsync(
            request.Email,
            request.AuthProviderSubject,
            cancellationToken);
        return Ok(new EnsureUserResponse(userId, workProfileId));
    }

    /// <summary>
    /// Registers a new user account using the provided email address.
    /// Checks for existing users with the same email and returns the existing account if found.
    /// Otherwise, creates a new user record in the repository and returns the new account details.
    /// </summary>
    /// <param name="request">The HTTP request body containing the user's email address.</param>
    /// <param name="cancellationToken">The cancellation token to observe the execution of the asynchronous operation.</param>
    /// <returns>A task representing the HTTP response containing the user's unique ID and email address on success.</returns>
    [HttpPost("register")]
    [ProducesResponseType<RegisterResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> RegisterAsync([FromBody] RegisterRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Email))
            return BadRequest("E-Mail ist erforderlich.");

        var normalizedEmail = request.Email.Trim().ToLowerInvariant();

        var existingUser = await _userRepository.FindByEmailAsync(normalizedEmail, cancellationToken);

        if (existingUser != null)
        {
            return Ok(new
            {
                success = true,
                created = false,
                data = new RegisterResponse
                {
                    Id = existingUser.Id,
                    Email = existingUser.Email,
                }
            });
        }

        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = normalizedEmail,
            CreatedAt = DateTime.UtcNow
        };

        await _userRepository.AddAsync(user, cancellationToken);

        return Ok(new RegisterResponse
            {
                Id = user.Id,
                Email = user.Email,
            }
        );
    }
}

public record EnsureUserRequest(
    string Email,
    string? AuthProviderSubject = null);

public record EnsureUserResponse(Guid UserId, Guid? WorkProfileId);

public record RegisterRequest
{
    public string Email { get; init; } = string.Empty;
}

public class RegisterResponse
{
    public Guid Id { get; set; }
    public string Email { get; set; } = string.Empty;
}