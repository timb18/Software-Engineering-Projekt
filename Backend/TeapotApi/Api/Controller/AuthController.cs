using DataAccess.Models;
using DataAccess.Repositories;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controller;

[Route("api/[controller]")]
[ApiController]
public class AuthController : ControllerBase
{
    private readonly IGenericRepository<User> _userRepository;

    public AuthController(IGenericRepository<User> userRepository)
    {
        _userRepository = userRepository;
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
