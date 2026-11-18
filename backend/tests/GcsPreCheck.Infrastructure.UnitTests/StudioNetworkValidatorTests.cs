using System.Net;
using FluentAssertions;
using GcsPreCheck.Domain.Entities;
using GcsPreCheck.Domain.Enums;
using GcsPreCheck.Infrastructure.NetworkSecurity;
using GcsPreCheck.Persistence.Contexts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace GcsPreCheck.Infrastructure.UnitTests;

public class StudioNetworkValidatorTests : IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly StudioNetworkValidator _validator;
    private readonly string _dbName;

    public StudioNetworkValidatorTests()
    {
        _dbName = $"GcsTest_Net_{Guid.NewGuid():N}";
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlServer($"Server=(localdb)\\mssqllocaldb;Database={_dbName};Trusted_Connection=True;MultipleActiveResultSets=true;Connect Timeout=15;")
            .Options;

        _context = new ApplicationDbContext(options);
        _context.Database.EnsureCreated();

        SeedTestData();

        _validator = new StudioNetworkValidator(_context, NullLogger<StudioNetworkValidator>.Instance);
    }

    private void SeedTestData()
    {
        var approver = new ApplicationUser
        {
            Username = "it_sec_approver",
            Email = "it_sec@company.internal",
            FullName = "IT Security Approver",
            Role = UserRole.Admin,
            IsActive = true
        };
        _context.ApplicationUsers.Add(approver);
        _context.SaveChanges();

        _context.StudioNetworkAllowlists.AddRange(
            new StudioNetworkAllowlist
            {
                NetworkPattern = "192.168.1.155",
                EntryType = AllowlistEntryType.ExactIp,
                EnvironmentDescription = "WERCS GCS Customer On-Premise Staging",
                IsActive = true,
                ApprovedByUserId = approver.Id,
                ApprovedAtUtc = DateTime.UtcNow
            },
            new StudioNetworkAllowlist
            {
                NetworkPattern = "10.200.0.0/16",
                EntryType = AllowlistEntryType.CidrSubnet,
                EnvironmentDescription = "Enterprise Internal Datacenter Subnet",
                IsActive = true,
                ApprovedByUserId = approver.Id,
                ApprovedAtUtc = DateTime.UtcNow
            },
            new StudioNetworkAllowlist
            {
                NetworkPattern = "*.wercsgcs.internal",
                EntryType = AllowlistEntryType.FqdnPattern,
                EnvironmentDescription = "Internal Domain Wildcard",
                IsActive = true,
                ApprovedByUserId = approver.Id,
                ApprovedAtUtc = DateTime.UtcNow
            },
            new StudioNetworkAllowlist
            {
                NetworkPattern = "192.168.1.99",
                EntryType = AllowlistEntryType.ExactIp,
                EnvironmentDescription = "Decommissioned Studio Node",
                IsActive = false, // Inactive
                ApprovedByUserId = approver.Id,
                ApprovedAtUtc = DateTime.UtcNow
            }
        );

        _context.SaveChanges();
    }

    [Fact]
    public async Task ValidateDestination_ProductionHttp_FailsSsrfValidation()
    {
        // Production requires HTTPS
        var uri = new Uri("http://192.168.1.155/WERCS");
        var result = await _validator.ValidateDestinationAsync(uri, TargetEnvironment.Production);

        result.IsAllowed.Should().BeFalse();
        result.FailureReason.Should().Contain("strictly require HTTPS");
    }

    [Fact]
    public async Task ValidateDestination_LoopbackIp_FailsSsrfValidation()
    {
        var uri = new Uri("https://127.0.0.1/WERCS");
        var result = await _validator.ValidateDestinationAsync(uri, TargetEnvironment.Production);

        result.IsAllowed.Should().BeFalse();
        result.FailureReason.Should().Contain("Loopback");
    }

    [Fact]
    public async Task ValidateDestination_CloudMetadataIp_FailsSsrfValidation()
    {
        var uri = new Uri("https://169.254.169.254/latest/meta-data/");
        var result = await _validator.ValidateDestinationAsync(uri, TargetEnvironment.Production);

        result.IsAllowed.Should().BeFalse();
        result.FailureReason.Should().ContainEquivalentOf("cloud instance metadata");
    }

    [Fact]
    public async Task ValidateDestination_AllowlistedExactIp_Succeeds()
    {
        var uri = new Uri("https://192.168.1.155/WERCS/Studio.aspx");
        var result = await _validator.ValidateDestinationAsync(uri, TargetEnvironment.Production);

        result.IsAllowed.Should().BeTrue();
        result.ResolvedIp.Should().Be(IPAddress.Parse("192.168.1.155"));
        result.MatchedAllowlistId.Should().NotBeNull();
    }

    [Fact]
    public async Task ValidateDestination_AllowlistedCidr_Succeeds()
    {
        var uri = new Uri("https://10.200.45.10/WERCS/Studio.aspx");
        var result = await _validator.ValidateDestinationAsync(uri, TargetEnvironment.Production);

        result.IsAllowed.Should().BeTrue();
        result.ResolvedIp.Should().Be(IPAddress.Parse("10.200.45.10"));
        result.MatchedAllowlistId.Should().NotBeNull();
    }

    [Fact]
    public async Task ValidateDestination_NonAllowlistedIp_FailsSsrfValidation()
    {
        var uri = new Uri("https://192.168.100.5/WERCS/Studio.aspx");
        var result = await _validator.ValidateDestinationAsync(uri, TargetEnvironment.Production);

        result.IsAllowed.Should().BeFalse();
        result.FailureReason.Should().Contain("not present in IT-approved Studio network allowlists");
    }

    [Fact]
    public async Task ValidateDestination_InactiveAllowlistIp_FailsSsrfValidation()
    {
        var uri = new Uri("https://192.168.1.99/WERCS/Studio.aspx");
        var result = await _validator.ValidateDestinationAsync(uri, TargetEnvironment.Production);

        result.IsAllowed.Should().BeFalse();
        result.FailureReason.Should().Contain("not present in IT-approved Studio network allowlists");
    }

    public void Dispose()
    {
        try
        {
            _context.Database.EnsureDeleted();
            _context.Dispose();
        }
        catch { }
    }
}
