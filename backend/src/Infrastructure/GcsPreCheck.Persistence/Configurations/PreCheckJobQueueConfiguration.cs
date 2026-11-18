using GcsPreCheck.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GcsPreCheck.Persistence.Configurations;

public class PreCheckJobQueueConfiguration : IEntityTypeConfiguration<PreCheckJobQueue>
{
    public void Configure(EntityTypeBuilder<PreCheckJobQueue> builder)
    {
        builder.ToTable("PreCheckJobQueue", "dbo");

        builder.HasKey(q => q.Id);

        builder.Property(q => q.StudioUrl)
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(q => q.SessionNonce)
            .IsRequired();

        builder.Property(q => q.ExecutionStatus)
            .HasConversion<string>()
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(q => q.Priority)
            .HasDefaultValue(10);

        builder.Property(q => q.QueuedAtUtc)
            .HasPrecision(3)
            .HasDefaultValueSql("SYSUTCDATETIME()");

        builder.Property(q => q.ScheduledAtUtc)
            .HasPrecision(3)
            .HasDefaultValueSql("SYSUTCDATETIME()");

        builder.Property(q => q.LeasedByWorkerId)
            .HasMaxLength(100)
            .IsRequired(false);

        builder.Property(q => q.LeaseExpiresAtUtc)
            .HasPrecision(3)
            .IsRequired(false);

        builder.Property(q => q.HeartbeatAtUtc)
            .HasPrecision(3)
            .IsRequired(false);

        builder.Property(q => q.CompletedAtUtc)
            .HasPrecision(3)
            .IsRequired(false);

        builder.HasOne(q => q.Ticket)
            .WithMany(t => t.JobQueueEntries)
            .HasForeignKey(q => q.TicketId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(q => new { q.ExecutionStatus, q.ScheduledAtUtc })
            .IncludeProperties(q => new { q.Priority, q.QueuedAtUtc });
    }
}
