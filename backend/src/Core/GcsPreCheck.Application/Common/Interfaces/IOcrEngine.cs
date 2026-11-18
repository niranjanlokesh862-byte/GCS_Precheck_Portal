namespace GcsPreCheck.Application.Common.Interfaces;

public record OcrWord(string Text, decimal Confidence, int X, int Y, int Width, int Height);
public record OcrLine(string LineText, decimal AverageConfidence, IReadOnlyList<OcrWord> Words);
public record OcrPageResult(string FullText, decimal OverallConfidence, IReadOnlyList<OcrLine> Lines);

public interface IOcrEngine
{
    Task<OcrPageResult> ExtractTextAsync(byte[] imageBytes, CancellationToken ct = default);
}

public record ExtractedParameterCandidate(
    string ParameterCode,
    string ParameterName,
    string ExtractedValue,
    decimal ConfidenceScore,
    string MatchedSnippet,
    string ScopeQualifier = "DEFAULT");

public record TicketExtractionSummary(
    decimal OverallConfidence,
    IReadOnlyList<ExtractedParameterCandidate> Parameters,
    IReadOnlyList<string> RawSnippets);

public interface ITicketParameterExtractor
{
    TicketExtractionSummary ExtractParameters(string rawOcrText, IReadOnlyList<OcrLine> lines);
}
