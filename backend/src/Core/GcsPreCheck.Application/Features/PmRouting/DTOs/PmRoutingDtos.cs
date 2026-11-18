using GcsPreCheck.Domain.Enums;

namespace GcsPreCheck.Application.Features.PmRouting.DTOs;

public record PmQueueItemDto(
    Guid TicketId,
    string TicketNumber,
    string CustomerName,
    CustomerClassification Classification,
    PmRoutingStatus PmStatus,
    int ScreenshotCount,
    decimal? OcrOverallConfidence,
    DateTime UploadedAtUtc);

public record PmApprovalDecisionRequest(
    CustomerClassification DecidedClassification,
    bool IsApproved,
    string ApprovalNotes,
    Guid PmUserId);
