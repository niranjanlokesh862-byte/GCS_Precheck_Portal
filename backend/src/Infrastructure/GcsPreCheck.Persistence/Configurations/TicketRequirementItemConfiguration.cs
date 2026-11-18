using GcsPreCheck.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GcsPreCheck.Persistence.Configurations;

public class TicketRequirementItemConfiguration : IEntityTypeConfiguration<TicketRequirementItem>
{
    public void Configure(EntityTypeBuilder<TicketRequirementItem> builder)
    {
        builder.ToTable("TicketRequirementItems", "dbo");

        builder.HasKey(r => r.Id);

        builder.Property(r => r.ParameterCode)
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(r => r.ScopeQualifier)
            .HasMaxLength(100)
            .HasDefaultValue("DEFAULT")
            .IsRequired();

        builder.Property(r => r.ExtractedRawText)
            .HasMaxLength(500)
            .IsRequired(false);

        builder.Property(r => r.OcrConfidenceScore)
            .HasPrecision(5, 4)
            .IsRequired(false);

        builder.Property(r => r.EmployeeVerifiedValue)
            .HasMaxLength(500)
            .IsRequired(false);

        builder.Property(r => r.FieldVerificationStatus)
            .HasConversion<string>()
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(r => r.VerifiedAtUtc)
            .HasPrecision(3)
            .IsRequired(false);

        builder.Property(r => r.CorrectionReason)
            .HasMaxLength(500)
            .IsRequired(false);

        builder.HasOne(r => r.Ticket)
            .WithMany(t => t.RequirementItems)
            .HasForeignKey(r => r.TicketId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(r => r.VerifiedByUser)
            .WithMany()
            .HasForeignKey(r => r.VerifiedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(r => new { r.TicketId, r.ParameterCode, r.ScopeQualifier })
            .IsUnique();
    }
}
