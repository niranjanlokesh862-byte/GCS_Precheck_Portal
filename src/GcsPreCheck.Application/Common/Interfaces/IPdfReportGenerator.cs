namespace GcsPreCheck.Application.Common.Interfaces;

public record ParameterCheckResultDto(
    string ParameterCode,
    string ParameterName,
    string TicketExpectedValue,
    string? StudioActualValue,
    string ComparisonStatus,
    string ScopeRuleApplied,
    string? EvaluationNotes);

public record PreCheckReportDto(
    Guid TicketId,
    string CustomerName,
    string CustomerClassification,
    Guid RunId,
    string StudioUrl,
    string TargetEnvironment,
    string WorkerId,
    DateTime ExecutedAtUtc,
    string OverallResult,
    List<ParameterCheckResultDto> ParameterResults,
    string VerifiedByUserName,
    DateTime VerifiedAtUtc,
    string AntivirusScanAttestation = "Defender Antivirus: Clean",
    string SsrNetworkAttestation = "SSRF Whitelist Pass: IT-Approved IP/CIDR");

public interface IPdfReportGenerator
{
    Task<byte[]> GeneratePreCheckReportAsync(PreCheckReportDto reportData, CancellationToken ct = default);
}
