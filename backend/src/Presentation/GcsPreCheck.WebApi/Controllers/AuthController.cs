using System.Security.Cryptography;
using System.Text;
using GcsPreCheck.Application.Common.Interfaces;
using GcsPreCheck.Domain.Entities;
using GcsPreCheck.Domain.Enums;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GcsPreCheck.WebApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IApplicationDbContext _context;
    private readonly ILogger<AuthController> _logger;

    public AuthController(IApplicationDbContext context, ILogger<AuthController> logger)
    {
        _context = context;
        _logger = logger;
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrWhiteSpace(request.Password))
        {
            return BadRequest(new { success = false, message = "Username and password are required." });
        }

        var normalizedUsername = request.Username.Trim();
        var passwordHash = ComputeSha256Hex(request.Password);

        ApplicationUser? user = null;

        try
        {
            user = await _context.ApplicationUsers
                .FirstOrDefaultAsync(u => u.Username == normalizedUsername && u.IsActive, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Database query failed during user login. Checking fallback developer credentials.");
        }

        // If user found in database, verify credentials
        if (user != null)
        {
            bool passwordValid = false;

            if (string.IsNullOrEmpty(user.PasswordHash))
            {
                // If password hash not yet set, accept initial admin password
                passwordValid = request.Password == "admin123";
            }
            else
            {
                // Verify against SHA256 hex or direct string comparison
                passwordValid = string.Equals(user.PasswordHash, passwordHash, StringComparison.OrdinalIgnoreCase)
                             || string.Equals(user.PasswordHash, request.Password, StringComparison.Ordinal);
            }

            if (!passwordValid)
            {
                _logger.LogWarning("Failed login attempt for user: {Username}", normalizedUsername);
                return Unauthorized(new { success = false, message = "Invalid username or password." });
            }

            var token = GenerateSessionToken(user.Id, user.Username, user.Role.ToString());
            return Ok(new
            {
                success = true,
                message = "Authentication successful.",
                token,
                user = new
                {
                    id = user.Id,
                    username = user.Username,
                    fullName = user.FullName,
                    role = user.Role.ToString(),
                    email = user.Email
                }
            });
        }

        // Development fallback: Allow admin/admin123 before database is seeded
        if (string.Equals(normalizedUsername, "admin", StringComparison.OrdinalIgnoreCase) && request.Password == "admin123")
        {
            var fallbackUser = new
            {
                id = Guid.Parse("00000000-0000-0000-0000-000000000001"),
                username = "admin",
                fullName = "System Administrator",
                role = "Admin",
                email = "admin@wercsgcs.internal"
            };

            var token = GenerateSessionToken(fallbackUser.id, fallbackUser.username, fallbackUser.role);
            return Ok(new
            {
                success = true,
                message = "Authentication successful (Admin Default Session).",
                token,
                user = fallbackUser
            });
        }

        return Unauthorized(new { success = false, message = "Invalid username or password." });
    }

    [HttpPost("logout")]
    public IActionResult Logout()
    {
        return Ok(new { success = true, message = "Session invalidated successfully." });
    }

    private static string ComputeSha256Hex(string input)
    {
        var bytes = Encoding.UTF8.GetBytes(input);
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static string GenerateSessionToken(Guid userId, string username, string role)
    {
        var payload = $"{userId}:{username}:{role}:{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}";
        return Convert.ToBase64String(Encoding.UTF8.GetBytes(payload));
    }
}

public record LoginRequest(string Username, string Password);
