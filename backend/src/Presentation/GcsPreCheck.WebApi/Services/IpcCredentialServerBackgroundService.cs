using System.Runtime.Versioning;
using GcsPreCheck.Application.Common.Interfaces;
using GcsPreCheck.Infrastructure.Security;

namespace GcsPreCheck.WebApi.Services;

[SupportedOSPlatform("windows")]
public class IpcCredentialServerBackgroundService : BackgroundService
{
    private readonly INamedPipeServerService _pipeServer;
    private readonly IEphemeralCredentialVault _vault;
    private readonly IConfiguration _configuration;
    private readonly ILogger<IpcCredentialServerBackgroundService> _logger;

    public IpcCredentialServerBackgroundService(
        INamedPipeServerService pipeServer,
        IEphemeralCredentialVault vault,
        IConfiguration configuration,
        ILogger<IpcCredentialServerBackgroundService> logger)
    {
        _pipeServer = pipeServer;
        _vault = vault;
        _configuration = configuration;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var pipeName = _configuration["Ipc:PipeName"] ?? "GcsPreCheck_Ipc";
        var expectedWorkerAccount = _configuration["Ipc:WorkerAccount"] ?? string.Empty;

        _logger.LogInformation("IPC Ephemeral Credential Server started on pipe '{PipeName}' (Expected Worker: '{WorkerAccount}')",
            pipeName, string.IsNullOrWhiteSpace(expectedWorkerAccount) ? "Current Process Identity" : expectedWorkerAccount);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await _pipeServer.ServeCredentialRequestAsync(
                    pipeName,
                    expectedWorkerAccount,
                    nonce => _vault.ClaimCredentials(nonce),
                    stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "IPC Credential Server error handling connection. Re-binding listener...");
                await Task.Delay(500, stoppingToken);
            }
        }

        _logger.LogInformation("IPC Ephemeral Credential Server stopped.");
    }
}
