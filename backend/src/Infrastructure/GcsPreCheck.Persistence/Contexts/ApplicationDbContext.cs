using GcsPreCheck.Application.Common.Interfaces;
using GcsPreCheck.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using System.Reflection;

namespace GcsPreCheck.Persistence.Contexts;

public class ApplicationDbContext : DbContext, IApplicationDbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<ApplicationUser> ApplicationUsers => Set<ApplicationUser>();
    public DbSet<CustomerTicket> CustomerTickets => Set<CustomerTicket>();
    public DbSet<TicketAttachment> TicketAttachments => Set<TicketAttachment>();
    public DbSet<TicketRequirementItem> TicketRequirementItems => Set<TicketRequirementItem>();
    public DbSet<SopConfiguration> SopConfigurations => Set<SopConfiguration>();
    public DbSet<StudioNetworkAllowlist> StudioNetworkAllowlists => Set<StudioNetworkAllowlist>();
    public DbSet<PreCheckJobQueue> PreCheckJobQueue => Set<PreCheckJobQueue>();
    public DbSet<PreCheckRun> PreCheckRuns => Set<PreCheckRun>();
    public DbSet<PreCheckMfaChallenge> PreCheckMfaChallenges => Set<PreCheckMfaChallenge>();
    public DbSet<ParameterCheckResult> ParameterCheckResults => Set<ParameterCheckResult>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());
    }
}
