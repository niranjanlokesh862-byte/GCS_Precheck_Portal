using GcsPreCheck.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GcsPreCheck.Persistence.Configurations;

public class CustomerTicketConfiguration : IEntityTypeConfiguration<CustomerTicket>
{
    public void Configure(EntityTypeBuilder<CustomerTicket> builder)
    {
        builder.ToTable("CustomerTickets", "dbo");

        builder.HasKey(t => t.Id);

        builder.Property(t => t.TicketNumber)
            .HasMaxLength(100)
            .IsRequired();

        builder.HasIndex(t => t.TicketNumber)
            .IsUnique();

        builder.Property(t => t.CustomerName)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(t => t.CustomerClassification)
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(t => t.PmRoutingStatus)
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(t => t.PmApprovedAtUtc)
            .HasPrecision(3)
            .IsRequired(false);

        builder.Property(t => t.PmApprovalNotes)
            .HasMaxLength(1000)
            .IsRequired(false);

        builder.Property(t => t.OcrOverallConfidence)
            .HasPrecision(5, 4)
            .IsRequired(false);

        builder.Property(t => t.VerificationStatus)
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(t => t.VerifiedByUserId)
            .IsRequired(false);

        builder.Property(t => t.VerifiedAtUtc)
            .HasPrecision(3)
            .IsRequired(false);

        builder.Property(t => t.CreatedAtUtc)
            .HasPrecision(3)
            .HasDefaultValueSql("SYSUTCDATETIME()");

        builder.HasOne(t => t.PmApprovedByUser)
            .WithMany()
            .HasForeignKey(t => t.PmApprovedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(t => t.VerifiedByUser)
            .WithMany()
            .HasForeignKey(t => t.VerifiedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(t => t.CreatedByUser)
            .WithMany(u => u.CreatedTickets)
            .HasForeignKey(t => t.CreatedByUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
