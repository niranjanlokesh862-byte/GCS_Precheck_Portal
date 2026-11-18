using GcsPreCheck.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GcsPreCheck.Persistence.Configurations;

public class ParameterCheckResultConfiguration : IEntityTypeConfiguration<ParameterCheckResult>
{
    public void Configure(EntityTypeBuilder<ParameterCheckResult> builder)
    {
        builder.ToTable("ParameterCheckResults", "dbo");

        builder.HasKey(p => p.Id);

        builder.Property(p => p.ParameterCode)
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(p => p.ParameterName)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(p => p.TicketExpectedValue)
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(p => p.StudioActualValue)
            .HasMaxLength(500)
            .IsRequired(false);

        builder.Property(p => p.ComparisonStatus)
            .HasConversion<string>()
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(p => p.IsConditional)
            .HasDefaultValue(false);

        builder.Property(p => p.ScopeRuleApplied)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(p => p.EvidenceScreenshotPath)
            .HasMaxLength(500)
            .IsRequired(false);

        builder.Property(p => p.DomSnapshotHash)
            .HasMaxLength(64)
            .IsFixedLength()
            .IsRequired(false);

        builder.Property(p => p.EvaluatedAtUtc)
            .HasPrecision(3)
            .HasDefaultValueSql("SYSUTCDATETIME()");

        builder.HasOne(p => p.Run)
            .WithMany(r => r.ParameterResults)
            .HasForeignKey(p => p.RunId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(p => p.Sop)
            .WithMany(s => s.ResultsUsingThisSop)
            .HasForeignKey(p => p.SopId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(p => new { p.RunId, p.ParameterCode });
    }
}
