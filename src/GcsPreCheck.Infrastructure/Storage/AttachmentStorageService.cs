using System.Buffers.Binary;
using System.Security.Cryptography;
using GcsPreCheck.Application.Common.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace GcsPreCheck.Infrastructure.Storage;

public class AttachmentStorageService : IAttachmentStorageService
{
    private static readonly byte[] PngMagic = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
    private static readonly byte[] JpegMagic = [0xFF, 0xD8, 0xFF];

    private const int MaxDimension = 8192;
    private const long MaxFileSizeBytes = 15 * 1024 * 1024; // 15 MB

    private readonly string _baseStoragePath;
    private readonly string _quarantinePath;
    private readonly string _verifiedPath;
    private readonly ILogger<AttachmentStorageService> _logger;

    public AttachmentStorageService(IConfiguration configuration, ILogger<AttachmentStorageService> logger)
    {
        _logger = logger;
        _baseStoragePath = configuration["Storage:BasePath"] ?? Path.Combine(AppContext.BaseDirectory, "Storage");
        _quarantinePath = Path.Combine(_baseStoragePath, "Quarantine");
        _verifiedPath = Path.Combine(_baseStoragePath, "VerifiedAttachments");

        Directory.CreateDirectory(_quarantinePath);
        Directory.CreateDirectory(_verifiedPath);
    }

    public AttachmentValidationResult ValidateImageHeader(byte[] buffer)
    {
        if (buffer == null || buffer.Length == 0)
        {
            return new AttachmentValidationResult(false, string.Empty, 0, 0, 0, string.Empty, "Buffer is empty or null.");
        }

        if (buffer.Length > MaxFileSizeBytes)
        {
            return new AttachmentValidationResult(false, string.Empty, 0, 0, buffer.Length, string.Empty, $"File exceeds maximum allowed size of {MaxFileSizeBytes / 1024 / 1024} MB.");
        }

        using var sha = SHA256.Create();
        var hashBytes = sha.ComputeHash(buffer);
        var sha256 = Convert.ToHexString(hashBytes);

        // 1. Check PNG
        if (buffer.Length >= 24 && buffer.AsSpan(0, PngMagic.Length).SequenceEqual(PngMagic))
        {
            // PNG IHDR chunk is immediately following 8-byte signature:
            // 4 bytes length, 4 bytes chunk type ("IHDR"), 4 bytes width, 4 bytes height
            var width = BinaryPrimitives.ReadInt32BigEndian(buffer.AsSpan(16, 4));
            var height = BinaryPrimitives.ReadInt32BigEndian(buffer.AsSpan(20, 4));

            if (width <= 0 || height <= 0 || width > MaxDimension || height > MaxDimension)
            {
                return new AttachmentValidationResult(false, "image/png", width, height, buffer.Length, sha256,
                    $"Invalid PNG dimensions: {width}x{height}. Must be between 1 and {MaxDimension} px.");
            }

            return new AttachmentValidationResult(true, "image/png", width, height, buffer.Length, sha256);
        }

        // 2. Check JPEG
        if (buffer.Length >= 4 && buffer.AsSpan(0, JpegMagic.Length).SequenceEqual(JpegMagic))
        {
            // Inspect JPEG SOF markers (SOF0 = 0xC0, SOF2 = 0xC2)
            var (width, height) = ParseJpegDimensions(buffer);
            if (width <= 0 || height <= 0 || width > MaxDimension || height > MaxDimension)
            {
                return new AttachmentValidationResult(false, "image/jpeg", width, height, buffer.Length, sha256,
                    $"Invalid or unreadable JPEG dimensions: {width}x{height}. Must be between 1 and {MaxDimension} px.");
            }

            return new AttachmentValidationResult(true, "image/jpeg", width, height, buffer.Length, sha256);
        }

        return new AttachmentValidationResult(false, string.Empty, 0, 0, buffer.Length, sha256,
            "File header failed binary magic byte inspection. Only valid PNG and JPEG images are permitted.");
    }

    public async Task<StoredAttachmentInfo> SaveToQuarantineAsync(byte[] buffer, string originalFileName, CancellationToken ct = default)
    {
        var validation = ValidateImageHeader(buffer);
        if (!validation.IsValid)
            throw new ArgumentException(validation.ErrorMessage);

        var extension = validation.MimeType == "image/png" ? ".png" : ".jpg";
        var safeFileName = $"{Guid.NewGuid():N}{extension}";
        var fullDiskPath = Path.Combine(_quarantinePath, safeFileName);

        await File.WriteAllBytesAsync(fullDiskPath, buffer, ct);

        return new StoredAttachmentInfo(
            StorageRelativePath: Path.Combine("Quarantine", safeFileName),
            FullDiskPath: fullDiskPath,
            Sha256Hash: validation.Sha256Hash,
            MimeType: validation.MimeType,
            FileSizeBytes: buffer.Length,
            Width: validation.Width,
            Height: validation.Height);
    }

    public async Task<string> PromoteFromQuarantineToVerifiedAsync(string quarantinePath, CancellationToken ct = default)
    {
        if (!File.Exists(quarantinePath))
            throw new FileNotFoundException($"Quarantine file '{quarantinePath}' not found.");

        var fileName = Path.GetFileName(quarantinePath);
        var dateFolder = DateTime.UtcNow.ToString("yyyy-MM-dd");
        var targetDir = Path.Combine(_verifiedPath, dateFolder);
        Directory.CreateDirectory(targetDir);

        var targetFullPath = Path.Combine(targetDir, fileName);
        File.Move(quarantinePath, targetFullPath, overwrite: true);

        await Task.CompletedTask;
        return Path.Combine("VerifiedAttachments", dateFolder, fileName);
    }

    public async Task DeleteQuarantineFileAsync(string quarantinePath)
    {
        if (File.Exists(quarantinePath))
        {
            try
            {
                File.Delete(quarantinePath);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to delete quarantine file '{Path}'", quarantinePath);
            }
        }
        await Task.CompletedTask;
    }

    public async Task<Stream?> OpenReadStreamAsync(string storageRelativePath, CancellationToken ct = default)
    {
        var fullPath = Path.Combine(_baseStoragePath, storageRelativePath);
        if (!File.Exists(fullPath))
            return null;

        await Task.CompletedTask;
        return File.OpenRead(fullPath);
    }

    private static (int Width, int Height) ParseJpegDimensions(byte[] buffer)
    {
        var index = 2; // Skip 0xFF, 0xD8
        while (index < buffer.Length - 8)
        {
            if (buffer[index] != 0xFF)
            {
                index++;
                continue;
            }

            var marker = buffer[index + 1];
            // SOF0 (0xC0), SOF1 (0xC1), SOF2 (0xC2)
            if (marker is 0xC0 or 0xC1 or 0xC2)
            {
                var height = BinaryPrimitives.ReadInt16BigEndian(buffer.AsSpan(index + 5, 2));
                var width = BinaryPrimitives.ReadInt16BigEndian(buffer.AsSpan(index + 7, 2));
                return (width, height);
            }

            // Variable-length marker length
            var length = BinaryPrimitives.ReadUInt16BigEndian(buffer.AsSpan(index + 2, 2));
            index += 2 + length;
        }

        return (0, 0);
    }
}
