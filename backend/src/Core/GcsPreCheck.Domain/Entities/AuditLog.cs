using GcsPreCheck.Domain.Common;

namespace GcsPreCheck.Domain.Entities;

public class AuditLog : BaseEntity
{
    public Guid? UserId { get; set; }
    public ApplicationUser? User { get; set; }

    public string Action { get; set; } = string.Empty;
    public string EntityName { get; set; } = string.Empty;
    public string EntityId { get; set; } = string.Empty;
    public string? AuditPayloadJson { get; set; }
    public string IpAddress { get; set; } = "127.0.0.1";
    public DateTime TimestampUtc { get; set; } = DateTime.UtcNow;
}
