using System.Net;
using System.Net.Sockets;
using GcsPreCheck.Application.Common.Interfaces;
using GcsPreCheck.Domain.Entities;
using GcsPreCheck.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace GcsPreCheck.Infrastructure.NetworkSecurity;

public interface IStudioNetworkValidator
{
    Task<NetworkValidationResult> ValidateDestinationAsync(
        Uri targetUri,
        TargetEnvironment environment,
        CancellationToken ct = default);
}

public record NetworkValidationResult(
    bool IsAllowed,
    IPAddress? ResolvedIp,
    Guid? MatchedAllowlistId,
    string? FailureReason = null);

public class StudioNetworkValidator : IStudioNetworkValidator
{
    private static readonly IPAddress CloudMetadataIp = IPAddress.Parse("169.254.169.254");

    private readonly IApplicationDbContext _context;
    private readonly ILogger<StudioNetworkValidator> _logger;

    public StudioNetworkValidator(IApplicationDbContext context, ILogger<StudioNetworkValidator> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<NetworkValidationResult> ValidateDestinationAsync(
        Uri targetUri,
        TargetEnvironment environment,
        CancellationToken ct = default)
    {
        if (targetUri == null)
            return new NetworkValidationResult(false, null, null, "Target URI cannot be null.");

        // 1. Enforce HTTPS in Production & Staging
        if (environment != TargetEnvironment.IsolatedLabMock)
        {
            if (!string.Equals(targetUri.Scheme, "https", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogWarning("SSRF Violation: Target URI '{Uri}' uses unencrypted scheme '{Scheme}'. Production and Staging strictly require HTTPS.", targetUri, targetUri.Scheme);
                return new NetworkValidationResult(false, null, null, "Production and Staging targets strictly require HTTPS.");
            }
        }

        // 2. Resolve DNS Hostname
        IPAddress[] resolvedIps;
        try
        {
            resolvedIps = await Dns.GetHostAddressesAsync(targetUri.DnsSafeHost, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to resolve hostname '{Host}'", targetUri.DnsSafeHost);
            return new NetworkValidationResult(false, null, null, $"DNS resolution failed for '{targetUri.DnsSafeHost}': {ex.Message}");
        }

        if (resolvedIps.Length == 0)
        {
            return new NetworkValidationResult(false, null, null, $"No IP addresses resolved for '{targetUri.DnsSafeHost}'.");
        }

        // Check primary IPv4 address
        var primaryIp = resolvedIps.FirstOrDefault(ip => ip.AddressFamily == AddressFamily.InterNetwork) ?? resolvedIps[0];

        // 3. Reject Loopback
        if (IPAddress.IsLoopback(primaryIp))
        {
            _logger.LogWarning("SSRF Violation: Target IP '{Ip}' is a loopback address.", primaryIp);
            return new NetworkValidationResult(false, primaryIp, null, "Loopback destinations are strictly forbidden.");
        }

        // 4. Reject Cloud Metadata (169.254.169.254)
        if (primaryIp.Equals(CloudMetadataIp))
        {
            _logger.LogWarning("SSRF Violation: Target IP '{Ip}' matches cloud metadata IP.", primaryIp);
            return new NetworkValidationResult(false, primaryIp, null, "Cloud instance metadata destinations are strictly forbidden.");
        }

        // 5. Query Active IT Allowlists
        var allowlists = await _context.StudioNetworkAllowlists
            .AsNoTracking()
            .Where(a => a.IsActive)
            .ToListAsync(ct);

        foreach (var entry in allowlists)
        {
            if (MatchesAllowlistEntry(targetUri, primaryIp, entry))
            {
                _logger.LogInformation("Network destination '{Uri}' ({Ip}) matched active allowlist entry '{Pattern}' ({Desc}).",
                    targetUri, primaryIp, entry.NetworkPattern, entry.EnvironmentDescription);
                return new NetworkValidationResult(true, primaryIp, entry.Id);
            }
        }

        _logger.LogWarning("SSRF Violation: Target '{Uri}' ({Ip}) does not match any active IT-approved Studio allowlist record.", targetUri, primaryIp);
        return new NetworkValidationResult(false, primaryIp, null, $"Destination '{targetUri.DnsSafeHost}' ({primaryIp}) is not present in IT-approved Studio network allowlists.");
    }

    private static bool MatchesAllowlistEntry(Uri uri, IPAddress resolvedIp, StudioNetworkAllowlist entry)
    {
        switch (entry.EntryType)
        {
            case AllowlistEntryType.ExactIp:
                if (IPAddress.TryParse(entry.NetworkPattern.Trim(), out var exactIp))
                {
                    return resolvedIp.Equals(exactIp);
                }
                break;

            case AllowlistEntryType.CidrSubnet:
                if (IsInCidrRange(resolvedIp, entry.NetworkPattern.Trim()))
                {
                    return true;
                }
                break;

            case AllowlistEntryType.FqdnPattern:
                var pattern = entry.NetworkPattern.Trim();
                if (pattern.StartsWith("*."))
                {
                    var rootDomain = pattern[2..]; // e.g. "corp.wercsgcs.internal"
                    if (uri.DnsSafeHost.EndsWith(rootDomain, StringComparison.OrdinalIgnoreCase) &&
                        uri.DnsSafeHost.Length > rootDomain.Length)
                    {
                        return true;
                    }
                }
                else if (string.Equals(uri.DnsSafeHost, pattern, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
                break;
        }

        return false;
    }

    private static bool IsInCidrRange(IPAddress ip, string cidr)
    {
        var parts = cidr.Split('/');
        if (parts.Length != 2 || !IPAddress.TryParse(parts[0], out var baseIp) || !int.TryParse(parts[1], out var prefixLength))
            return false;

        if (ip.AddressFamily != baseIp.AddressFamily)
            return false;

        var ipBytes = ip.GetAddressBytes();
        var baseBytes = baseIp.GetAddressBytes();

        var fullBytes = prefixLength / 8;
        var remainingBits = prefixLength % 8;

        for (int i = 0; i < fullBytes; i++)
        {
            if (ipBytes[i] != baseBytes[i]) return false;
        }

        if (remainingBits > 0 && fullBytes < ipBytes.Length)
        {
            int mask = (byte)(0xFF << (8 - remainingBits));
            if ((ipBytes[fullBytes] & mask) != (baseBytes[fullBytes] & mask))
                return false;
        }

        return true;
    }
}
