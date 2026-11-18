using FluentAssertions;
using GcsPreCheck.Application.Common.Interfaces;
using GcsPreCheck.Infrastructure.Ocr;
using Xunit;

namespace GcsPreCheck.Infrastructure.UnitTests;

public class TicketParameterExtractorTests
{
    private readonly TicketParameterExtractor _extractor = new();

    [Fact]
    public void ExtractParameters_RealisticCustomerTicket_ExtractsAll10Parameters()
    {
        var rawTicketText = """
        CUSTOMER CHANGE REQUEST - GCS WERCS UPGRADE TICKET #TCK-9941
        Customer Name: Apex Global Chemicals
        Classification: eLite

        TECHNICAL SPECIFICATIONS:
        Site Version: 10.4.2_Patch1
        ChemAdvisor Version: 2024.1
        Transport DB Version: 2024.2
        Current Content Version: 2024.1
        Requested Content Version: 2024.2
        Number of Regions: 14
        Japan Region: Yes
        Transportation: Active
        PCN: Y
        Customer Customization: Custom_EHS_UserExit_V2
        """;

        var lines = rawTicketText.Split('\n')
            .Select(l => new OcrLine(l.Trim(), 0.95m, Array.Empty<OcrWord>()))
            .ToList();

        var summary = _extractor.ExtractParameters(rawTicketText, lines);

        summary.Parameters.Should().HaveCount(10);
        summary.OverallConfidence.Should().BeGreaterThan(0.85m);

        var paramMap = summary.Parameters.ToDictionary(p => p.ParameterCode, p => p.ExtractedValue);

        paramMap["SITE_VERSION"].Should().Be("10.4.2_Patch1");
        paramMap["CHEMADVISOR_VERSION"].Should().Be("2024.1");
        paramMap["TRANSPORT_DB_VERSION"].Should().Be("2024.2");
        paramMap["CURRENT_CONTENT_VERSION"].Should().Be("2024.1");
        paramMap["REQUESTED_CONTENT_VERSION"].Should().Be("2024.2");
        paramMap["NUMBER_OF_REGIONS"].Should().Be("14");
        paramMap["JAPAN_REGION"].Should().Be("Yes");
        paramMap["TRANSPORTATION"].Should().Be("Active");
        paramMap["PCN"].Should().Be("Y");
        paramMap["CUSTOMER_CUSTOMIZATION"].Should().Be("Custom_EHS_UserExit_V2");
    }

    [Fact]
    public void ExtractParameters_SeparatesChemAdvisorAndTransportDb_EvenWhenAdjacent()
    {
        var rawText = """
        ChemAdvisor: 2024.1
        Transport DB: 2023.2
        """;

        var summary = _extractor.ExtractParameters(rawText, Array.Empty<OcrLine>());
        var chem = summary.Parameters.First(p => p.ParameterCode == "CHEMADVISOR_VERSION");
        var trans = summary.Parameters.First(p => p.ParameterCode == "TRANSPORT_DB_VERSION");

        chem.ExtractedValue.Should().Be("2024.1");
        trans.ExtractedValue.Should().Be("2023.2");
    }

    [Fact]
    public void ExtractParameters_MissingParameter_ReturnsEmptyValueWithZeroConfidence()
    {
        var rawText = """
        Site Version: 10.4.0
        """;

        var summary = _extractor.ExtractParameters(rawText, Array.Empty<OcrLine>());
        var pcn = summary.Parameters.First(p => p.ParameterCode == "PCN");

        pcn.ExtractedValue.Should().BeEmpty();
        pcn.ConfidenceScore.Should().Be(0m);
    }
}
