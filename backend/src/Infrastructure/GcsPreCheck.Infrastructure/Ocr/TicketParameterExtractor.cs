using System.Text.RegularExpressions;
using GcsPreCheck.Application.Common.Interfaces;

namespace GcsPreCheck.Infrastructure.Ocr;

public class TicketParameterExtractor : ITicketParameterExtractor
{
    private static readonly (string Code, string Name, Regex Pattern, decimal DefaultConfidence)[] ParameterRules =
    [
        (
            "SITE_VERSION",
            "Site version / Patch",
            new Regex(@"(?:Site\s*Version(?:\s*\/\s*Patch)?|Studio\s*Version)\s*[:=\-]?\s*([0-9]+\.[0-9]+(?:\.[0-9]+)?(?:[_\-][A-Za-z0-9]+)?)", RegexOptions.IgnoreCase | RegexOptions.Compiled),
            0.92m
        ),
        (
            "CHEMADVISOR_VERSION",
            "ChemAdvisor version",
            new Regex(@"ChemAdvisor(?:\s*Version|\s*DB|\s*Database)?\s*[:=\-]?\s*([0-9]{4}\.[0-9]+|[0-9]+\.[0-9]+(?:\.[0-9]+)?)", RegexOptions.IgnoreCase | RegexOptions.Compiled),
            0.90m
        ),
        (
            "TRANSPORT_DB_VERSION",
            "Transport database version",
            new Regex(@"Transport(?:ation)?\s*(?:DB|Database)(?:\s*Version)?\s*[:=\-]?\s*([0-9]{4}\.[0-9]+|[0-9]+\.[0-9]+(?:\.[0-9]+)?)", RegexOptions.IgnoreCase | RegexOptions.Compiled),
            0.89m
        ),
        (
            "CURRENT_CONTENT_VERSION",
            "Current content version",
            new Regex(@"(?:Current|Installed|Baseline)\s*Content(?:\s*Version)?\s*[:=\-]?\s*([0-9]{4}\.[0-9]+|[0-9]+\.[0-9]+)", RegexOptions.IgnoreCase | RegexOptions.Compiled),
            0.91m
        ),
        (
            "REQUESTED_CONTENT_VERSION",
            "Requested content version",
            new Regex(@"(?:Requested|Target|Update)\s*Content(?:\s*Version)?\s*[:=\-]?\s*([0-9]{4}\.[0-9]+|[0-9]+\.[0-9]+)", RegexOptions.IgnoreCase | RegexOptions.Compiled),
            0.93m
        ),
        (
            "NUMBER_OF_REGIONS",
            "Number of regions",
            new Regex(@"(?:Number\s*of\s*Regions|Regions?\s*Count|Total\s*Regions)\s*[:=\-]?\s*([0-9]+)", RegexOptions.IgnoreCase | RegexOptions.Compiled),
            0.88m
        ),
        (
            "JAPAN_REGION",
            "Japan region",
            new Regex(@"Japan(?:\s*Region|\s*Jurisdiction)?\s*[:=\-]?\s*(Y(?:es)?|N(?:o)?|Active|Inactive|Enabled|Disabled)", RegexOptions.IgnoreCase | RegexOptions.Compiled),
            0.90m
        ),
        (
            "TRANSPORTATION",
            "Transportation",
            new Regex(@"Transportation(?:\s*Module)?\s*[:=\-]?\s*(Y(?:es)?|N(?:o)?|Active|Inactive|Enabled|Disabled)", RegexOptions.IgnoreCase | RegexOptions.Compiled),
            0.89m
        ),
        (
            "PCN",
            "PCN",
            new Regex(@"(?:PCN|Poison\s*Centr(?:e|er)\s*Notification)\s*[:=\-]?\s*(Y(?:es)?|N(?:o)?|Active|Inactive|Enabled|Disabled)", RegexOptions.IgnoreCase | RegexOptions.Compiled),
            0.90m
        ),
        (
            "CUSTOMER_CUSTOMIZATION",
            "Customer customization",
            new Regex(@"(?:Customer\s*Customization|Customizations?|Custom\s*Scripts?)\s*[:=\-]?\s*([A-Za-z0-9_\-\s]+)", RegexOptions.IgnoreCase | RegexOptions.Compiled),
            0.85m
        )
    ];

    public TicketExtractionSummary ExtractParameters(string rawOcrText, IReadOnlyList<OcrLine> lines)
    {
        var candidates = new List<ExtractedParameterCandidate>();
        var snippets = new List<string>();
        var totalConfidence = 0m;

        foreach (var rule in ParameterRules)
        {
            var match = rule.Pattern.Match(rawOcrText);
            if (match.Success && match.Groups.Count > 1)
            {
                var val = match.Groups[1].Value.Trim();
                var confidence = rule.DefaultConfidence;

                // Adjust confidence based on matched line confidence if available
                var matchingLine = lines.FirstOrDefault(l => l.LineText.Contains(val, StringComparison.OrdinalIgnoreCase));
                if (matchingLine != null && matchingLine.AverageConfidence > 0)
                {
                    confidence = Math.Round((confidence + matchingLine.AverageConfidence) / 2m, 4);
                }

                candidates.Add(new ExtractedParameterCandidate(
                    ParameterCode: rule.Code,
                    ParameterName: rule.Name,
                    ExtractedValue: val,
                    ConfidenceScore: confidence,
                    MatchedSnippet: match.Value
                ));

                snippets.Add(match.Value);
                totalConfidence += confidence;
            }
            else
            {
                // Unmatched parameter - record with 0 confidence and empty string for employee review
                candidates.Add(new ExtractedParameterCandidate(
                    ParameterCode: rule.Code,
                    ParameterName: rule.Name,
                    ExtractedValue: string.Empty,
                    ConfidenceScore: 0.00m,
                    MatchedSnippet: string.Empty
                ));
            }
        }

        var overallConfidence = candidates.Count > 0 
            ? Math.Round(totalConfidence / candidates.Count(c => c.ConfidenceScore > 0), 4) 
            : 0m;

        return new TicketExtractionSummary(overallConfidence, candidates, snippets);
    }
}
