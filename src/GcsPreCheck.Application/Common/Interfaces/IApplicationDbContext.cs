using GcsPreCheck.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace GcsPreCheck.Application.Common.Interfaces;

public interface IApplicationDbContext
{
    DbSet<ApplicationUser> ApplicationUsers { get; }
    DbSet<CustomerTicket> CustomerTickets { get; }
    DbSet<TicketAttachment> TicketAttachments { get; }
    DbSet<TicketRequirementItem> TicketRequirementItems { get; }
    DbSet<SopConfiguration> SopConfigurations { get; }
    DbSet<StudioNetworkAllowlist> StudioNetworkAllowlists { get; }
    DbSet<PreCheckJobQueue> PreCheckJobQueue { get; }
    DbSet<PreCheckRun> PreCheckRuns { get; }
    DbSet<PreCheckMfaChallenge> PreCheckMfaChallenges { get; }
    DbSet<ParameterCheckResult> ParameterCheckResults { get; }
    DbSet<AuditLog> AuditLogs { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
