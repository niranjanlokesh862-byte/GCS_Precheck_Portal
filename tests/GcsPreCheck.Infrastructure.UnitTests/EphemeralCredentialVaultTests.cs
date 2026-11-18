using FluentAssertions;
using GcsPreCheck.Infrastructure.Security;
using Xunit;

namespace GcsPreCheck.Infrastructure.UnitTests;

public class EphemeralCredentialVaultTests : IDisposable
{
    private readonly EphemeralCredentialVault _vault;

    public EphemeralCredentialVaultTests()
    {
        _vault = new EphemeralCredentialVault();
    }

    [Fact]
    public void StoreAndClaim_WithinTtl_ReturnsCredentials_AndEnforcesSingleUse()
    {
        // Arrange
        var nonce = Guid.NewGuid();
        var username = "EnterpriseAdmin";
        var password = "SuperSecretPassword123!";

        // Act
        _vault.StoreCredentials(nonce, username, password, TimeSpan.FromSeconds(30));
        var envelope = _vault.ClaimCredentials(nonce);

        // Assert
        envelope.Should().NotBeNull();
        envelope!.Username.Should().Be("EnterpriseAdmin");
        new string(envelope.PasswordBuffer).Should().Be(password);

        // Verify single-use eviction: immediate second claim must return null
        var secondClaim = _vault.ClaimCredentials(nonce);
        secondClaim.Should().BeNull();
    }

    [Fact]
    public void ClaimCredentials_ExpiredEntry_ReturnsNull()
    {
        // Arrange
        var nonce = Guid.NewGuid();
        // Set an already-expired TTL
        _vault.StoreCredentials(nonce, "ExpiredUser", "SomePass", TimeSpan.FromMilliseconds(-100));

        // Act
        var envelope = _vault.ClaimCredentials(nonce);

        // Assert
        envelope.Should().BeNull();
    }

    [Fact]
    public void Invalidate_ExplicitlyRemovesCredentials()
    {
        // Arrange
        var nonce = Guid.NewGuid();
        _vault.StoreCredentials(nonce, "UserToInvalidate", "Secret!", TimeSpan.FromSeconds(60));

        // Act
        _vault.Invalidate(nonce);
        var envelope = _vault.ClaimCredentials(nonce);

        // Assert
        envelope.Should().BeNull();
    }

    [Fact]
    public void ClaimCredentials_NonExistentNonce_ReturnsNull()
    {
        // Act
        var envelope = _vault.ClaimCredentials(Guid.NewGuid());

        // Assert
        envelope.Should().BeNull();
    }

    public void Dispose()
    {
        _vault.Dispose();
    }
}
