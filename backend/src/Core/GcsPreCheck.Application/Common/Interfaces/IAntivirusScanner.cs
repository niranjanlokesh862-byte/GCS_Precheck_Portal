using GcsPreCheck.Domain.Enums;

namespace GcsPreCheck.Application.Common.Interfaces;

public record AntivirusScanResult(
    bool IsClean,
    AntivirusScanStatus Status,
    string EngineName,
    string? ThreatName = null,
    string? FailureReason = null);

public interface IAntivirusScanner
{
    Task<AntivirusScanResult> ScanFileAsync(string filePath, CancellationToken ct = default);
    Task<AntivirusScanResult> ScanBufferAsync(byte[] buffer, string fileName, CancellationToken ct = default);
}
