using System.Runtime.Versioning;
using System.Security;
using System.Security.Principal;
using FluentAssertions;
using GcsPreCheck.Infrastructure.Security;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace GcsPreCheck.Infrastructure.UnitTests;

[SupportedOSPlatform("windows")]
public class NamedPipeSecurityTests
{
    private readonly NamedPipeSecurityModule _module;

    public NamedPipeSecurityTests()
    {
        _module = new NamedPipeSecurityModule(NullLogger<NamedPipeSecurityModule>.Instance);
    }

    [Fact]
    public async Task NamedPipe_MutualAuthenticationAndNonceExchange_Succeeds()
    {
        var pipeName = $"GcsTestPipe_{Guid.NewGuid():N}";
        var currentAccount = WindowsIdentity.GetCurrent().Name;
        var testNonce = Guid.NewGuid();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));

        // Start server listening task
        var serverTask = Task.Run(async () =>
        {
            return await _module.WaitForCredentialRequestAsync(pipeName, currentAccount, testNonce, cts.Token);
        }, cts.Token);

        // Small delay to ensure server pipe is waiting for connection
        await Task.Delay(200, cts.Token);

        // Client connects and exchanges nonce
        var authenticatedServer = await _module.RequestCredentialsAsync(pipeName, string.Empty, currentAccount, testNonce, cts.Token);

        authenticatedServer.Should().NotBeNull();

        var receivedNonceBytes = await serverTask;
        new Guid(receivedNonceBytes).Should().Be(testNonce);
    }

    [Fact]
    public async Task NamedPipe_InvalidNonce_ThrowsSecurityException()
    {
        var pipeName = $"GcsTestPipe_Fail_{Guid.NewGuid():N}";
        var currentAccount = WindowsIdentity.GetCurrent().Name;
        var validNonce = Guid.NewGuid();
        var forgedNonce = Guid.NewGuid();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));

        var serverTask = Task.Run(async () =>
        {
            return await _module.WaitForCredentialRequestAsync(pipeName, currentAccount, validNonce, cts.Token);
        }, cts.Token);

        await Task.Delay(200, cts.Token);

        // Client presents forged nonce
        var clientTask = Task.Run(async () =>
        {
            await _module.RequestCredentialsAsync(pipeName, string.Empty, currentAccount, forgedNonce, cts.Token);
        }, cts.Token);

        // Server should reject the mismatched nonce and throw SecurityException
        var serverFunc = async () => await serverTask;
        await serverFunc.Should().ThrowAsync<SecurityException>();

        // Ensure client finishes (or throws due to severed pipe)
        try { await clientTask; } catch { /* expected severed pipe */ }
    }
}
