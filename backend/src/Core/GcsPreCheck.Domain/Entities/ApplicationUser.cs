using GcsPreCheck.Domain.Common;
using GcsPreCheck.Domain.Enums;

namespace GcsPreCheck.Domain.Entities;

public class ApplicationUser : BaseEntity
{
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string? PasswordHash { get; set; }
    public string AuthProvider { get; set; } = "LocalIdentity";
    public UserRole Role { get; set; } = UserRole.Operator;
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public ICollection<CustomerTicket> CreatedTickets { get; set; } = new List<CustomerTicket>();
    public ICollection<PreCheckRun> ExecutedRuns { get; set; } = new List<PreCheckRun>();
    public ICollection<AuditLog> AuditLogs { get; set; } = new List<AuditLog>();
}
