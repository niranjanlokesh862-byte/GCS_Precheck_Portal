using System.Security;
using System.Security.Cryptography;
using GcsPreCheck.Application.Common.Interfaces;
using GcsPreCheck.Domain.Entities;
using GcsPreCheck.Domain.Enums;
using GcsPreCheck.Infrastructure.NetworkSecurity;
using GcsPreCheck.Infrastructure.Security;
using GcsPreCheck.Worker.Automation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace GcsPreCheck.Worker.Services;

public class PreCheckWorkerService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILocalStudioForwardProxy _forwardProxy;
    private readonly INamedPipeClientService _ipcClient;
    private readonly IConfiguration _configuration;
    private readonly ILogger<PreCheckWorkerService> _logger;

    public PreCheckWorkerService(
        IServiceProvider serviceProvider,
        ILocalStudioForwardProxy forwardProxy,
        INamedPipeClientService ipcClient,
        IConfiguration configuration,
        ILogger<PreCheckWorkerService> logger)
    {
        _serviceProvider = serviceProvider;
        _forwardProxy = forwardProxy;
        _ipcClient = ipcClient;
        _configuration = configuration;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("GcsPreCheck.Worker started. Initializing Local Forward Proxy...");
        _forwardProxy.Start();

        var rawStorage = _configuration["Storage:BasePath"];
        var storageBase = string.IsNullOrWhiteSpace(rawStorage)
            ? Path.Combine(AppContext.BaseDirectory, "Storage")
            : (Path.IsPathRooted(rawStorage) ? rawStorage : Path.Combine(AppContext.BaseDirectory, rawStorage));
        var pipeName = _configuration["Ipc:PipeName"] ?? "GcsPreCheck_Ipc";
        var serverAccount = _configuration["Ipc:ServerAccount"] ?? string.Empty;
        var serverSpn = _configuration["Ipc:ServerSpn"] ?? string.Empty;

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var context = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
                var automationEngine = scope.ServiceProvider.GetRequiredService<IStudioAutomationEngine>();
                var networkValidator = scope.ServiceProvider.GetRequiredService<IStudioNetworkValidator>();

                // 1. Transactional Lease: Find next queued job
                var job = await context.PreCheckJobQueue
                    .Include(q => q.Ticket)
                    .Where(q => q.ExecutionStatus == ExecutionStatus.QUEUED && q.ScheduledAtUtc <= DateTime.UtcNow)
                    .OrderByDescending(q => q.Priority)
                    .ThenBy(q => q.QueuedAtUtc)
                    .FirstOrDefaultAsync(stoppingToken);

                if (job == null)
                {
                    // No work - backoff for 3 seconds
                    await Task.Delay(3000, stoppingToken);
                    continue;
                }

                _logger.LogInformation("Leasing PreCheck job {JobId} for ticket {TicketId}", job.Id, job.TicketId);

                // Destination SSRF Validation
                if (!Uri.TryCreate(job.StudioUrl, UriKind.Absolute, out var targetUri))
                {
                    job.ExecutionStatus = ExecutionStatus.FAILED;
                    job.ErrorMessage = "Malformed Studio target URI.";
                    await context.SaveChangesAsync(stoppingToken);
                    continue;
                }

                var netCheck = await networkValidator.ValidateDestinationAsync(targetUri, TargetEnvironment.Production, stoppingToken);
                if (!netCheck.IsAllowed)
                {
                    job.ExecutionStatus = ExecutionStatus.FAILED;
                    job.ErrorMessage = $"SSRF_VALIDATION_FAILED: {netCheck.FailureReason}";
                    await context.SaveChangesAsync(stoppingToken);
                    continue;
                }

                // Update Lease
                job.ExecutionStatus = ExecutionStatus.RUNNING;
                job.LeasedByWorkerId = Environment.MachineName;
                job.LeaseExpiresAtUtc = DateTime.UtcNow.AddSeconds(120);
                job.HeartbeatAtUtc = DateTime.UtcNow;

                var evidenceDir = Path.Combine(storageBase, "Evidence", job.Id.ToString("N"));

                // Create PreCheckRun instance
                var run = new PreCheckRun
                {
                    JobId = job.Id,
                    TicketId = job.TicketId,
                    ExecutedByUserId = job.Ticket?.CreatedByUserId ?? Guid.Empty,
                    StudioUrl = job.StudioUrl,
                    ResolvedStudioIp = netCheck.ResolvedIp?.ToString() ?? "Unknown",
                    MatchedAllowlistId = netCheck.MatchedAllowlistId ?? Guid.Empty,
                    TargetEnvironment = TargetEnvironment.Production,
                    ExecutionStatus = ExecutionStatus.RUNNING,
                    EvidenceStorageDirectory = evidenceDir,
                    StartedAtUtc = DateTime.UtcNow
                };

                context.PreCheckRuns.Add(run);
                await context.SaveChangesAsync(stoppingToken);

                // 2. Claim Credentials via Authenticated Named Pipe IPC
                EphemeralClaimResult claim;
                try
                {
                    _logger.LogInformation("Claiming ephemeral credentials via IPC for job {JobId} (Nonce: {Nonce})", job.Id, job.SessionNonce);
                    claim = await _ipcClient.RequestCredentialsAsync(
                        pipeName,
                        serverSpn,
                        serverAccount,
                        job.SessionNonce,
                        stoppingToken);

                    if (string.IsNullOrWhiteSpace(claim.Username) || claim.PasswordBuffer.Length == 0)
                    {
                        throw new SecurityException("IPC claimed credential envelope is empty or unpopulated.");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to claim ephemeral credentials via IPC. Job {JobId} transitioning to EXPIRED.", job.Id);
                    job.ExecutionStatus = ExecutionStatus.EXPIRED;
                    job.ErrorMessage = $"CREDENTIAL_CLAIM_FAILED: {ex.Message}";
                    run.ExecutionStatus = ExecutionStatus.EXPIRED;
                    run.CompletedAtUtc = DateTime.UtcNow;
                    await context.SaveChangesAsync(stoppingToken);
                    continue;
                }

                // 3. Execute Studio Inspection
                try
                {
                    var result = await automationEngine.ExecuteInspectionAsync(
                        job.StudioUrl,
                        claim.Username,
                        claim.PasswordBuffer,
                        run,
                        async (challengeType) =>
                        {
                            var challenge = new PreCheckMfaChallenge
                            {
                                JobId = job.Id,
                                RunId = run.Id,
                                ChallengeType = challengeType,
                                ExpiresAtUtc = DateTime.UtcNow.AddSeconds(180)
                            };
                            context.PreCheckMfaChallenges.Add(challenge);
                            job.ExecutionStatus = ExecutionStatus.WAITING_FOR_MFA;
                            run.ExecutionStatus = ExecutionStatus.WAITING_FOR_MFA;
                            await context.SaveChangesAsync(stoppingToken);

                            _logger.LogInformation("Waiting for employee OTP injection for job {JobId} (Challenge {ChallengeId})...", job.Id, challenge.Id);

                            var deadline = DateTime.UtcNow.AddSeconds(180);
                            while (DateTime.UtcNow < deadline && !stoppingToken.IsCancellationRequested)
                            {
                                var refreshed = await context.PreCheckMfaChallenges.FindAsync(new object[] { challenge.Id }, stoppingToken);
                                if (refreshed != null && refreshed.IsConsumed)
                                {
                                    job.ExecutionStatus = ExecutionStatus.RUNNING;
                                    run.ExecutionStatus = ExecutionStatus.RUNNING;
                                    await context.SaveChangesAsync(stoppingToken);
                                    return "MFA_VERIFIED";
                                }
                                await Task.Delay(2000, stoppingToken);
                            }

                            throw new TimeoutException("MFA Challenge timed out after 180 seconds without employee OTP injection.");
                        },
                        stoppingToken);

                    // 4. Record Results
                    run.OverallResult = result.OverallResult;
                    run.ExecutionStatus = ExecutionStatus.COMPLETED;
                    run.CompletedAtUtc = DateTime.UtcNow;
                    run.PassedCount = result.ParameterResults.Count(p => p.ComparisonStatus == ComparisonStatus.PASS);
                    run.FailedCount = result.ParameterResults.Count(p => p.ComparisonStatus == ComparisonStatus.FAIL);
                    run.NotVerifiedCount = result.ParameterResults.Count(p => p.ComparisonStatus == ComparisonStatus.NOT_VERIFIED);
                    run.NotApplicableCount = result.ParameterResults.Count(p => p.ComparisonStatus == ComparisonStatus.NOT_APPLICABLE);
                    run.NeedsReviewCount = result.ParameterResults.Count(p => p.ComparisonStatus == ComparisonStatus.NEEDS_REVIEW);

                    foreach (var param in result.ParameterResults)
                    {
                        context.ParameterCheckResults.Add(param);
                    }

                    job.ExecutionStatus = ExecutionStatus.COMPLETED;
                    job.CompletedAtUtc = DateTime.UtcNow;

                    await context.SaveChangesAsync(stoppingToken);
                    _logger.LogInformation("Job {JobId} completed with Overall Result: {OverallResult}", job.Id, result.OverallResult);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Studio automation failed for job {JobId}", job.Id);
                    job.ExecutionStatus = ExecutionStatus.FAILED;
                    job.ErrorMessage = ex.Message;
                    run.ExecutionStatus = ExecutionStatus.FAILED;
                    run.CompletedAtUtc = DateTime.UtcNow;
                    await context.SaveChangesAsync(stoppingToken);
                }
                finally
                {
                    CryptographicOperations.ZeroMemory(System.Runtime.InteropServices.MemoryMarshal.AsBytes(claim.PasswordBuffer.AsSpan()));
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Error in worker execution loop.");
                await Task.Delay(5000, stoppingToken);
            }
        }

        _forwardProxy.Stop();
    }
}
