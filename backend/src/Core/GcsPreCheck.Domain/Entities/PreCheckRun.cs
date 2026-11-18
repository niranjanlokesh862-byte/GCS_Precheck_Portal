using GcsPreCheck.Domain.Common;
using GcsPreCheck.Domain.Enums;

namespace GcsPreCheck.Domain.Entities;

public class PreCheckRun : BaseEntity
{
    public Guid JobId { get; set; }
    public PreCheckJobQueue? Job { get; set; }

    public Guid TicketId { get; set; }
    public CustomerTicket? Ticket { get; set; }

    public Guid ExecutedByUserId { get; set; }
    public ApplicationUser? ExecutedByUser { get; set; }

    public string StudioUrl { get; set; } = string.Empty;
    public string ResolvedStudioIp { get; set; } = string.Empty;

    public Guid MatchedAllowlistId { get; set; }
    public StudioNetworkAllowlist? MatchedAllowlist { get; set; }

    public TargetEnvironment TargetEnvironment { get; set; } = TargetEnvironment.Production;
    public ExecutionStatus ExecutionStatus { get; set; } = ExecutionStatus.RUNNING;
    public OverallResult? OverallResult { get; set; }

    public int PassedCount { get; set; } = 0;
    public int FailedCount { get; set; } = 0;
    public int NotVerifiedCount { get; set; } = 0;
    public int NotApplicableCount { get; set; } = 0;
    public int NeedsReviewCount { get; set; } = 0;

    public string EvidenceStorageDirectory { get; set; } = string.Empty;
    public int? ExecutionDurationMs { get; set; }

    public DateTime StartedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAtUtc { get; set; }

    public ICollection<ParameterCheckResult> ParameterResults { get; set; } = new List<ParameterCheckResult>();
    public ICollection<PreCheckMfaChallenge> MfaChallenges { get; set; } = new List<PreCheckMfaChallenge>();
}
