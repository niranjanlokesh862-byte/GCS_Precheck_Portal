using GcsPreCheck.Application.Common.Interfaces;
using GcsPreCheck.Domain.Enums;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace GcsPreCheck.WebApi.Hubs;

public interface IPreCheckClient
{
    Task OnLogEvent(string message, string level, DateTime timestamp);
    Task OnProgressUpdate(int percent, string currentStep);
    Task OnMfaRequired(Guid challengeId, string challengeType, DateTime expiresAtUtc);
    Task OnExecutionCompleted(Guid runId, string overallResult);
    Task OnExecutionFailed(string errorMessage);
}

public class PreCheckHub : Hub<IPreCheckClient>
{
    private readonly IApplicationDbContext _context;
    private readonly ILogger<PreCheckHub> _logger;

    public PreCheckHub(IApplicationDbContext context, ILogger<PreCheckHub> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task JoinJobGroup(string jobId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, $"job_{jobId}");
        _logger.LogInformation("Client {ConnectionId} subscribed to job group job_{JobId}", Context.ConnectionId, jobId);
    }

    public async Task SubmitMfaOtp(string challengeIdStr, string otpCode)
    {
        if (!Guid.TryParse(challengeIdStr, out var challengeId) || string.IsNullOrWhiteSpace(otpCode))
        {
            throw new HubException("Invalid MFA challenge ID or empty OTP code.");
        }

        var challenge = await _context.PreCheckMfaChallenges
            .Include(c => c.Job)
            .FirstOrDefaultAsync(c => c.Id == challengeId);

        if (challenge == null)
        {
            throw new HubException("MFA Challenge not found.");
        }

        if (challenge.IsConsumed)
        {
            throw new HubException("MFA Challenge has already been consumed.");
        }

        if (DateTime.UtcNow > challenge.ExpiresAtUtc)
        {
            throw new HubException("MFA Challenge has expired. Run must be restarted.");
        }

        challenge.SubmissionAttempts++;
        if (challenge.SubmissionAttempts > 3)
        {
            challenge.IsConsumed = true;
            if (challenge.Job != null)
            {
                challenge.Job.ExecutionStatus = ExecutionStatus.FAILED;
                challenge.Job.ErrorMessage = "MAX_MFA_ATTEMPTS_EXCEEDED";
            }
            await _context.SaveChangesAsync();
            await Clients.Group($"job_{challenge.JobId}").OnExecutionFailed("Maximum MFA submission attempts (3) exceeded.");
            throw new HubException("Max MFA attempts exceeded. Automation terminated.");
        }

        await _context.SaveChangesAsync();

        // Broadcast OTP to worker listener group
        await Clients.Group($"worker_job_{challenge.JobId}").OnLogEvent(otpCode, "MFA_OTP_SUBMITTED", DateTime.UtcNow);
        _logger.LogInformation("MFA OTP received for challenge {ChallengeId} (Attempt {Attempt}/3)", challengeId, challenge.SubmissionAttempts);
    }
}
