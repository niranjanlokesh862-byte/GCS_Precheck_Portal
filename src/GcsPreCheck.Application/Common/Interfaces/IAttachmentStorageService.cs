namespace GcsPreCheck.Application.Common.Interfaces;

public record AttachmentValidationResult(
    bool IsValid,
    string MimeType,
    int Width,
    int Height,
    long FileSizeBytes,
    string Sha256Hash,
    string? ErrorMessage = null);

public record StoredAttachmentInfo(
    string StorageRelativePath,
    string FullDiskPath,
    string Sha256Hash,
    string MimeType,
    long FileSizeBytes,
    int Width,
    int Height);

public interface IAttachmentStorageService
{
    AttachmentValidationResult ValidateImageHeader(byte[] buffer);
    Task<StoredAttachmentInfo> SaveToQuarantineAsync(byte[] buffer, string originalFileName, CancellationToken ct = default);
    Task<string> PromoteFromQuarantineToVerifiedAsync(string quarantinePath, CancellationToken ct = default);
    Task DeleteQuarantineFileAsync(string quarantinePath);
    Task<Stream?> OpenReadStreamAsync(string storageRelativePath, CancellationToken ct = default);
}
