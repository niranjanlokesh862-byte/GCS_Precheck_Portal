using System.Diagnostics;
using GcsPreCheck.Application.Common.Interfaces;
using GcsPreCheck.Domain.Enums;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace GcsPreCheck.Infrastructure.Security;

public class AntivirusScanService : IAntivirusScanner
{
    private readonly string _scannerMode; // "EnterpriseDefender", "MockAlwaysClean", "MockSimulateInfection"
    private readonly string _defenderPath;
    private readonly ILogger<AntivirusScanService> _logger;

    public AntivirusScanService(IConfiguration configuration, ILogger<AntivirusScanService> logger)
    {
        _logger = logger;
        _scannerMode = configuration["Antivirus:Mode"] ?? "EnterpriseDefender";
        _defenderPath = configuration["Antivirus:DefenderPath"] 
            ?? @"C:\Program Files\Windows Defender\MpCmdRun.exe";
    }

    public async Task<AntivirusScanResult> ScanFileAsync(string filePath, CancellationToken ct = default)
    {
        if (_scannerMode == "MockAlwaysClean")
        {
            _logger.LogInformation("Antivirus [MOCK]: File '{FilePath}' marked clean by development mock.", filePath);
            return new AntivirusScanResult(true, AntivirusScanStatus.Clean, "MockScannerEngine");
        }

        if (_scannerMode == "MockSimulateInfection")
        {
            _logger.LogWarning("Antivirus [MOCK]: Simulating infected detection for '{FilePath}'.", filePath);
            return new AntivirusScanResult(false, AntivirusScanStatus.Infected, "MockScannerEngine", "EICAR_Test_Payload");
        }

        // Enterprise Windows Defender CLI Execution
        if (!File.Exists(_defenderPath))
        {
            _logger.LogError("FAIL-CLOSED: Antivirus scanner binary '{Path}' does not exist.", _defenderPath);
            return new AntivirusScanResult(false, AntivirusScanStatus.ScanFailed, "WindowsDefenderCli",
                FailureReason: "SCAN_SERVICE_UNAVAILABLE: Antivirus scanner engine binary not found.");
        }

        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = _defenderPath,
                Arguments = $"-Scan -ScanType 3 -File \"{filePath}\" -DisableRemediation",
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            using var process = new Process { StartInfo = psi };
            process.Start();

            await process.WaitForExitAsync(ct);

            // MpCmdRun.exe Return Codes:
            // 0: No threat detected (Clean)
            // 2: Threat detected (Infected)
            // Other: Scan failed / error
            if (process.ExitCode == 0)
            {
                return new AntivirusScanResult(true, AntivirusScanStatus.Clean, "MicrosoftDefender");
            }

            if (process.ExitCode == 2)
            {
                var stdout = await process.StandardOutput.ReadToEndAsync(ct);
                _logger.LogWarning("Threat detected by Microsoft Defender: {Output}", stdout);
                return new AntivirusScanResult(false, AntivirusScanStatus.Infected, "MicrosoftDefender", "TrojanOrMalwareDetected");
            }

            var stderr = await process.StandardError.ReadToEndAsync(ct);
            return new AntivirusScanResult(false, AntivirusScanStatus.ScanFailed, "MicrosoftDefender",
                FailureReason: $"Defender scanner exited with error code {process.ExitCode}: {stderr}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "FAIL-CLOSED: Unexpected exception while invoking antivirus engine.");
            return new AntivirusScanResult(false, AntivirusScanStatus.ScanFailed, "MicrosoftDefender",
                FailureReason: $"SCAN_SERVICE_UNAVAILABLE: Exception invoking engine - {ex.Message}");
        }
    }

    public async Task<AntivirusScanResult> ScanBufferAsync(byte[] buffer, string fileName, CancellationToken ct = default)
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}_{fileName}");
        try
        {
            await File.WriteAllBytesAsync(tempFile, buffer, ct);
            return await ScanFileAsync(tempFile, ct);
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                try { File.Delete(tempFile); } catch { /* best effort cleanup */ }
            }
        }
    }
}
