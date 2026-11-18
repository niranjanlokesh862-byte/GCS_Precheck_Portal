using GcsPreCheck.Domain.Common;

namespace GcsPreCheck.Domain.Entities;

public class SopConfiguration : BaseEntity
{
    public string ParameterCode { get; set; } = string.Empty;
    public string SopDocumentId { get; set; } = string.Empty;
    public string SopTitle { get; set; } = string.Empty;
    public string SopVersion { get; set; } = string.Empty;
    public string SopDocumentUrl { get; set; } = string.Empty;
    public bool IsApproved { get; set; } = false;

    public Guid? ApprovedByUserId { get; set; }
    public ApplicationUser? ApprovedByUser { get; set; }
    public DateTime? ApprovedAtUtc { get; set; }

    public DateOnly? EffectiveDate { get; set; }
    public DateOnly? RetirementDate { get; set; }
    public bool IsActive { get; set; } = false;

    public ICollection<ParameterCheckResult> ResultsUsingThisSop { get; set; } = new List<ParameterCheckResult>();
}
