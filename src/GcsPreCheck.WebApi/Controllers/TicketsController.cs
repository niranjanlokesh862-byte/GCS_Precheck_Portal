using GcsPreCheck.Application.Common.Interfaces;
using GcsPreCheck.Application.Features.Tickets.DTOs;
using GcsPreCheck.Application.Features.Tickets.Services;
using GcsPreCheck.Domain.Enums;
using Microsoft.AspNetCore.Mvc;

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
