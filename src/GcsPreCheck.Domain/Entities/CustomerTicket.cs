using GcsPreCheck.Domain.Common;
using GcsPreCheck.Domain.Enums;

namespace GcsPreCheck.Domain.Entities;

public class CustomerTicket : BaseEntity
{
    public string TicketNumber { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public CustomerClassification CustomerClassification { get; set; } = CustomerClassification.Unknown;
    public PmRoutingStatus PmRoutingStatus { get; set; } = PmRoutingStatus.NotRequired;
    
    public Guid? PmApprovedByUserId { get; set; }
    public ApplicationUser? PmApprovedByUser { get; set; }
    public DateTime? PmApprovedAtUtc { get; set; }
    public string? PmApprovalNotes { get; set; }

    public decimal? OcrOverallConfidence { get; set; }
    public VerificationStatus VerificationStatus { get; set; } = VerificationStatus.PendingVerification;
    
    public Guid? VerifiedByUserId { get; set; }
    public ApplicationUser? VerifiedByUser { get; set; }
    public DateTime? VerifiedAtUtc { get; set; }

    public Guid CreatedByUserId { get; set; }
    public ApplicationUser? CreatedByUser { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public ICollection<TicketAttachment> Attachments { get; set; } = new List<TicketAttachment>();
    public ICollection<TicketRequirementItem> RequirementItems { get; set; } = new List<TicketRequirementItem>();
    public ICollection<PreCheckJobQueue> JobQueueEntries { get; set; } = new List<PreCheckJobQueue>();
    public ICollection<PreCheckRun> PreCheckRuns { get; set; } = new List<PreCheckRun>();
}
