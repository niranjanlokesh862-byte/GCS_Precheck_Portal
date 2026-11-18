using GcsPreCheck.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GcsPreCheck.Persistence.Configurations;

public class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> builder)
    {
        builder.ToTable("AuditLogs", "dbo");

        builder.HasKey(a => a.Id);

        builder.Property(a => a.UserId)
            .IsRequired(false);

        builder.HasOne(a => a.User)
            .WithMany(u => u.AuditLogs)
            .HasForeignKey(a => a.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Property(a => a.Action)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(a => a.EntityName)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(a => a.EntityId)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(a => a.AuditPayloadJson)
            .IsRequired(false);

        builder.Property(a => a.IpAddress)
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(a => a.TimestampUtc)
            .HasPrecision(3)
            .HasDefaultValueSql("SYSUTCDATETIME()");
    }
}
