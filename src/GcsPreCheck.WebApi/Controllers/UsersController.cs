using GcsPreCheck.Application.Common.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GcsPreCheck.WebApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class UsersController : ControllerBase
{
    private readonly IApplicationDbContext _context;

    public UsersController(IApplicationDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<IActionResult> GetActiveUsers(CancellationToken ct)
    {
        var users = await _context.ApplicationUsers
            .AsNoTracking()
            .Where(u => u.IsActive)
            .OrderBy(u => u.Role)
            .ThenBy(u => u.Username)
            .Select(u => new
            {
                u.Id,
                u.Username,
                u.FullName,
                Role = u.Role.ToString(),
                u.Email
            })
            .ToListAsync(ct);

        return Ok(users);
    }
}
