using GcsPreCheck.Domain.Common;
using GcsPreCheck.Domain.Enums;

namespace GcsPreCheck.Domain.Entities;

public class ParameterCheckResult : BaseEntity
{
    public Guid RunId { get; set; }
    public PreCheckRun? Run { get; set; }

    public string ParameterCode { get; set; } = string.Empty;
    public string ParameterName { get; set; } = string.Empty;
    public string TicketExpectedValue { get; set; } = string.Empty;
    public string? StudioActualValue { get; set; }
    public ComparisonStatus ComparisonStatus { get; set; } = ComparisonStatus.NOT_VERIFIED;
    
    public bool IsConditional { get; set; } = false;
    public string ScopeRuleApplied { get; set; } = string.Empty;

    public Guid? SopId { get; set; }
    public SopConfiguration? Sop { get; set; }

    public string? EvidenceScreenshotPath { get; set; }
    public string? DomSnapshotHash { get; set; }
    public string? EvaluationNotes { get; set; }
    public DateTime EvaluatedAtUtc { get; set; } = DateTime.UtcNow;
}
