using DataAccess.Models;
using DataAccess.Repositories;
using Microsoft.AspNetCore.Mvc;
using Services.Users;

namespace Api.Controller;

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
    /// Finds or creates a user by email and ensures they have a personal work profile.
    /// Call this once after Auth0 login. Returns userId and workProfileId for subsequent API calls.
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
    /// Registers a new user by email or returns existing user if email already registered.
    /// Validates email presence and creates user in database if not found.
    /// </summary>
    /// <param name="request">Registration request containing the user's email address.</param>
    /// <param name="cancellationToken">Token to allow operation cancellation by caller.</param>
    /// <returns>Task representing registration result. Contains Ok result with user data including Id and Email.
    /// Success flag indicates operation completed. Created flag indicates whether new user was created or existing user was returned.</returns>
    [HttpPost("register")]
    public async Task<IActionResult> RegisterAsync([FromBody] RegisterRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Email))
            return BadRequest(new { success = false, message = "E-Mail ist erforderlich." });

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

        return Ok(new
        {
            success = true,
            created = true,
            data = new RegisterResponse
            {
                Id = user.Id,
                Email = user.Email,
            }
        });
    }
}

public record EnsureUserRequest(
    string Email,
    string? AuthProviderSubject = null);

public record EnsureUserResponse(Guid UserId, Guid WorkProfileId);

public record RegisterRequest
{
    public string Email { get; init; } = string.Empty;
}

public class RegisterResponse
{
    public Guid Id { get; set; }
    public string Email { get; set; } = string.Empty;
}