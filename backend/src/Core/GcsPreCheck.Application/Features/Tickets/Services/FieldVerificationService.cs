using GcsPreCheck.Application.Common.Interfaces;
using GcsPreCheck.Application.Features.Tickets.DTOs;
using GcsPreCheck.Domain.Entities;
using GcsPreCheck.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace GcsPreCheck.Application.Features.Tickets.Services;

public interface IFieldVerificationService
{
    Task<TicketRequirementItemDto> VerifyFieldAsync(Guid requirementItemId, VerifyFieldRequest request, CancellationToken ct = default);
    Task<TicketDetailDto> CompleteTicketReviewAsync(Guid ticketId, Guid employeeUserId, CancellationToken ct = default);
}

public class FieldVerificationService : IFieldVerificationService
{
    private readonly IApplicationDbContext _context;
    private readonly ILogger<FieldVerificationService> _logger;

    public FieldVerificationService(IApplicationDbContext context, ILogger<FieldVerificationService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<TicketRequirementItemDto> VerifyFieldAsync(Guid requirementItemId, VerifyFieldRequest request, CancellationToken ct = default)
    {
        var item = await _context.TicketRequirementItems
            .Include(r => r.Ticket)
            .FirstOrDefaultAsync(r => r.Id == requirementItemId, ct);

        if (item == null)
            throw new KeyNotFoundException($"Requirement item '{requirementItemId}' not found.");

        if (item.Ticket == null)
            throw new InvalidOperationException("Requirement item has no associated customer ticket.");

        if (item.Ticket.VerificationStatus == VerificationStatus.RequirementsVerified)
            throw new InvalidOperationException("Cannot modify fields on a ticket that has already been verified and locked.");

        item.EmployeeVerifiedValue = request.VerifiedValue?.Trim() ?? string.Empty;
        item.FieldVerificationStatus = request.IsModified ? FieldVerificationStatus.Modified : FieldVerificationStatus.Verified;
        item.VerifiedByUserId = request.VerifiedByUserId;
        item.VerifiedAtUtc = DateTime.UtcNow;
        item.CorrectionReason = request.IsModified ? request.CorrectionReason : null;

        await _context.SaveChangesAsync(ct);

        var employee = await _context.ApplicationUsers.FindAsync(new object[] { request.VerifiedByUserId }, ct);

        return new TicketRequirementItemDto(
            item.Id,
            item.ParameterCode,
            item.ScopeQualifier,
            item.ExtractedRawText,
            item.OcrConfidenceScore,
            item.EmployeeVerifiedValue,
            item.FieldVerificationStatus,
            item.VerifiedByUserId,
            employee?.FullName,
            item.VerifiedAtUtc,
            item.CorrectionReason
        );
    }

    public async Task<TicketDetailDto> CompleteTicketReviewAsync(Guid ticketId, Guid employeeUserId, CancellationToken ct = default)
    {
        var ticket = await _context.CustomerTickets
            .Include(t => t.RequirementItems)
            .Include(t => t.Attachments)
            .Include(t => t.PmApprovedByUser)
            .FirstOrDefaultAsync(t => t.Id == ticketId, ct);

        if (ticket == null)
            throw new KeyNotFoundException($"Customer ticket '{ticketId}' not found.");

        // PM Gate Check: If ticket requires PM approval and has not been approved yet, abort!
        if (ticket.PmRoutingStatus == PmRoutingStatus.PendingPmApproval)
        {
            throw new InvalidOperationException($"Ticket '{ticket.TicketNumber}' is locked pending Project Manager approval.");
        }

        if (ticket.VerificationStatus == VerificationStatus.Rejected)
        {
            throw new InvalidOperationException($"Ticket '{ticket.TicketNumber}' has been rejected and cannot be verified.");
        }

        // Validate that EVERY individual requirement item has been reviewed (not PendingReview)
        var unreviewed = ticket.RequirementItems
            .Where(r => r.FieldVerificationStatus == FieldVerificationStatus.PendingReview)
            .ToList();

        if (unreviewed.Count > 0)
        {
            var missingCodes = string.Join(", ", unreviewed.Select(u => u.ParameterCode));
            throw new InvalidOperationException($"Cannot complete review. The following {unreviewed.Count} parameters still require employee verification: {missingCodes}");
        }

        ticket.VerificationStatus = VerificationStatus.RequirementsVerified;
        ticket.VerifiedByUserId = employeeUserId;
        ticket.VerifiedAtUtc = DateTime.UtcNow;

        _context.AuditLogs.Add(new AuditLog
        {
            UserId = employeeUserId,
            Action = "TICKET_VERIFICATION_COMPLETED",
            EntityName = "CustomerTicket",
            EntityId = ticket.Id.ToString(),
            AuditPayloadJson = System.Text.Json.JsonSerializer.Serialize(new
            {
                ticket.TicketNumber,
                TotalParametersVerified = ticket.RequirementItems.Count,
                ModifiedCount = ticket.RequirementItems.Count(r => r.FieldVerificationStatus == FieldVerificationStatus.Modified)
            })
        });

        await _context.SaveChangesAsync(ct);
        _logger.LogInformation("Ticket {TicketNumber} successfully verified by employee {UserId}", ticket.TicketNumber, employeeUserId);

        var employee = await _context.ApplicationUsers.FindAsync(new object[] { employeeUserId }, ct);

        return new TicketDetailDto(
            ticket.Id,
            ticket.TicketNumber,
            ticket.CustomerName,
            ticket.CustomerClassification,
            ticket.PmRoutingStatus,
            ticket.PmApprovedByUser?.FullName,
            ticket.PmApprovedAtUtc,
            ticket.PmApprovalNotes,
            ticket.OcrOverallConfidence,
            ticket.VerificationStatus,
            employee?.FullName,
            ticket.VerifiedAtUtc,
            ticket.CreatedAtUtc,
            ticket.Attachments.Select(a => new TicketAttachmentDto(
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
            ticket.RequirementItems.Select(r => new TicketRequirementItemDto(
                r.Id,
                r.ParameterCode,
                r.ScopeQualifier,
                r.ExtractedRawText,
                r.OcrConfidenceScore,
                r.EmployeeVerifiedValue,
                r.FieldVerificationStatus,
                r.VerifiedByUserId,
                employee?.FullName,
                r.VerifiedAtUtc,
                r.CorrectionReason
            )).ToList()
        );
    }
}
