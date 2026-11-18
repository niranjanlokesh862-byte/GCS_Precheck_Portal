using GcsPreCheck.Domain.Common;
using GcsPreCheck.Domain.Enums;

namespace GcsPreCheck.Domain.Entities;

public class TicketAttachment : BaseEntity
{
    public Guid TicketId { get; set; }
    public CustomerTicket? Ticket { get; set; }

    public AttachmentType AttachmentType { get; set; } = AttachmentType.CustomerTicketScreenshot;
    public string OriginalFileName { get; set; } = string.Empty;
    public string StorageRelativePath { get; set; } = string.Empty;
    public long FileSizeBytes { get; set; }
    public string MimeType { get; set; } = "image/png";
    public string Sha256Checksum { get; set; } = string.Empty;
    public AntivirusScanStatus AntivirusScanStatus { get; set; } = AntivirusScanStatus.Clean;
    public OcrProcessingStatus OcrProcessingStatus { get; set; } = OcrProcessingStatus.Pending;

    public Guid UploadedByUserId { get; set; }
    public ApplicationUser? UploadedByUser { get; set; }
    public DateTime UploadedAtUtc { get; set; } = DateTime.UtcNow;
}
