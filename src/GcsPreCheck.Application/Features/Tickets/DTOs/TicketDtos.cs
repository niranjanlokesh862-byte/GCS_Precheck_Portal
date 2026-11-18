using GcsPreCheck.Domain.Enums;

namespace GcsPreCheck.Application.Features.Tickets.DTOs;

public record UploadScreenshotItem(
    string FileName,
    string ContentType,
    byte[] Data);

public record UploadTicketRequest(
    string TicketNumber,
    string CustomerName,
    CustomerClassification SuggestedClassification,
    IReadOnlyList<UploadScreenshotItem> Screenshots,
    Guid UploadedByUserId);

public record TicketRequirementItemDto(
    Guid Id,
    string ParameterCode,
    string ScopeQualifier,
    string? ExtractedRawText,
    decimal? OcrConfidenceScore,
    string? EmployeeVerifiedValue,
    FieldVerificationStatus FieldVerificationStatus,
    Guid? VerifiedByUserId,
    string? VerifiedByUserName,
    DateTime? VerifiedAtUtc,
    string? CorrectionReason);

public record TicketAttachmentDto(
    Guid Id,
    string OriginalFileName,
    string StorageRelativePath,
    long FileSizeBytes,
    string MimeType,
    string Sha256Checksum,
    AntivirusScanStatus AntivirusScanStatus,
    OcrProcessingStatus OcrProcessingStatus,
    DateTime UploadedAtUtc);

public record TicketDetailDto(
    Guid Id,
    string TicketNumber,
    string CustomerName,
    CustomerClassification CustomerClassification,
    PmRoutingStatus PmRoutingStatus,
    string? PmApprovedByUserName,
    DateTime? PmApprovedAtUtc,
    string? PmApprovalNotes,
    decimal? OcrOverallConfidence,
    VerificationStatus VerificationStatus,
    string? VerifiedByUserName,
    DateTime? VerifiedAtUtc,
    DateTime CreatedAtUtc,
    IReadOnlyList<TicketAttachmentDto> Attachments,
    IReadOnlyList<TicketRequirementItemDto> RequirementItems);

public record VerifyFieldRequest(
    string VerifiedValue,
    bool IsModified,
    string? CorrectionReason,
    Guid VerifiedByUserId);
