using DataAccess.Models;
using DataAccess.Repositories;
using Microsoft.AspNetCore.Mvc;
using Services;

namespace Api.Controller;

[Route("api/[controller]")]
[ApiController]
public class AuthController : ControllerBase
{
    private readonly IGenericRepository<User> _userRepository;
    private readonly IUserService _userService;

    public AuthController(IUserService userService, IGenericRepository<User> userRepository)
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

        var (userId, workProfileId) = await _userService.EnsureUserAsync(request.Email, cancellationToken);
        return Ok(new EnsureUserResponse(userId, workProfileId));
    }

    [HttpPost("register")]
    public async Task<IActionResult> RegisterAsync([FromBody] RegisterRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Email))
            return BadRequest(new { success = false, message = "E-Mail ist erforderlich." });

        var normalizedEmail = request.Email.Trim().ToLowerInvariant();

        var existingUser = _userRepository.GetQueryable()
            .FirstOrDefault(u => u.Email.ToLower() == normalizedEmail);

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
                    Username = existingUser.Username ?? normalizedEmail.Split('@')[0]
                }
            });
        }

        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = normalizedEmail,
            Username = string.IsNullOrWhiteSpace(request.Username) ? normalizedEmail.Split('@')[0] : request.Username.Trim(),
            CreatedAt = DateTime.UtcNow
        };

        await _userRepository.AddAsync(user);

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

public record EnsureUserRequest(string Email);
public record EnsureUserResponse(Guid UserId, Guid WorkProfileId);

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
