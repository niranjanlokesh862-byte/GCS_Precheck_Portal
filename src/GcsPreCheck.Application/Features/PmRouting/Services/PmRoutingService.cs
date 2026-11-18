using GcsPreCheck.Application.Common.Interfaces;
using GcsPreCheck.Application.Features.PmRouting.DTOs;
using GcsPreCheck.Domain.Entities;
using GcsPreCheck.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace GcsPreCheck.Application.Features.PmRouting.Services;

public interface IPmRoutingService
{
    Task<IReadOnlyList<PmQueueItemDto>> GetPendingPmQueueAsync(CancellationToken ct = default);
    Task ProcessPmDecisionAsync(Guid ticketId, PmApprovalDecisionRequest request, CancellationToken ct = default);
}

public class PmRoutingService : IPmRoutingService
{
    private readonly IApplicationDbContext _context;
    private readonly ILogger<PmRoutingService> _logger;

    public PmRoutingService(IApplicationDbContext context, ILogger<PmRoutingService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<IReadOnlyList<PmQueueItemDto>> GetPendingPmQueueAsync(CancellationToken ct = default)
    {
        return await _context.CustomerTickets
            .AsNoTracking()
            .Where(t => t.PmRoutingStatus == PmRoutingStatus.PendingPmApproval)
            .OrderBy(t => t.CreatedAtUtc)
            .Select(t => new PmQueueItemDto(
                t.Id,
                t.TicketNumber,
                t.CustomerName,
                t.CustomerClassification,
                t.PmRoutingStatus,
                t.Attachments.Count,
                t.OcrOverallConfidence,
                t.CreatedAtUtc
            ))
            .ToListAsync(ct);
    }

    public async Task ProcessPmDecisionAsync(Guid ticketId, PmApprovalDecisionRequest request, CancellationToken ct = default)
    {
        var ticket = await _context.CustomerTickets
            .FirstOrDefaultAsync(t => t.Id == ticketId, ct);

        if (ticket == null)
            throw new KeyNotFoundException($"Customer ticket with ID '{ticketId}' not found.");

        if (ticket.PmRoutingStatus != PmRoutingStatus.PendingPmApproval)
            throw new InvalidOperationException($"Ticket '{ticket.TicketNumber}' is not pending PM approval (current status: {ticket.PmRoutingStatus}).");

        if (request.IsApproved)
        {
            if (request.DecidedClassification == CustomerClassification.Unknown || 
                request.DecidedClassification == CustomerClassification.OutOfScope)
            {
                throw new ArgumentException("Approved ticket cannot remain 'Unknown' or 'OutOfScope'. PM must assign 'eLite' or 'ManagedServices'.");
            }

            ticket.CustomerClassification = request.DecidedClassification;
            ticket.PmRoutingStatus = PmRoutingStatus.ApprovedByPm;
            ticket.PmApprovedByUserId = request.PmUserId;
            ticket.PmApprovedAtUtc = DateTime.UtcNow;
            ticket.PmApprovalNotes = request.ApprovalNotes;
        }
        else
        {
            ticket.PmRoutingStatus = PmRoutingStatus.RejectedByPm;
            ticket.VerificationStatus = VerificationStatus.Rejected;
            ticket.PmApprovedByUserId = request.PmUserId;
            ticket.PmApprovedAtUtc = DateTime.UtcNow;
            ticket.PmApprovalNotes = $"REJECTED: {request.ApprovalNotes}";
        }

        _context.AuditLogs.Add(new AuditLog
        {
            UserId = request.PmUserId,
            Action = request.IsApproved ? "PM_TICKET_APPROVED" : "PM_TICKET_REJECTED",
            EntityName = "CustomerTicket",
            EntityId = ticket.Id.ToString(),
            AuditPayloadJson = System.Text.Json.JsonSerializer.Serialize(new
            {
                ticket.TicketNumber,
                AssignedClassification = ticket.CustomerClassification,
                request.IsApproved,
                request.ApprovalNotes
            })
        });

        await _context.SaveChangesAsync(ct);
        _logger.LogInformation("PM decision processed for ticket {TicketNumber}: Approved={IsApproved}", ticket.TicketNumber, request.IsApproved);
    }
}
