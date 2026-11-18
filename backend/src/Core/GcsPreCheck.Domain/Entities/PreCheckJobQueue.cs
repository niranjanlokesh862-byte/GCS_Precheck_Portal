using GcsPreCheck.Domain.Common;
using GcsPreCheck.Domain.Enums;

namespace GcsPreCheck.Domain.Entities;

public class PreCheckJobQueue : BaseEntity
{
    public Guid TicketId { get; set; }
    public CustomerTicket? Ticket { get; set; }

    public string StudioUrl { get; set; } = string.Empty;
    public Guid SessionNonce { get; set; } = Guid.NewGuid();
    public ExecutionStatus ExecutionStatus { get; set; } = ExecutionStatus.QUEUED;
    public int Priority { get; set; } = 10;
    public DateTime QueuedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime ScheduledAtUtc { get; set; } = DateTime.UtcNow;

    public string? LeasedByWorkerId { get; set; }
    public DateTime? LeaseExpiresAtUtc { get; set; }
    public DateTime? HeartbeatAtUtc { get; set; }
    public DateTime? CompletedAtUtc { get; set; }
    public string? ErrorMessage { get; set; }

    public PreCheckRun? Run { get; set; }
    public ICollection<PreCheckMfaChallenge> MfaChallenges { get; set; } = new List<PreCheckMfaChallenge>();
}
