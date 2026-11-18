using GcsPreCheck.Application.Common.Interfaces;
using GcsPreCheck.Application.Features.Tickets.DTOs;
using GcsPreCheck.Application.Features.Tickets.Services;
using GcsPreCheck.Domain.Enums;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GcsPreCheck.WebApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class TicketsController : ControllerBase
{
    private readonly ITicketIngestionService _ingestionService;
    private readonly IFieldVerificationService _verificationService;
    private readonly IAttachmentStorageService _storageService;
    private readonly IApplicationDbContext _context;

    public TicketsController(
        ITicketIngestionService ingestionService,
        IFieldVerificationService verificationService,
        IAttachmentStorageService storageService,
        IApplicationDbContext context)
    {
        _ingestionService = ingestionService;
        _verificationService = verificationService;
        _storageService = storageService;
        _context = context;
    }

    [HttpGet]
    public async Task<IActionResult> GetAllTickets(CancellationToken ct)
    {
        var tickets = await _context.CustomerTickets
            .Include(t => t.RequirementItems)
            .Include(t => t.Attachments)
            .OrderByDescending(t => t.CreatedAtUtc)
            .Select(t => new TicketSummaryDto(
                t.Id,
                t.TicketNumber,
                t.CustomerName,
                t.CustomerClassification.ToString(),
                t.PmRoutingStatus.ToString(),
                t.VerificationStatus.ToString(),
                t.RequirementItems.Count,
                t.RequirementItems.Count(r => r.FieldStatus == FieldVerificationStatus.Verified || r.FieldStatus == FieldVerificationStatus.Modified),
                t.Attachments.Count,
                t.CreatedAtUtc
            ))
            .ToListAsync(ct);

        return Ok(tickets);
    }

    [HttpGet("dashboard-metrics")]
    public async Task<IActionResult> GetDashboardMetrics(CancellationToken ct)
    {
        var totalTickets = await _context.CustomerTickets.CountAsync(ct);
        var verifiedTickets = await _context.CustomerTickets.CountAsync(t => t.VerificationStatus == VerificationStatus.RequirementsVerified, ct);
        var pendingPmApprovals = await _context.CustomerTickets.CountAsync(t => t.PmRoutingStatus == PmRoutingStatus.PendingPmApproval, ct);
        var totalRuns = await _context.PreCheckRuns.CountAsync(ct);
        var passedRuns = await _context.PreCheckRuns.CountAsync(r => r.OverallResult == OverallResult.PASS, ct);
        var failedRuns = await _context.PreCheckRuns.CountAsync(r => r.OverallResult == OverallResult.FAIL, ct);

        return Ok(new
        {
            totalTickets,
            verifiedTickets,
            pendingPmApprovals,
            totalRuns,
            passedRuns,
            failedRuns
        });
    }

    [HttpPost("upload")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> UploadTicket(
        [FromForm] string ticketNumber,
        [FromForm] string customerName,
        [FromForm] CustomerClassification suggestedClassification,
        [FromForm] Guid uploadedByUserId,
        [FromForm] List<IFormFile> files,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(ticketNumber))
            return BadRequest(new { message = "TicketNumber is required." });

        if (files == null || files.Count == 0)
            return BadRequest(new { message = "At least one screenshot file is required." });

        var screenshotItems = new List<UploadScreenshotItem>();
        foreach (var file in files)
        {
            using var ms = new MemoryStream();
            await file.CopyToAsync(ms, ct);
            screenshotItems.Add(new UploadScreenshotItem(
                FileName: file.FileName,
                ContentType: file.ContentType,
                Data: ms.ToArray()));
        }

        var request = new UploadTicketRequest(
            TicketNumber: ticketNumber,
            CustomerName: customerName,
            SuggestedClassification: suggestedClassification,
            Screenshots: screenshotItems,
            UploadedByUserId: uploadedByUserId);

        var result = await _ingestionService.IngestTicketAsync(request, ct);
        return CreatedAtAction(nameof(GetTicket), new { id = result.Id }, result);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetTicket(Guid id, CancellationToken ct)
    {
        var ticket = await _ingestionService.GetTicketByIdAsync(id, ct);
        if (ticket == null)
            return NotFound(new { message = $"Ticket '{id}' not found." });

        return Ok(ticket);
    }

    [HttpPut("{id:guid}/fields/{fieldId:guid}/verify")]
    public async Task<IActionResult> VerifyField(Guid id, Guid fieldId, [FromBody] VerifyFieldRequest request, CancellationToken ct)
    {
        var updatedItem = await _verificationService.VerifyFieldAsync(fieldId, request, ct);
        return Ok(updatedItem);
    }

    [HttpPost("{id:guid}/complete-review")]
    public async Task<IActionResult> CompleteReview(Guid id, [FromQuery] Guid employeeUserId, CancellationToken ct)
    {
        var ticket = await _verificationService.CompleteTicketReviewAsync(id, employeeUserId, ct);
        return Ok(ticket);
    }

    [HttpGet("{id:guid}/attachments/{attachmentId:guid}/stream")]
    public async Task<IActionResult> StreamAttachment(Guid id, Guid attachmentId, CancellationToken ct)
    {
        var attachment = await _context.TicketAttachments.FindAsync(new object[] { attachmentId }, ct);
        if (attachment == null || attachment.TicketId != id)
            return NotFound();

        var stream = await _storageService.OpenReadStreamAsync(attachment.StorageRelativePath, ct);
        if (stream == null)
            return NotFound(new { message = "Physical attachment file not found on storage." });

        return File(stream, attachment.MimeType, attachment.OriginalFileName);
    }
}
