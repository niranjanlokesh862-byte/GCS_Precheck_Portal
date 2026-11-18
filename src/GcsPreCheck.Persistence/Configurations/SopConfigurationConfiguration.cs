using GcsPreCheck.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GcsPreCheck.Persistence.Configurations;

public class SopConfigurationConfiguration : IEntityTypeConfiguration<SopConfiguration>
{
    public void Configure(EntityTypeBuilder<SopConfiguration> builder)
    {
        builder.ToTable("SopConfigurations", "dbo");

        builder.HasKey(s => s.Id);

        builder.Property(s => s.ParameterCode)
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(s => s.SopDocumentId)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(s => s.SopTitle)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(s => s.SopVersion)
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(s => s.SopDocumentUrl)
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(s => s.IsApproved)
            .HasDefaultValue(false);

        builder.Property(s => s.ApprovedAtUtc)
            .HasPrecision(3)
            .IsRequired(false);

        builder.Property(s => s.IsActive)
            .HasDefaultValue(false);

        builder.HasOne(s => s.ApprovedByUser)
            .WithMany()
            .HasForeignKey(s => s.ApprovedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(s => new { s.ParameterCode, s.SopVersion })
            .IsUnique();

        builder.HasIndex(s => s.ParameterCode)
            .HasFilter("[IsActive] = 1 AND [IsApproved] = 1")
            .IsUnique();
    }
}
