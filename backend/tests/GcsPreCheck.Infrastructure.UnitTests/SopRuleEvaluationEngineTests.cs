using FluentAssertions;
using GcsPreCheck.Application.Features.RulesEngine;
using GcsPreCheck.Domain.Entities;
using GcsPreCheck.Domain.Enums;
using GcsPreCheck.Persistence.Contexts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace GcsPreCheck.Infrastructure.UnitTests;

public class SopRuleEvaluationEngineTests : IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly SopRuleEvaluationEngine _engine;
    private readonly string _dbName;

    public SopRuleEvaluationEngineTests()
    {
        _dbName = $"GcsTest_Sop_{Guid.NewGuid():N}";
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlServer($"Server=(localdb)\\mssqllocaldb;Database={_dbName};Trusted_Connection=True;MultipleActiveResultSets=true;Connect Timeout=15;")
            .Options;

        _context = new ApplicationDbContext(options);
        _context.Database.EnsureCreated();

        SeedSopConfigurations();

        _engine = new SopRuleEvaluationEngine(_context, NullLogger<SopRuleEvaluationEngine>.Instance);
    }

    private void SeedSopConfigurations()
    {
        var approver = new ApplicationUser
        {
            Username = "sop_approver",
            Email = "sop@company.internal",
            FullName = "Regulatory SOP Approver",
            Role = UserRole.LeadSpecialist,
            IsActive = true
        };
        _context.ApplicationUsers.Add(approver);
        _context.SaveChanges();

        _context.SopConfigurations.AddRange(
            new SopConfiguration
            {
                ParameterCode = "CHEMADVISOR_VERSION",
                SopDocumentId = "SOP-REG-CHEMADV",
                SopTitle = "Regulatory ChemAdvisor Database Validation",
                SopVersion = "v2.1",
                IsApproved = true,
                IsActive = true,
                ApprovedByUserId = approver.Id,
                ApprovedAtUtc = DateTime.UtcNow,
                EffectiveDate = new DateOnly(2025, 1, 1)
            },
            new SopConfiguration
            {
                ParameterCode = "TRANSPORT_DB_VERSION",
                SopDocumentId = "SOP-REG-TRANS",
                SopTitle = "Transport Engine Database Compliance",
                SopVersion = "v1.4",
                IsApproved = true,
                IsActive = true,
                ApprovedByUserId = approver.Id,
                ApprovedAtUtc = DateTime.UtcNow,
                EffectiveDate = new DateOnly(2025, 1, 1)
            },
            new SopConfiguration
            {
                ParameterCode = "JAPAN_REGION",
                SopDocumentId = "SOP-JURIS-APAC",
                SopTitle = "Japan Regulatory Jurisdiction Matrix",
                SopVersion = "v3.0",
                IsApproved = true,
                IsActive = true,
                ApprovedByUserId = approver.Id,
                ApprovedAtUtc = DateTime.UtcNow,
                EffectiveDate = new DateOnly(2025, 1, 1)
            }
        );

        _context.SaveChanges();
    }

    [Fact]
    public async Task EvaluateParameter_ChemAdvisorVersion_SemanticMatch_ReturnsPassWithSopDocument()
    {
        var runId = Guid.NewGuid();
        var input = new ParameterEvaluationInput(
            "CHEMADVISOR_VERSION",
            "ChemAdvisor version",
            "2024.1",
            "2024.1");

        var result = await _engine.EvaluateParameterAsync(runId, input);

        result.ComparisonStatus.Should().Be(ComparisonStatus.PASS);
        result.ScopeRuleApplied.Should().Be("SOP-REG-CHEMADV");
        result.EvaluationNotes.Should().Contain("Semantic GcsVersion match");
    }

    [Fact]
    public async Task EvaluateParameter_ChemAdvisorVersion_Mismatch_ReturnsFail()
    {
        var runId = Guid.NewGuid();
        var input = new ParameterEvaluationInput(
            "CHEMADVISOR_VERSION",
            "ChemAdvisor version",
            "2024.2",
            "2024.1");

        var result = await _engine.EvaluateParameterAsync(runId, input);

        result.ComparisonStatus.Should().Be(ComparisonStatus.FAIL);
        result.EvaluationNotes.Should().Contain("Version mismatch");
    }

    [Fact]
    public async Task EvaluateParameter_SiteVersion_SemanticPatch_ReturnsPass()
    {
        var runId = Guid.NewGuid();
        var input = new ParameterEvaluationInput(
            "SITE_VERSION",
            "Site version / Patch",
            "10.4.2_Patch1",
            "10.4.2_Patch1");

        var result = await _engine.EvaluateParameterAsync(runId, input);

        result.ComparisonStatus.Should().Be(ComparisonStatus.PASS);
        result.EvaluationNotes.Should().Contain("Semantic GcsVersion match");
    }

    [Fact]
    public async Task EvaluateParameter_RequestedContentVersion_PrerequisiteSatisfied_ReturnsPass()
    {
        var runId = Guid.NewGuid();
        // Installed baseline is 2024.1, requested target is 2024.2 -> upgrade prerequisite met
        var input = new ParameterEvaluationInput(
            "REQUESTED_CONTENT_VERSION",
            "Requested content version",
            "2024.2",
            "2024.1");

        var result = await _engine.EvaluateParameterAsync(runId, input);

        result.ComparisonStatus.Should().Be(ComparisonStatus.PASS);
        result.EvaluationNotes.Should().Contain("Prerequisite satisfied");
    }

    [Fact]
    public async Task EvaluateParameter_RequestedContentVersion_AlreadyAtOrAboveTarget_ReturnsNeedsReview()
    {
        var runId = Guid.NewGuid();
        // Installed baseline is 2024.2, requested target is 2024.1 -> needs review
        var input = new ParameterEvaluationInput(
            "REQUESTED_CONTENT_VERSION",
            "Requested content version",
            "2024.1",
            "2024.2");

        var result = await _engine.EvaluateParameterAsync(runId, input);

        result.ComparisonStatus.Should().Be(ComparisonStatus.NEEDS_REVIEW);
        result.EvaluationNotes.Should().Contain("already at or below installed baseline");
    }

    [Fact]
    public async Task EvaluateParameter_JapanRegion_ExpectedNo_WhenDisabledInStudio_ReturnsPass()
    {
        var runId = Guid.NewGuid();
        var input = new ParameterEvaluationInput(
            "JAPAN_REGION",
            "Japan region",
            "N",
            "Disabled");

        var result = await _engine.EvaluateParameterAsync(runId, input);

        result.ComparisonStatus.Should().Be(ComparisonStatus.PASS);
        result.IsConditional.Should().BeTrue();
        result.ScopeRuleApplied.Should().Be("SOP-JURIS-APAC");
        result.EvaluationNotes.Should().Contain("Japan region is disabled/absent in Studio");
    }

    [Fact]
    public async Task EvaluateParameter_JapanRegion_ExpectedNo_WhenActiveInStudio_ReturnsFail()
    {
        var runId = Guid.NewGuid();
        var input = new ParameterEvaluationInput(
            "JAPAN_REGION",
            "Japan region",
            "N",
            "Active");

        var result = await _engine.EvaluateParameterAsync(runId, input);

        result.ComparisonStatus.Should().Be(ComparisonStatus.FAIL);
        result.IsConditional.Should().BeTrue();
        result.EvaluationNotes.Should().Contain("SOP violation");
    }

    [Fact]
    public async Task EvaluateParameter_Transportation_ExpectedNo_WhenDisabled_ReturnsPass()
    {
        var runId = Guid.NewGuid();
        var input = new ParameterEvaluationInput(
            "TRANSPORTATION",
            "Transportation",
            "N",
            "Disabled");

        var result = await _engine.EvaluateParameterAsync(runId, input);

        result.ComparisonStatus.Should().Be(ComparisonStatus.PASS);
        result.IsConditional.Should().BeTrue();
        result.EvaluationNotes.Should().Contain("Transportation module is disabled/absent");
    }

    [Fact]
    public async Task EvaluateParameter_Pcn_ExpectedYes_WhenConfigured_ReturnsPass()
    {
        var runId = Guid.NewGuid();
        var input = new ParameterEvaluationInput(
            "PCN",
            "PCN",
            "Y",
            "Configured");

        var result = await _engine.EvaluateParameterAsync(runId, input);

        result.ComparisonStatus.Should().Be(ComparisonStatus.PASS);
        result.IsConditional.Should().BeTrue();
        result.EvaluationNotes.Should().Contain("PCN module is configured and verified");
    }

    [Fact]
    public async Task EvaluateParameter_Customization_StandardCore_ReturnsPass()
    {
        var runId = Guid.NewGuid();
        var input = new ParameterEvaluationInput(
            "CUSTOMER_CUSTOMIZATION",
            "Customer customization",
            "None",
            "Standard");

        var result = await _engine.EvaluateParameterAsync(runId, input);

        result.ComparisonStatus.Should().Be(ComparisonStatus.PASS);
        result.EvaluationNotes.Should().Contain("standard GCS core");
    }

    [Fact]
    public async Task EvaluateParameter_EmptyStudioActual_ReturnsNotVerified()
    {
        var runId = Guid.NewGuid();
        var input = new ParameterEvaluationInput(
            "SITE_VERSION",
            "Site version / Patch",
            "10.4.2_Patch1",
            null);

        var result = await _engine.EvaluateParameterAsync(runId, input);

        result.ComparisonStatus.Should().Be(ComparisonStatus.NOT_VERIFIED);
        result.EvaluationNotes.Should().Contain("not located");
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
