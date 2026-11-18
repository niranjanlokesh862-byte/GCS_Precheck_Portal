using GcsPreCheck.Domain.Common;

namespace GcsPreCheck.Domain.Entities;

public class PreCheckMfaChallenge : BaseEntity
{
    public Guid JobId { get; set; }
    public PreCheckJobQueue? Job { get; set; }

    public Guid RunId { get; set; }
    public PreCheckRun? Run { get; set; }

    public string ChallengeType { get; set; } = "TOTP_6_DIGIT";
    public int SubmissionAttempts { get; set; } = 0;
    public bool IsConsumed { get; set; } = false;
    public DateTime ExpiresAtUtc { get; set; } = DateTime.UtcNow.AddSeconds(180);
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
