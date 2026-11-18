using System.Text;
using FluentAssertions;
using GcsPreCheck.Application.Common.Interfaces;
using GcsPreCheck.Infrastructure.Reports;
using Xunit;

namespace GcsPreCheck.Infrastructure.UnitTests;

public class PdfReportGeneratorTests
{
    private readonly StandardPreCheckPdfReportGenerator _generator;

    public PdfReportGeneratorTests()
    {
        _generator = new StandardPreCheckPdfReportGenerator();
    }

    [Fact]
    public async Task GeneratePreCheckReport_ProducesValidPdfDocument()
    {
        var reportData = new PreCheckReportDto(
            TicketId: Guid.NewGuid(),
            CustomerName: "Acme Chemicals International",
            CustomerClassification: "eLite",
            RunId: Guid.NewGuid(),
            StudioUrl: "https://192.168.1.155/WERCS",
            TargetEnvironment: "Production",
            WorkerId: "WORKER-NODE-01",
            ExecutedAtUtc: DateTime.UtcNow,
            OverallResult: "PASS",
            ParameterResults:
            [
                new("SITE_VERSION", "Site version / Patch", "10.4.2_Patch1", "10.4.2_Patch1", "PASS", "SOP-WERCS-PREREQ", "Semantic GcsVersion match"),
                new("CHEMADVISOR_VERSION", "ChemAdvisor version", "2024.1", "2024.1", "PASS", "SOP-REG-CHEMADV", "Semantic GcsVersion match"),
                new("TRANSPORT_DB_VERSION", "Transport database version", "2024.2", "2024.2", "PASS", "SOP-REG-TRANS", "Semantic GcsVersion match"),
                new("CURRENT_CONTENT_VERSION", "Current content version", "2024.1", "2024.1", "PASS", "SOP-WERCS-UPGRADE", "Installed content verified"),
                new("REQUESTED_CONTENT_VERSION", "Requested content version", "2024.2", "2024.1", "PASS", "SOP-WERCS-UPGRADE", "Prerequisite satisfied"),
                new("NUMBER_OF_REGIONS", "Number of regions", "14", "14", "PASS", "SOP-JURIS-GLOBAL", "Region count match"),
                new("JAPAN_REGION", "Japan region", "N", "Disabled", "PASS", "SOP-JURIS-APAC", "Japan disabled"),
                new("TRANSPORTATION", "Transportation", "Active", "Active", "PASS", "SOP-MOD-TRANSPORT", "Transportation active"),
                new("PCN", "PCN", "Y", "Configured", "PASS", "SOP-EU-COMPLIANCE", "PCN verified"),
                new("CUSTOMER_CUSTOMIZATION", "Customer customization", "None", "Standard", "PASS", "SOP-QA-CUSTOM-AUDIT", "Standard core verified")
            ],
            VerifiedByUserName: "John Doe (Lead Specialist)",
            VerifiedAtUtc: DateTime.UtcNow);

        var pdfBytes = await _generator.GeneratePreCheckReportAsync(reportData);

        pdfBytes.Should().NotBeNullOrEmpty();
        pdfBytes.Length.Should().BeGreaterThan(300);

        var pdfHeader = Encoding.ASCII.GetString(pdfBytes[..8]);
        pdfHeader.Should().StartWith("%PDF-1.4");

        var fullPdfString = Encoding.ASCII.GetString(pdfBytes);
        fullPdfString.Should().Contain("WERCS GCS PRE-CHECK AUTOMATION AUDIT REPORT");
        fullPdfString.Should().Contain("Acme Chemicals International");
        fullPdfString.Should().Contain("SOP-REG-CHEMADV");
        fullPdfString.Should().Contain("trailer");
        fullPdfString.Should().Contain("%%EOF");
    }
}
