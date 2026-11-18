using GcsPreCheck.Application.Common.Interfaces;
using GcsPreCheck.Domain.Entities;
using GcsPreCheck.Domain.Enums;
using GcsPreCheck.Infrastructure.NetworkSecurity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GcsPreCheck.WebApi.Controllers;

public record StartPreCheckRequest(
    Guid TicketId,
    string StudioUrl,
    string Username,
    string Password,
    TargetEnvironment Environment,
    Guid ExecutedByUserId);

public record PreCheckJobStatusDto(
    Guid JobId,
    Guid TicketId,
    string StudioUrl,
    ExecutionStatus ExecutionStatus,
    string? LeasedByWorkerId,
    DateTime? LeaseExpiresAtUtc,
    DateTime? CompletedAtUtc,
    string? ErrorMessage,
    Guid? RunId,
    OverallResult? OverallResult);

[ApiController]
[Route("api/precheck")]
public class PreCheckController : ControllerBase
{
    private readonly IApplicationDbContext _context;
    private readonly IStudioNetworkValidator _networkValidator;
    private readonly IEphemeralCredentialVault _credentialVault;
    private readonly IPdfReportGenerator _reportGenerator;
    private readonly ILogger<PreCheckController> _logger;

    public PreCheckController(
        IApplicationDbContext context,
        IStudioNetworkValidator networkValidator,
        IEphemeralCredentialVault credentialVault,
        IPdfReportGenerator reportGenerator,
        ILogger<PreCheckController> logger)
    {
        _context = context;
        _networkValidator = networkValidator;
        _credentialVault = credentialVault;
        _reportGenerator = reportGenerator;
        _logger = logger;
    }

    [HttpPost("start")]
    public async Task<IActionResult> StartPreCheck([FromBody] StartPreCheckRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.StudioUrl) ||
            string.IsNullOrWhiteSpace(request.Username) ||
            string.IsNullOrWhiteSpace(request.Password))
        {
            return BadRequest(new { message = "StudioUrl, Username, and Password are all required." });
        }

        // 1. Verify Ticket Verification Gate
        var ticket = await _context.CustomerTickets
            .FirstOrDefaultAsync(t => t.Id == request.TicketId, ct);

        if (ticket == null)
            return NotFound(new { message = $"Customer ticket '{request.TicketId}' not found." });

        if (ticket.VerificationStatus != VerificationStatus.RequirementsVerified)
        {
            return StatusCode(StatusCodes.Status412PreconditionFailed, new
            {
                message = "Pre-check cannot proceed. Ticket requirements have not been completely reviewed and verified by an employee."
            });
        }

        // 2. Verify PM Routing Gate
        if (ticket.PmRoutingStatus == PmRoutingStatus.PendingPmApproval)
        {
            return StatusCode(StatusCodes.Status412PreconditionFailed, new
            {
                message = "Pre-check cannot proceed. Ticket is locked pending Project Manager approval."
            });
        }

        if (ticket.CustomerClassification == CustomerClassification.OutOfScope ||
            ticket.VerificationStatus == VerificationStatus.Rejected)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new
            {
                message = "Pre-check forbidden. Customer or ticket is marked Out of Scope."
            });
        }

        // 3. Validate Destination against SSRF & IT Allowlists
        if (!Uri.TryCreate(request.StudioUrl.Trim(), UriKind.Absolute, out var targetUri))
        {
            return BadRequest(new { message = "Invalid Studio URL format." });
        }

        var networkCheck = await _networkValidator.ValidateDestinationAsync(targetUri, request.Environment, ct);
        if (!networkCheck.IsAllowed)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new
            {
                message = $"Studio URL rejected by Security Policy: {networkCheck.FailureReason}"
            });
        }

        // 4. Generate Single-Use Session Nonce & Store Secrets in Ephemeral RAM Vault (90s TTL)
        var sessionNonce = Guid.NewGuid();
        _credentialVault.StoreCredentials(sessionNonce, request.Username.Trim(), request.Password, TimeSpan.FromSeconds(90));

        // 5. Enqueue Job in Durable SQL Queue (Strictly NO Passwords in Queue)
        var job = new PreCheckJobQueue
        {
            TicketId = ticket.Id,
            StudioUrl = targetUri.ToString(),
            SessionNonce = sessionNonce,
            ExecutionStatus = ExecutionStatus.QUEUED,
            Priority = 10,
            QueuedAtUtc = DateTime.UtcNow,
            ScheduledAtUtc = DateTime.UtcNow
        };

        _context.PreCheckJobQueue.Add(job);

        // Audit Trail
        _context.AuditLogs.Add(new AuditLog
        {
            UserId = request.ExecutedByUserId,
            Action = "PRECHECK_JOB_QUEUED",
            EntityName = "PreCheckJobQueue",
            EntityId = job.Id.ToString(),
            AuditPayloadJson = System.Text.Json.JsonSerializer.Serialize(new
            {
                ticket.TicketNumber,
                StudioUrl = targetUri.ToString(),
                ResolvedIp = networkCheck.ResolvedIp?.ToString(),
                MatchedAllowlistId = networkCheck.MatchedAllowlistId,
                Environment = request.Environment.ToString()
            })
        });

        await _context.SaveChangesAsync(ct);
        _logger.LogInformation("Enqueued pre-check job {JobId} for ticket {TicketNumber} (Target: {StudioUrl})", job.Id, ticket.TicketNumber, targetUri);

        return Accepted(new
        {
            jobId = job.Id,
            ticketId = ticket.Id,
            executionStatus = job.ExecutionStatus.ToString(),
            queuedAtUtc = job.QueuedAtUtc
        });
    }

    [HttpGet("jobs/{id:guid}")]
    public async Task<IActionResult> GetJobStatus(Guid id, CancellationToken ct)
    {
        var job = await _context.PreCheckJobQueue
            .Include(j => j.Run)
            .AsNoTracking()
            .FirstOrDefaultAsync(j => j.Id == id, ct);

        if (job == null)
            return NotFound(new { message = $"Job '{id}' not found." });

        return Ok(new PreCheckJobStatusDto(
            job.Id,
            job.TicketId,
            job.StudioUrl,
            job.ExecutionStatus,
            job.LeasedByWorkerId,
            job.LeaseExpiresAtUtc,
            job.CompletedAtUtc,
            job.ErrorMessage,
            job.Run?.Id,
            job.Run?.OverallResult
        ));
    }

    [HttpGet("runs/{runId:guid}/report")]
    public async Task<IActionResult> DownloadPreCheckReport(Guid runId, CancellationToken ct)
    {
        var run = await _context.PreCheckRuns
            .Include(r => r.Ticket)
            .Include(r => r.ParameterResults)
            .Include(r => r.ExecutedByUser)
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == runId, ct);

        if (run == null)
            return NotFound(new { message = $"PreCheckRun '{runId}' not found." });

        var parameterDtos = run.ParameterResults
            .Select(p => new ParameterCheckResultDto(
                p.ParameterCode,
                p.ParameterName,
                p.TicketExpectedValue,
                p.StudioActualValue,
                p.ComparisonStatus.ToString(),
                p.ScopeRuleApplied,
                p.EvaluationNotes))
            .ToList();

        var reportData = new PreCheckReportDto(
            run.TicketId,
            run.Ticket?.CustomerName ?? "Enterprise Client",
            run.Ticket?.CustomerClassification.ToString() ?? "eLite",
            run.Id,
            run.StudioUrl,
            run.TargetEnvironment.ToString(),
            run.JobId.ToString(),
            run.CompletedAtUtc ?? run.StartedAtUtc,
            run.OverallResult?.ToString() ?? "NOT_VERIFIED",
            parameterDtos,
            run.ExecutedByUser?.FullName ?? "Enterprise Operator",
            run.CompletedAtUtc ?? DateTime.UtcNow);

        var pdfBytes = await _reportGenerator.GeneratePreCheckReportAsync(reportData, ct);
        return File(pdfBytes, "application/pdf", $"GCS_PreCheck_Report_{run.TicketId:N}.pdf");
    }
}
