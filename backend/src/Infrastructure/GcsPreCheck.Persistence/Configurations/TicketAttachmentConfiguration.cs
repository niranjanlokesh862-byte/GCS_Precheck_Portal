using GcsPreCheck.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GcsPreCheck.Persistence.Configurations;

public class TicketAttachmentConfiguration : IEntityTypeConfiguration<TicketAttachment>
{
    public void Configure(EntityTypeBuilder<TicketAttachment> builder)
    {
        builder.ToTable("TicketAttachments", "dbo");

        builder.HasKey(a => a.Id);

        builder.Property(a => a.AttachmentType)
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(a => a.OriginalFileName)
            .HasMaxLength(255)
            .IsRequired();

        builder.Property(a => a.StorageRelativePath)
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(a => a.FileSizeBytes)
            .IsRequired();

        builder.Property(a => a.MimeType)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(a => a.Sha256Checksum)
            .HasMaxLength(64)
            .IsFixedLength()
            .IsRequired();

        builder.Property(a => a.AntivirusScanStatus)
            .HasConversion<string>()
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(a => a.OcrProcessingStatus)
            .HasConversion<string>()
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(a => a.UploadedAtUtc)
            .HasPrecision(3)
            .HasDefaultValueSql("SYSUTCDATETIME()");

        builder.HasOne(a => a.Ticket)
            .WithMany(t => t.Attachments)
            .HasForeignKey(a => a.TicketId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(a => a.UploadedByUser)
            .WithMany()
            .HasForeignKey(a => a.UploadedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(a => a.TicketId);
    }
}
