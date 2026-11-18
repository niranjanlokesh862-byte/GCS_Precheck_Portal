using FluentAssertions;
using GcsPreCheck.Domain.ValueObjects;
using Xunit;

namespace GcsPreCheck.Domain.UnitTests;

public class GcsVersionTests
{
    [Theory]
    [InlineData("10.2", "10.10", -1)] // Numeric minor comparison (2 < 10)
    [InlineData("10.4.2", "10.4.2", 0)] // Exact match
    [InlineData("10.4.2-RC1", "10.4.2", -1)] // Pre-release RC is older than canonical release
    [InlineData("10.4.2-RC1", "10.4.2-RC2", -1)] // RC index comparison
    [InlineData("10.4.2", "10.4.2_HF1", -1)] // Hotfix is newer than base release
    [InlineData("10.4.2_HF1", "10.4.2_Patch1", -1)] // Patch tier is strictly newer than hotfix tier
    [InlineData("10.4.2_Patch1", "10.4.2_Patch2", -1)] // Patch index comparison
    [InlineData("2024.1.0", "2024.1", 0)] // Missing build normalized to 0
    [InlineData("10.4.2.100", "10.4.2.99", 1)] // Revision comparison
    [InlineData("10.4.2-Alpha1", "10.4.2-Beta1", -1)] // Alpha < Beta
    [InlineData("10.4.2-Beta2", "10.4.2-RC1", -1)] // Beta < RC
    public void CompareTo_EvaluatesCorrectSemanticPrecedence(string v1Str, string v2Str, int expectedSign)
    {
        var success1 = GcsVersion.TryParse(v1Str, out var v1);
        var success2 = GcsVersion.TryParse(v2Str, out var v2);

        success1.Should().BeTrue($"'{v1Str}' should be valid syntax");
        success2.Should().BeTrue($"'{v2Str}' should be valid syntax");

        var actualSign = Math.Sign(v1.CompareTo(v2));
        actualSign.Should().Be(expectedSign, $"Comparing '{v1Str}' to '{v2Str}' should yield sign {expectedSign}");
    }

    [Fact]
    public void TryParse_InvalidGrammar_ReturnsFalse()
    {
        var success = GcsVersion.TryParse("Special_Vendor_Build", out _);
        success.Should().BeFalse("Arbitrary non-version strings should fail TryParse for fallback to NEEDS_REVIEW");
    }

    [Fact]
    public void EqualityOperators_ReflectValueObjectIdentity()
    {
        var v1 = GcsVersion.Parse("10.4.2_Patch1");
        var v2 = GcsVersion.Parse("10.4.2_Patch1");
        var v3 = GcsVersion.Parse("10.4.2_Patch2");

        (v1 == v2).Should().BeTrue();
        (v1 != v3).Should().BeTrue();
        (v1 < v3).Should().BeTrue();
        (v3 > v1).Should().BeTrue();
        v1.GetHashCode().Should().Be(v2.GetHashCode());
    }
}
