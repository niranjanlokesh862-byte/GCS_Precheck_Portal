using GcsPreCheck.Domain.Common;
using GcsPreCheck.Domain.Enums;

namespace GcsPreCheck.Domain.Entities;

public class StudioNetworkAllowlist : BaseEntity
{
    public AllowlistEntryType EntryType { get; set; } = AllowlistEntryType.ExactIp;
    public string NetworkPattern { get; set; } = string.Empty;
    public string EnvironmentDescription { get; set; } = string.Empty;
    public bool IsProduction { get; set; } = true;

    public Guid ApprovedByUserId { get; set; }
    public ApplicationUser? ApprovedByUser { get; set; }
    public DateTime ApprovedAtUtc { get; set; } = DateTime.UtcNow;
    public bool IsActive { get; set; } = true;

    public ICollection<PreCheckRun> PreCheckRuns { get; set; } = new List<PreCheckRun>();
}
