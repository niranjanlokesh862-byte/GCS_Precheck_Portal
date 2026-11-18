using GcsPreCheck.Domain.Common;
using GcsPreCheck.Domain.Enums;

namespace GcsPreCheck.Domain.Entities;

public class TicketRequirementItem : BaseEntity
{
    public Guid TicketId { get; set; }
    public CustomerTicket? Ticket { get; set; }

    public string ParameterCode { get; set; } = string.Empty;
    public string ScopeQualifier { get; set; } = "DEFAULT";
    public string? ExtractedRawText { get; set; }
    public decimal? OcrConfidenceScore { get; set; }
    public string? EmployeeVerifiedValue { get; set; }
    public FieldVerificationStatus FieldVerificationStatus { get; set; } = FieldVerificationStatus.PendingReview;

    public Guid? VerifiedByUserId { get; set; }
    public ApplicationUser? VerifiedByUser { get; set; }
    public DateTime? VerifiedAtUtc { get; set; }

    public string? CorrectionReason { get; set; }
}
