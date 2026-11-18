namespace GcsPreCheck.Application.Common.Interfaces;

public record EphemeralSecretEnvelope(
    string Username,
    char[] PasswordBuffer,
    DateTime ExpiresAtUtc);

public interface IEphemeralCredentialVault
{
    void StoreCredentials(Guid sessionNonce, string username, string password, TimeSpan? ttl = null);
    EphemeralSecretEnvelope? ClaimCredentials(Guid sessionNonce);
    void Invalidate(Guid sessionNonce);
}
