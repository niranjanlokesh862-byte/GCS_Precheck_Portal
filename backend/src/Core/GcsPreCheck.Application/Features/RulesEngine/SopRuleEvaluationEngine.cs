using GcsPreCheck.Application.Common.Interfaces;
using GcsPreCheck.Domain.Entities;
using GcsPreCheck.Domain.Enums;
using GcsPreCheck.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace GcsPreCheck.Application.Features.RulesEngine;

public class SopRuleEvaluationEngine : ISopRuleEvaluationEngine
{
    private readonly IApplicationDbContext _context;
    private readonly ILogger<SopRuleEvaluationEngine> _logger;

    public SopRuleEvaluationEngine(IApplicationDbContext context, ILogger<SopRuleEvaluationEngine> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<List<ParameterCheckResult>> EvaluateAllParametersAsync(
        Guid runId,
        IEnumerable<ParameterEvaluationInput> inputs,
        CancellationToken ct = default)
    {
        var results = new List<ParameterCheckResult>();
        foreach (var input in inputs)
        {
            var result = await EvaluateParameterAsync(runId, input, ct);
            results.Add(result);
        }
        return results;
    }

    public async Task<ParameterCheckResult> EvaluateParameterAsync(
        Guid runId,
        ParameterEvaluationInput input,
        CancellationToken ct = default)
    {
        // 1. Fetch matching active approved SOP rule from database
        var sop = await _context.SopConfigurations
            .AsNoTracking()
            .Where(s => s.ParameterCode == input.ParameterCode && s.IsActive && s.IsApproved)
            .OrderByDescending(s => s.EffectiveDate)
            .FirstOrDefaultAsync(ct);

        var ruleApplied = sop?.SopDocumentId ?? $"SOP-DEFAULT-{input.ParameterCode}";
        var sopId = sop?.Id;

        var result = new ParameterCheckResult
        {
            RunId = runId,
            ParameterCode = input.ParameterCode,
            ParameterName = input.ParameterName,
            TicketExpectedValue = input.TicketExpectedValue,
            StudioActualValue = input.StudioActualValue,
            ScopeRuleApplied = ruleApplied,
            SopId = sopId,
            EvidenceScreenshotPath = input.ScreenshotPath,
            DomSnapshotHash = input.DomHash,
            EvaluatedAtUtc = DateTime.UtcNow
        };

        if (string.IsNullOrWhiteSpace(input.StudioActualValue))
        {
            result.ComparisonStatus = ComparisonStatus.NOT_VERIFIED;
            result.EvaluationNotes = "Studio actual value was not located or could not be read from Studio DOM.";
            return result;
        }

        switch (input.ParameterCode.ToUpperInvariant())
        {
            case "SITE_VERSION":
            case "CHEMADVISOR_VERSION":
            case "TRANSPORT_DB_VERSION":
            case "CURRENT_CONTENT_VERSION":
                EvaluateVersionParameter(input, result);
                break;

            case "REQUESTED_CONTENT_VERSION":
                EvaluateRequestedContentParameter(input, result);
                break;

            case "NUMBER_OF_REGIONS":
                EvaluateRegionsCountParameter(input, result);
                break;

            case "JAPAN_REGION":
                EvaluateJapanRegionParameter(input, result);
                break;

            case "TRANSPORTATION":
                EvaluateTransportationParameter(input, result);
                break;

            case "PCN":
                EvaluatePcnParameter(input, result);
                break;

            case "CUSTOMER_CUSTOMIZATION":
                EvaluateCustomizationParameter(input, result);
                break;

            default:
                EvaluateGeneralExactMatch(input, result);
                break;
        }

        return result;
    }

    private static void EvaluateVersionParameter(ParameterEvaluationInput input, ParameterCheckResult result)
    {
        var expectedParsed = GcsVersion.TryParse(input.TicketExpectedValue, out var expectedVer);
        var actualParsed = GcsVersion.TryParse(input.StudioActualValue, out var actualVer);

        if (!expectedParsed || !actualParsed)
        {
            // Fallback to strict normalized string comparison if non-standard format
            var match = string.Equals(input.TicketExpectedValue.Trim(), input.StudioActualValue?.Trim(), StringComparison.OrdinalIgnoreCase);
            result.ComparisonStatus = match ? ComparisonStatus.PASS : ComparisonStatus.FAIL;
            result.EvaluationNotes = match
                ? "String comparison matched (non-standard version string format)."
                : $"Mismatch: Ticket expected '{input.TicketExpectedValue}', Studio reported '{input.StudioActualValue}'.";
            return;
        }

        var comparison = actualVer.CompareTo(expectedVer);
        if (comparison == 0)
        {
            result.ComparisonStatus = ComparisonStatus.PASS;
            result.EvaluationNotes = $"Semantic GcsVersion match: Studio {actualVer.RawString} equals Ticket {expectedVer.RawString}.";
        }
        else
        {
            result.ComparisonStatus = ComparisonStatus.FAIL;
            result.EvaluationNotes = $"Version mismatch: Studio is {actualVer.RawString}, expected {expectedVer.RawString} (Diff: {(comparison > 0 ? "Ahead" : "Behind")}).";
        }
    }

    private static void EvaluateRequestedContentParameter(ParameterEvaluationInput input, ParameterCheckResult result)
    {
        // Requested Content Version is a target prerequisite check
        // The Studio actual value represents current installed content baseline
        var requestedParsed = GcsVersion.TryParse(input.TicketExpectedValue, out var requestedVer);
        var currentParsed = GcsVersion.TryParse(input.StudioActualValue, out var currentVer);

        if (requestedParsed && currentParsed)
        {
            if (currentVer.CompareTo(requestedVer) >= 0)
            {
                result.ComparisonStatus = ComparisonStatus.NEEDS_REVIEW;
                result.EvaluationNotes = $"Target content {requestedVer.RawString} is already at or below installed baseline {currentVer.RawString}. Verify update necessity.";
            }
            else
            {
                result.ComparisonStatus = ComparisonStatus.PASS;
                result.EvaluationNotes = $"Prerequisite satisfied: Installed content {currentVer.RawString} is an eligible baseline for target content {requestedVer.RawString}.";
            }
        }
        else
        {
            result.ComparisonStatus = ComparisonStatus.PASS;
            result.EvaluationNotes = "Requested content prerequisite acknowledged.";
        }
    }

    private static void EvaluateRegionsCountParameter(ParameterEvaluationInput input, ParameterCheckResult result)
    {
        if (int.TryParse(input.TicketExpectedValue.Trim(), out var expectedCount) &&
            int.TryParse(input.StudioActualValue?.Trim(), out var actualCount))
        {
            result.ComparisonStatus = (expectedCount == actualCount) ? ComparisonStatus.PASS : ComparisonStatus.FAIL;
            result.EvaluationNotes = (expectedCount == actualCount)
                ? $"Region count {actualCount} matches ticket requirement."
                : $"Region count mismatch: Ticket specifies {expectedCount}, Studio reported {actualCount}.";
        }
        else
        {
            EvaluateGeneralExactMatch(input, result);
        }
    }

    private static void EvaluateJapanRegionParameter(ParameterEvaluationInput input, ParameterCheckResult result)
    {
        result.IsConditional = true;
        var exp = input.TicketExpectedValue.Trim().ToUpperInvariant();
        var act = input.StudioActualValue?.Trim().ToUpperInvariant() ?? "";

        // If ticket specifies 'N' / 'NO' / 'DISABLED' -> verify Japan is absent or disabled
        if (exp is "N" or "NO" or "FALSE" or "DISABLED" or "ABSENT")
        {
            var isAbsent = act is "DISABLED" or "ABSENT" or "INACTIVE" or "N" or "NO" or "" or "NONE";
            result.ComparisonStatus = isAbsent ? ComparisonStatus.PASS : ComparisonStatus.FAIL;
            result.EvaluationNotes = isAbsent
                ? "SOP verified: Ticket specifies 'N' and Japan region is disabled/absent in Studio."
                : $"SOP violation: Ticket specifies Japan 'N', but Studio reported Japan is '{input.StudioActualValue}'.";
        }
        else
        {
            // Ticket specifies 'Y' / 'YES' / 'ACTIVE' -> verify Japan is present and active
            var isActive = act is "ACTIVE" or "ENABLED" or "Y" or "YES" or "PRESENT";
            result.ComparisonStatus = isActive ? ComparisonStatus.PASS : ComparisonStatus.FAIL;
            result.EvaluationNotes = isActive
                ? "SOP verified: Japan region is configured and active in Studio."
                : $"SOP violation: Ticket specifies Japan 'Y', but Studio reported '{input.StudioActualValue}'.";
        }
    }

    private static void EvaluateTransportationParameter(ParameterEvaluationInput input, ParameterCheckResult result)
    {
        result.IsConditional = true;
        var exp = input.TicketExpectedValue.Trim().ToUpperInvariant();
        var act = input.StudioActualValue?.Trim().ToUpperInvariant() ?? "";

        if (exp is "N" or "NO" or "FALSE" or "DISABLED")
        {
            var isOff = act is "DISABLED" or "INACTIVE" or "N" or "NO" or "ABSENT" or "";
            result.ComparisonStatus = isOff ? ComparisonStatus.PASS : ComparisonStatus.FAIL;
            result.EvaluationNotes = isOff
                ? "SOP verified: Transportation module is disabled/absent as specified by ticket ('N')."
                : $"SOP violation: Ticket specifies Transportation 'N', but Studio reported '{input.StudioActualValue}'.";
        }
        else
        {
            var isOn = act is "ACTIVE" or "ENABLED" or "Y" or "YES";
            result.ComparisonStatus = isOn ? ComparisonStatus.PASS : ComparisonStatus.FAIL;
            result.EvaluationNotes = isOn
                ? "Transportation module is active and verified."
                : $"Transportation module is expected to be active, but Studio reported '{input.StudioActualValue}'.";
        }
    }

    private static void EvaluatePcnParameter(ParameterEvaluationInput input, ParameterCheckResult result)
    {
        result.IsConditional = true;
        var exp = input.TicketExpectedValue.Trim().ToUpperInvariant();
        var act = input.StudioActualValue?.Trim().ToUpperInvariant() ?? "";

        if (exp is "N" or "NO" or "DISABLED")
        {
            var isOff = act is "DISABLED" or "INACTIVE" or "N" or "NO" or "ABSENT" or "";
            result.ComparisonStatus = isOff ? ComparisonStatus.PASS : ComparisonStatus.FAIL;
            result.EvaluationNotes = isOff
                ? "PCN module verified absent/inactive per ticket requirement."
                : $"PCN is expected to be inactive, but Studio reported '{input.StudioActualValue}'.";
        }
        else
        {
            var isOn = act is "CONFIGURED" or "ACTIVE" or "ENABLED" or "Y" or "YES";
            result.ComparisonStatus = isOn ? ComparisonStatus.PASS : ComparisonStatus.FAIL;
            result.EvaluationNotes = isOn
                ? "PCN module is configured and verified."
                : $"PCN module mismatch: Expected active/configured, but Studio reported '{input.StudioActualValue}'.";
        }
    }

    private static void EvaluateCustomizationParameter(ParameterEvaluationInput input, ParameterCheckResult result)
    {
        var exp = input.TicketExpectedValue.Trim();
        var act = input.StudioActualValue?.Trim() ?? "";

        if (string.Equals(exp, act, StringComparison.OrdinalIgnoreCase))
        {
            result.ComparisonStatus = ComparisonStatus.PASS;
            result.EvaluationNotes = $"Customer customization package '{act}' matches ticket specifications.";
        }
        else if (exp.Equals("None", StringComparison.OrdinalIgnoreCase) && (act.Equals("Standard", StringComparison.OrdinalIgnoreCase) || string.IsNullOrEmpty(act)))
        {
            result.ComparisonStatus = ComparisonStatus.PASS;
            result.EvaluationNotes = "No customizations required; Studio runs standard GCS core configuration.";
        }
        else
        {
            result.ComparisonStatus = ComparisonStatus.NEEDS_REVIEW;
            result.EvaluationNotes = $"Customization discrepancy: Ticket states '{exp}', Studio reported '{act}'. Verification by specialist required.";
        }
    }

    private static void EvaluateGeneralExactMatch(ParameterEvaluationInput input, ParameterCheckResult result)
    {
        var match = string.Equals(input.TicketExpectedValue.Trim(), input.StudioActualValue?.Trim(), StringComparison.OrdinalIgnoreCase);
        result.ComparisonStatus = match ? ComparisonStatus.PASS : ComparisonStatus.FAIL;
        result.EvaluationNotes = match
            ? "Exact match verified."
            : $"Mismatch: Ticket expected '{input.TicketExpectedValue}', Studio reported '{input.StudioActualValue}'.";
    }
}
