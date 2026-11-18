using GcsPreCheck.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GcsPreCheck.Persistence.Configurations;

public class StudioNetworkAllowlistConfiguration : IEntityTypeConfiguration<StudioNetworkAllowlist>
{
    public void Configure(EntityTypeBuilder<StudioNetworkAllowlist> builder)
    {
        builder.ToTable("StudioNetworkAllowlists", "dbo");

        builder.HasKey(a => a.Id);

        builder.Property(a => a.EntryType)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(a => a.NetworkPattern)
            .HasMaxLength(255)
            .IsRequired();

        builder.Property(a => a.EnvironmentDescription)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(a => a.IsProduction)
            .HasDefaultValue(true);

        builder.Property(a => a.ApprovedAtUtc)
            .HasPrecision(3)
            .HasDefaultValueSql("SYSUTCDATETIME()");

        builder.Property(a => a.IsActive)
            .HasDefaultValue(true);

        builder.HasOne(a => a.ApprovedByUser)
            .WithMany()
            .HasForeignKey(a => a.ApprovedByUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
