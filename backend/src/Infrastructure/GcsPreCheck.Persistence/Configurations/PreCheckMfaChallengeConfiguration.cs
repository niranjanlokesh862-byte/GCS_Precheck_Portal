using GcsPreCheck.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GcsPreCheck.Persistence.Configurations;

public class PreCheckMfaChallengeConfiguration : IEntityTypeConfiguration<PreCheckMfaChallenge>
{
    public void Configure(EntityTypeBuilder<PreCheckMfaChallenge> builder)
    {
        builder.ToTable("PreCheckMfaChallenges", "dbo");

        builder.HasKey(c => c.Id);

        builder.Property(c => c.ChallengeType)
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(c => c.SubmissionAttempts)
            .HasDefaultValue(0);

        builder.Property(c => c.IsConsumed)
            .HasDefaultValue(false);

        builder.Property(c => c.ExpiresAtUtc)
            .HasPrecision(3)
            .IsRequired();

        builder.Property(c => c.CreatedAtUtc)
            .HasPrecision(3)
            .HasDefaultValueSql("SYSUTCDATETIME()");

        builder.HasOne(c => c.Job)
            .WithMany(q => q.MfaChallenges)
            .HasForeignKey(c => c.JobId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(c => c.Run)
            .WithMany(r => r.MfaChallenges)
            .HasForeignKey(c => c.RunId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(c => new { c.JobId, c.IsConsumed, c.ExpiresAtUtc });
    }
}
