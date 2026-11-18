using GcsPreCheck.Domain.Entities;
using GcsPreCheck.Domain.Enums;

namespace GcsPreCheck.Application.Features.RulesEngine;

public record ParameterEvaluationInput(
    string ParameterCode,
    string ParameterName,
    string TicketExpectedValue,
    string? StudioActualValue,
    string? ScreenshotPath = null,
    string? DomHash = null);

public interface ISopRuleEvaluationEngine
{
    Task<ParameterCheckResult> EvaluateParameterAsync(
        Guid runId,
        ParameterEvaluationInput input,
        CancellationToken ct = default);

    Task<List<ParameterCheckResult>> EvaluateAllParametersAsync(
        Guid runId,
        IEnumerable<ParameterEvaluationInput> inputs,
        CancellationToken ct = default);
}
