using System.Security;
using GcsPreCheck.Application.Common.Interfaces;
using GcsPreCheck.Application.Features.Tickets.DTOs;
using GcsPreCheck.Domain.Entities;
using GcsPreCheck.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace GcsPreCheck.Application.Features.Tickets.Services;

public interface ITicketIngestionService
{
    Task<TicketDetailDto> IngestTicketAsync(UploadTicketRequest request, CancellationToken ct = default);
    Task<TicketDetailDto?> GetTicketByIdAsync(Guid ticketId, CancellationToken ct = default);
}

public class TicketIngestionService : ITicketIngestionService
{
    private readonly IApplicationDbContext _context;
    private readonly IAttachmentStorageService _storageService;
    private readonly IAntivirusScanner _antivirusScanner;
    private readonly IOcrEngine _ocrEngine;
    private readonly ITicketParameterExtractor _parameterExtractor;
    private readonly ILogger<TicketIngestionService> _logger;

    public TicketIngestionService(
        IApplicationDbContext context,
        IAttachmentStorageService storageService,
        IAntivirusScanner antivirusScanner,
        IOcrEngine ocrEngine,
        ITicketParameterExtractor parameterExtractor,
        ILogger<TicketIngestionService> logger)
    {
        _context = context;
        _storageService = storageService;
        _antivirusScanner = antivirusScanner;
        _ocrEngine = ocrEngine;
        _parameterExtractor = parameterExtractor;
        _logger = logger;
    }

    public async Task<TicketDetailDto> IngestTicketAsync(UploadTicketRequest request, CancellationToken ct = default)
    {
        if (request.Screenshots == null || request.Screenshots.Count == 0)
        {
            throw new ArgumentException("At least one screenshot attachment is required for ticket ingestion.");
        }

        // 1. Enforce Customer Classification Governance
        var classification = request.SuggestedClassification;
        var pmStatus = classification switch
        {
            CustomerClassification.ManagedServices => PmRoutingStatus.PendingPmApproval,
            CustomerClassification.Unknown => PmRoutingStatus.PendingPmApproval,
            CustomerClassification.eLite => PmRoutingStatus.NotRequired,
            _ => PmRoutingStatus.NotRequired
        };

        var ticket = new CustomerTicket
        {
            TicketNumber = request.TicketNumber.Trim(),
            CustomerName = request.CustomerName.Trim(),
            CustomerClassification = classification,
            PmRoutingStatus = pmStatus,
            VerificationStatus = classification == CustomerClassification.OutOfScope 
                ? VerificationStatus.Rejected 
                : VerificationStatus.PendingVerification,
            CreatedByUserId = request.UploadedByUserId
        };

        _context.CustomerTickets.Add(ticket);

        var combinedOcrText = new System.Text.StringBuilder();
        var allLines = new List<OcrLine>();
        var totalConfidenceSum = 0m;
        var validOcrPages = 0;

        // 2. Process each screenshot through Security & OCR Pipeline
        foreach (var file in request.Screenshots)
        {
            // Validate Image Header (Magic bytes & dimensions)
            var validation = _storageService.ValidateImageHeader(file.Data);
            if (!validation.IsValid)
            {
                throw new InvalidOperationException($"Invalid attachment '{file.FileName}': {validation.ErrorMessage}");
            }

            // Save to temporary quarantine
            var storedInfo = await _storageService.SaveToQuarantineAsync(file.Data, file.FileName, ct);

            // Fail-Closed Antivirus Scan
            var avResult = await _antivirusScanner.ScanFileAsync(storedInfo.FullDiskPath, ct);
            if (!avResult.IsClean)
            {
                await _storageService.DeleteQuarantineFileAsync(storedInfo.FullDiskPath);
                _logger.LogCritical("Security Alert: File '{FileName}' failed antivirus inspection. Threat: {Threat}, Reason: {Reason}",
                    file.FileName, avResult.ThreatName, avResult.FailureReason);

                _context.AuditLogs.Add(new AuditLog
                {
                    UserId = request.UploadedByUserId,
                    Action = "SECURITY_AV_SCAN_FAILED",
                    EntityName = "TicketAttachment",
                    EntityId = file.FileName,
                    AuditPayloadJson = System.Text.Json.JsonSerializer.Serialize(avResult)
                });

                await _context.SaveChangesAsync(ct);
                throw new SecurityException($"Antivirus inspection failed for '{file.FileName}': {avResult.FailureReason ?? avResult.ThreatName}");
            }

            // Promote from quarantine to verified permanent storage
            var relativePath = await _storageService.PromoteFromQuarantineToVerifiedAsync(storedInfo.FullDiskPath, ct);

            var attachment = new TicketAttachment
            {
                TicketId = ticket.Id,
                OriginalFileName = file.FileName,
                StorageRelativePath = relativePath,
                FileSizeBytes = storedInfo.FileSizeBytes,
                MimeType = storedInfo.MimeType,
                Sha256Checksum = storedInfo.Sha256Hash,
                AntivirusScanStatus = AntivirusScanStatus.Clean,
                OcrProcessingStatus = OcrProcessingStatus.Pending,
                UploadedByUserId = request.UploadedByUserId
            };

            ticket.Attachments.Add(attachment);

            // Execute local offline OCR
            try
            {
                var ocrResult = await _ocrEngine.ExtractTextAsync(file.Data, ct);
                attachment.OcrProcessingStatus = OcrProcessingStatus.Processed;
                combinedOcrText.AppendLine(ocrResult.FullText);
                allLines.AddRange(ocrResult.Lines);

                totalConfidenceSum += ocrResult.OverallConfidence;
                validOcrPages++;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "OCR extraction failed for attachment '{FileName}'", file.FileName);
                attachment.OcrProcessingStatus = OcrProcessingStatus.Failed;
            }
        }

        // 3. Extract Pre-Check Target Requirements
        var overallConfidence = validOcrPages > 0 ? (totalConfidenceSum / validOcrPages) : 0m;
        ticket.OcrOverallConfidence = Math.Round(overallConfidence, 4);

        var extraction = _parameterExtractor.ExtractParameters(combinedOcrText.ToString(), allLines);

        foreach (var candidate in extraction.Parameters)
        {
            var item = new TicketRequirementItem
            {
                TicketId = ticket.Id,
                ParameterCode = candidate.ParameterCode,
                ScopeQualifier = candidate.ScopeQualifier,
                ExtractedRawText = candidate.ExtractedValue,
                OcrConfidenceScore = Math.Round(candidate.ConfidenceScore, 4),
                EmployeeVerifiedValue = candidate.ExtractedValue, // Pre-populated for employee review
                FieldVerificationStatus = FieldVerificationStatus.PendingReview
            };

            ticket.RequirementItems.Add(item);
        }

        // Audit Trail
        _context.AuditLogs.Add(new AuditLog
        {
            UserId = request.UploadedByUserId,
            Action = "TICKET_INGESTED",
            EntityName = "CustomerTicket",
            EntityId = ticket.Id.ToString(),
            AuditPayloadJson = System.Text.Json.JsonSerializer.Serialize(new
            {
                ticket.TicketNumber,
                ticket.CustomerClassification,
                ticket.PmRoutingStatus,
                ExtractedCount = ticket.RequirementItems.Count,
                Confidence = ticket.OcrOverallConfidence
            })
        });

        await _context.SaveChangesAsync(ct);

        return await MapToDetailDtoAsync(ticket.Id, ct)
               ?? throw new InvalidOperationException("Failed to reload newly ingested ticket.");
    }

    public async Task<TicketDetailDto?> GetTicketByIdAsync(Guid ticketId, CancellationToken ct = default)
    {
        return await MapToDetailDtoAsync(ticketId, ct);
    }

    private async Task<TicketDetailDto?> MapToDetailDtoAsync(Guid ticketId, CancellationToken ct)
    {
        var t = await _context.CustomerTickets
            .Include(t => t.Attachments)
            .Include(t => t.RequirementItems)
                .ThenInclude(r => r.VerifiedByUser)
            .Include(t => t.PmApprovedByUser)
            .Include(t => t.VerifiedByUser)
            .FirstOrDefaultAsync(x => x.Id == ticketId, ct);

        if (t == null) return null;

        return new TicketDetailDto(
            t.Id,
            t.TicketNumber,
            t.CustomerName,
            t.CustomerClassification,
            t.PmRoutingStatus,
            t.PmApprovedByUser?.FullName,
            t.PmApprovedAtUtc,
            t.PmApprovalNotes,
            t.OcrOverallConfidence,
            t.VerificationStatus,
            t.VerifiedByUser?.FullName,
            t.VerifiedAtUtc,
            t.CreatedAtUtc,
            t.Attachments.Select(a => new TicketAttachmentDto(
                a.Id,
                a.OriginalFileName,
                a.StorageRelativePath,
                a.FileSizeBytes,
                a.MimeType,
                a.Sha256Checksum,
                a.AntivirusScanStatus,
                a.OcrProcessingStatus,
                a.UploadedAtUtc
            )).ToList(),
            t.RequirementItems.Select(r => new TicketRequirementItemDto(
                r.Id,
                r.ParameterCode,
                r.ScopeQualifier,
                r.ExtractedRawText,
                r.OcrConfidenceScore,
                r.EmployeeVerifiedValue,
                r.FieldVerificationStatus,
                r.VerifiedByUserId,
                r.VerifiedByUser?.FullName,
                r.VerifiedAtUtc,
                r.CorrectionReason
            )).ToList()
        );
    }
}
