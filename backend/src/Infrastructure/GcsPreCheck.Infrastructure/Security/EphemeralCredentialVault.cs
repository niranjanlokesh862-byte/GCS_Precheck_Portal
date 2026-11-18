using System.Collections.Concurrent;
using System.Security.Cryptography;
using GcsPreCheck.Application.Common.Interfaces;

namespace GcsPreCheck.Infrastructure.Security;

public class EphemeralCredentialVault : IEphemeralCredentialVault, IDisposable
{
    private readonly ConcurrentDictionary<Guid, EphemeralSecretEnvelope> _cache = new();
    private readonly Timer _cleanupTimer;
    private bool _disposed;

    public EphemeralCredentialVault()
    {
        // Cleanup expired entries every 30 seconds
        _cleanupTimer = new Timer(CleanupExpiredEntries, null, TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(30));
    }

    public void StoreCredentials(Guid sessionNonce, string username, string password, TimeSpan? ttl = null)
    {
        var effectiveTtl = ttl ?? TimeSpan.FromSeconds(90);
        var buffer = password.ToCharArray();
        var envelope = new EphemeralSecretEnvelope(username, buffer, DateTime.UtcNow.Add(effectiveTtl));
        _cache[sessionNonce] = envelope;
    }

    public EphemeralSecretEnvelope? ClaimCredentials(Guid sessionNonce)
    {
        // Single-use eviction: claiming immediately removes the credentials from vault memory
        if (_cache.TryRemove(sessionNonce, out var envelope))
        {
            if (DateTime.UtcNow <= envelope.ExpiresAtUtc)
            {
                return envelope;
            }

            // Expired - zero-wipe buffer
            CryptographicOperations.ZeroMemory(System.Runtime.InteropServices.MemoryMarshal.AsBytes(envelope.PasswordBuffer.AsSpan()));
        }

        return null;
    }

    public void Invalidate(Guid sessionNonce)
    {
        if (_cache.TryRemove(sessionNonce, out var envelope))
        {
            CryptographicOperations.ZeroMemory(System.Runtime.InteropServices.MemoryMarshal.AsBytes(envelope.PasswordBuffer.AsSpan()));
        }
    }

    private void CleanupExpiredEntries(object? state)
    {
        var now = DateTime.UtcNow;
        foreach (var kvp in _cache)
        {
            if (kvp.Value.ExpiresAtUtc < now)
            {
                if (_cache.TryRemove(kvp.Key, out var expiredEnvelope))
                {
                    CryptographicOperations.ZeroMemory(System.Runtime.InteropServices.MemoryMarshal.AsBytes(expiredEnvelope.PasswordBuffer.AsSpan()));
                }
            }
        }
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _cleanupTimer.Dispose();
            foreach (var kvp in _cache)
            {
                CryptographicOperations.ZeroMemory(System.Runtime.InteropServices.MemoryMarshal.AsBytes(kvp.Value.PasswordBuffer.AsSpan()));
            }
            _cache.Clear();
            _disposed = true;
        }
        GC.SuppressFinalize(this);
    }
}
