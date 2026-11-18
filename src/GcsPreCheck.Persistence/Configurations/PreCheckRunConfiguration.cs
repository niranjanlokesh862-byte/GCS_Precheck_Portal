using GcsPreCheck.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GcsPreCheck.Persistence.Configurations;

public class PreCheckRunConfiguration : IEntityTypeConfiguration<PreCheckRun>
{
    public void Configure(EntityTypeBuilder<PreCheckRun> builder)
    {
        builder.ToTable("PreCheckRuns", "dbo");

        builder.HasKey(r => r.Id);

        builder.Property(r => r.JobId)
            .IsRequired();

        builder.HasIndex(r => r.JobId)
            .IsUnique();

        builder.HasOne(r => r.Job)
            .WithOne(q => q.Run)
            .HasForeignKey<PreCheckRun>(r => r.JobId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(r => r.Ticket)
            .WithMany(t => t.PreCheckRuns)
            .HasForeignKey(r => r.TicketId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(r => r.ExecutedByUser)
            .WithMany(u => u.ExecutedRuns)
            .HasForeignKey(r => r.ExecutedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Property(r => r.StudioUrl)
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(r => r.ResolvedStudioIp)
            .HasMaxLength(50)
            .IsRequired();

        builder.HasOne(r => r.MatchedAllowlist)
            .WithMany(a => a.PreCheckRuns)
            .HasForeignKey(r => r.MatchedAllowlistId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Property(r => r.TargetEnvironment)
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(r => r.ExecutionStatus)
            .HasConversion<string>()
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(r => r.OverallResult)
            .HasConversion<string>()
            .HasMaxLength(30)
            .IsRequired(false);

        builder.Property(r => r.EvidenceStorageDirectory)
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(r => r.PassedCount)
            .HasDefaultValue(0);

        builder.Property(r => r.FailedCount)
            .HasDefaultValue(0);

        builder.Property(r => r.NotVerifiedCount)
            .HasDefaultValue(0);

        builder.Property(r => r.NotApplicableCount)
            .HasDefaultValue(0);

        builder.Property(r => r.NeedsReviewCount)
            .HasDefaultValue(0);

        builder.Property(r => r.ExecutionDurationMs)
            .IsRequired(false);

        builder.Property(r => r.StartedAtUtc)
            .HasPrecision(3)
            .HasDefaultValueSql("SYSUTCDATETIME()");

        builder.Property(r => r.CompletedAtUtc)
            .HasPrecision(3)
            .IsRequired(false);

        builder.HasIndex(r => r.TicketId);
    }
}
