using DataAccess.Models;
using DataAccess.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controller;

[Route("api/[controller]")]
[ApiController]
[Authorize(AuthenticationSchemes = "Auth0")]
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
    ///     Finds an existing user by email or creates a new one when none exists.
    ///     Call this once after Auth0 login so the frontend can get the internal user ID
    ///     and, when available, the active work profile ID in a single round-trip.
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
            request.DisplayName,
            request.ProfileImageUrl,
            cancellationToken);
        return Ok(new EnsureUserResponse(userId, workProfileId));
    }

    /// <summary>
    ///     Registers a user if the email is new, or returns the existing user if it already exists.
    ///     This keeps the operation idempotent for clients that may retry the request.
    /// </summary>
    [HttpPost("register")]
    public async Task<IActionResult> RegisterAsync([FromBody] RegisterRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Email))
            return BadRequest(new { success = false, message = "E-Mail ist erforderlich." });

        var normalizedEmail = request.Email.Trim().ToLowerInvariant();

        var existingUser = await _userRepository.FindByEmailAsync(normalizedEmail, cancellationToken);

        if (existingUser != null)
            return Ok(new
            {
                success = true,
                created = false,
                data = new RegisterResponse
                {
                    Id = existingUser.Id,
                    Email = existingUser.Email,
                    Username = existingUser.Username ?? normalizedEmail.Split('@')[0]
                }
            });

        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = normalizedEmail,
            Username = string.IsNullOrWhiteSpace(request.Username)
                ? normalizedEmail.Split('@')[0]
                : request.Username.Trim(),
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
                Username = user.Username ?? normalizedEmail.Split('@')[0]
            }
        });
    }
}

public record EnsureUserRequest(
    string Email,
    string? AuthProviderSubject = null,
    string? DisplayName = null,
    string? ProfileImageUrl = null);

public record EnsureUserResponse(Guid UserId, Guid? WorkProfileId);

public class RegisterRequest
{
    public string Email { get; set; } = string.Empty;
    public string? Username { get; set; }
}

public class RegisterResponse
{
    public Guid Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
}