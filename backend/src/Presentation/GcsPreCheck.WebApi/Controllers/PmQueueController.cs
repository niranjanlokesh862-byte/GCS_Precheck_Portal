using GcsPreCheck.Application.Features.PmRouting.DTOs;
using GcsPreCheck.Application.Features.PmRouting.Services;
using Microsoft.AspNetCore.Mvc;

namespace GcsPreCheck.WebApi.Controllers;

[ApiController]
[Route("api/pm")]
public class PmQueueController : ControllerBase
{
    private readonly IPmRoutingService _pmService;

    public PmQueueController(IPmRoutingService pmService)
    {
        _pmService = pmService;
    }

    [HttpGet("queue")]
    public async Task<IActionResult> GetPendingQueue(CancellationToken ct)
    {
        var items = await _pmService.GetPendingPmQueueAsync(ct);
        return Ok(items);
    }

    [HttpPost("tickets/{ticketId:guid}/decision")]
    public async Task<IActionResult> SubmitDecision(Guid ticketId, [FromBody] PmApprovalDecisionRequest request, CancellationToken ct)
    {
        await _pmService.ProcessPmDecisionAsync(ticketId, request, ct);
        return Ok(new { message = "PM decision applied successfully.", ticketId, isApproved = request.IsApproved });
    }
}
