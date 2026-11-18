using System.Buffers.Binary;
using FluentAssertions;
using GcsPreCheck.Infrastructure.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Primitives;
using Xunit;

namespace GcsPreCheck.Infrastructure.UnitTests;

public class AttachmentStorageServiceTests : IDisposable
{
    private readonly string _testDir;
    private readonly AttachmentStorageService _service;

    public AttachmentStorageServiceTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), $"GcsStorageTest_{Guid.NewGuid():N}");
        var inMemoryConfig = new Dictionary<string, string?>
        {
            ["Storage:BasePath"] = _testDir
        };
        var config = new SimpleTestConfiguration(inMemoryConfig);
        _service = new AttachmentStorageService(config, NullLogger<AttachmentStorageService>.Instance);
    }

    [Fact]
    public void ValidateImageHeader_ValidPng_ReturnsSuccess()
    {
        var validPng = CreateSyntheticPng(800, 600);
        var result = _service.ValidateImageHeader(validPng);

        result.IsValid.Should().BeTrue();
        result.MimeType.Should().Be("image/png");
        result.Width.Should().Be(800);
        result.Height.Should().Be(600);
        result.Sha256Hash.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void ValidateImageHeader_OversizedDimensions_ReturnsFailure()
    {
        var oversizedPng = CreateSyntheticPng(10000, 500); // Exceeds 8192px cap
        var result = _service.ValidateImageHeader(oversizedPng);

        result.IsValid.Should().BeFalse();
        result.ErrorMessage.Should().Contain("dimensions");
    }

    [Fact]
    public void ValidateImageHeader_InvalidMagicBytes_ReturnsFailure()
    {
        var invalidPayload = new byte[] { 0x4D, 0x5A, 0x90, 0x00 }; // Windows Executable 'MZ'
        var result = _service.ValidateImageHeader(invalidPayload);

        result.IsValid.Should().BeFalse();
        result.ErrorMessage.Should().Contain("magic byte");
    }

    [Fact]
    public async Task SaveToQuarantine_And_PromoteToVerified_Succeeds()
    {
        var validPng = CreateSyntheticPng(400, 300);
        var stored = await _service.SaveToQuarantineAsync(validPng, "TicketScreenshot.png");

        File.Exists(stored.FullDiskPath).Should().BeTrue();
        stored.FullDiskPath.Should().Contain("Quarantine");

        var verifiedRelativePath = await _service.PromoteFromQuarantineToVerifiedAsync(stored.FullDiskPath);
        File.Exists(stored.FullDiskPath).Should().BeFalse(); // Moved from quarantine
        verifiedRelativePath.Should().Contain("VerifiedAttachments");
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDir))
        {
            try { Directory.Delete(_testDir, true); } catch { }
        }
    }

    private static byte[] CreateSyntheticPng(int width, int height)
    {
        // 8-byte PNG signature
        byte[] signature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
        
        // IHDR Chunk: 4 bytes length (13), 4 bytes "IHDR", 13 bytes data, 4 bytes CRC
        var ihdr = new byte[25];
        BinaryPrimitives.WriteInt32BigEndian(ihdr.AsSpan(0, 4), 13);
        ihdr[4] = (byte)'I'; ihdr[5] = (byte)'H'; ihdr[6] = (byte)'D'; ihdr[7] = (byte)'R';
        BinaryPrimitives.WriteInt32BigEndian(ihdr.AsSpan(8, 4), width);
        BinaryPrimitives.WriteInt32BigEndian(ihdr.AsSpan(12, 4), height);
        ihdr[16] = 8; // Bit depth
        ihdr[17] = 2; // Color type (RGB)
        ihdr[18] = 0; // Compression
        ihdr[19] = 0; // Filter
        ihdr[20] = 0; // Interlace
        ihdr[21] = 0xAA; ihdr[22] = 0xBB; ihdr[23] = 0xCC; ihdr[24] = 0xDD;

        var full = new byte[signature.Length + ihdr.Length];
        Array.Copy(signature, 0, full, 0, signature.Length);
        Array.Copy(ihdr, 0, full, signature.Length, ihdr.Length);
        return full;
    }

    private class SimpleTestConfiguration : IConfiguration
    {
        private readonly Dictionary<string, string?> _dict;
        public SimpleTestConfiguration(Dictionary<string, string?> dict) => _dict = dict;
        public string? this[string key] { get => _dict.TryGetValue(key, out var val) ? val : null; set => _dict[key] = value; }
        public IEnumerable<IConfigurationSection> GetChildren() => Enumerable.Empty<IConfigurationSection>();
        public IChangeToken GetReloadToken() => new DummyReloadToken();
        public IConfigurationSection GetSection(string key) => null!;
    }

    private class DummyReloadToken : IChangeToken
    {
        public bool HasChanged => false;
        public bool ActiveChangeCallbacks => false;
        public IDisposable RegisterChangeCallback(Action<object?> callback, object? state) => new DummyDisposable();
    }

    private class DummyDisposable : IDisposable
    {
        public void Dispose() { }
    }
}
