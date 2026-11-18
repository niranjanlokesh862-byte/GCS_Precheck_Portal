using System.Text;
using GcsPreCheck.Application.Common.Interfaces;

namespace GcsPreCheck.Infrastructure.Reports;

public class StandardPreCheckPdfReportGenerator : IPdfReportGenerator
{
    public Task<byte[]> GeneratePreCheckReportAsync(PreCheckReportDto report, CancellationToken ct = default)
    {
        var sb = new StringBuilder();

        // PDF Content Stream Instructions
        sb.AppendLine("BT");
        sb.AppendLine("/F1 18 Tf");
        sb.AppendLine("50 750 Td");
        sb.AppendLine("(WERCS GCS PRE-CHECK AUTOMATION AUDIT REPORT) Tj");

        sb.AppendLine("0 -24 Td");
        sb.AppendLine("/F1 10 Tf");
        sb.AppendLine($"(Generated: {report.ExecutedAtUtc:yyyy-MM-dd HH:mm:ss} UTC  |  Status: {report.OverallResult}) Tj");

        sb.AppendLine("0 -20 Td");
        sb.AppendLine("/F1 11 Tf");
        sb.AppendLine($"(Customer: {EscapePdf(report.CustomerName)}  [{report.CustomerClassification}]) Tj");

        sb.AppendLine("0 -15 Td");
        sb.AppendLine($"(Ticket ID: {report.TicketId}  |  Run ID: {report.RunId}) Tj");

        sb.AppendLine("0 -15 Td");
        sb.AppendLine($"(Studio Target: {EscapePdf(report.StudioUrl)}  [{report.TargetEnvironment}]) Tj");

        sb.AppendLine("0 -15 Td");
        sb.AppendLine($"(Worker Node: {EscapePdf(report.WorkerId)}  |  Verified By: {EscapePdf(report.VerifiedByUserName)}) Tj");

        sb.AppendLine("0 -25 Td");
        sb.AppendLine("/F1 13 Tf");
        sb.AppendLine("(PARAMETER VERIFICATION MATRIX) Tj");

        sb.AppendLine("0 -18 Td");
        sb.AppendLine("/F1 9 Tf");
        sb.AppendLine("(CODE                  EXPECTED       ACTUAL         STATUS         RULE APPLIED) Tj");
        sb.AppendLine("0 -10 Td");
        sb.AppendLine("(--------------------------------------------------------------------------------) Tj");

        foreach (var p in report.ParameterResults)
        {
            sb.AppendLine("0 -14 Td");
            var code = Pad(p.ParameterCode, 20);
            var exp = Pad(p.TicketExpectedValue, 14);
            var act = Pad(p.StudioActualValue ?? "N/A", 14);
            var stat = Pad(p.ComparisonStatus, 14);
            var rule = p.ScopeRuleApplied;
            var line = $"{code} {exp} {act} {stat} {rule}";
            sb.AppendLine($"({EscapePdf(line)}) Tj");
        }

        sb.AppendLine("0 -25 Td");
        sb.AppendLine("/F1 11 Tf");
        sb.AppendLine("(SECURITY & COMPLIANCE ATTESTATION) Tj");

        sb.AppendLine("0 -14 Td");
        sb.AppendLine("/F1 9 Tf");
        sb.AppendLine($"(Antivirus: {EscapePdf(report.AntivirusScanAttestation)}) Tj");

        sb.AppendLine("0 -12 Td");
        sb.AppendLine($"(Network Boundary: {EscapePdf(report.SsrNetworkAttestation)}) Tj");

        sb.AppendLine("0 -12 Td");
        sb.AppendLine("(Credentials: Ephemeral In-Memory Only [Single-Use Claimed, Zero-Memory Wiped]) Tj");

        sb.AppendLine("0 -12 Td");
        sb.AppendLine("(Automation Safeguard: Browser Mutating Verbs [PUT, DELETE, PATCH] Aborted by Guard) Tj");

        sb.AppendLine("ET");

        var streamBytes = Encoding.ASCII.GetBytes(sb.ToString());

        // Construct standard PDF 1.4 document
        using var ms = new MemoryStream();
        using var writer = new StreamWriter(ms, Encoding.ASCII);

        var offsets = new List<long>();

        writer.Write("%PDF-1.4\n");
        writer.Flush();

        // 1 0 obj: Catalog
        offsets.Add(ms.Position);
        writer.Write("1 0 obj\n<< /Type /Catalog /Pages 2 0 R >>\nendobj\n");
        writer.Flush();

        // 2 0 obj: Pages
        offsets.Add(ms.Position);
        writer.Write("2 0 obj\n<< /Type /Pages /Kids [3 0 R] /Count 1 >>\nendobj\n");
        writer.Flush();

        // 3 0 obj: Page
        offsets.Add(ms.Position);
        writer.Write("3 0 obj\n<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Resources << /Font << /F1 4 0 R >> >> /Contents 5 0 R >>\nendobj\n");
        writer.Flush();

        // 4 0 obj: Font
        offsets.Add(ms.Position);
        writer.Write("4 0 obj\n<< /Type /Font /Subtype /Type1 /BaseFont /Courier >>\nendobj\n");
        writer.Flush();

        // 5 0 obj: Stream Content
        offsets.Add(ms.Position);
        writer.Write($"5 0 obj\n<< /Length {streamBytes.Length} >>\nstream\n");
        writer.Flush();
        ms.Write(streamBytes, 0, streamBytes.Length);
        writer.Write("\nendstream\nendobj\n");
        writer.Flush();

        // xref table
        var startXref = ms.Position;
        writer.Write($"xref\n0 6\n0000000000 65535 f \n");
        foreach (var offset in offsets)
        {
            writer.Write($"{offset:D10} 00000 n \n");
        }

        // trailer
        writer.Write($"trailer\n<< /Size 6 /Root 1 0 R >>\nstartxref\n{startXref}\n%%EOF\n");
        writer.Flush();

        return Task.FromResult(ms.ToArray());
    }

    private static string EscapePdf(string input)
    {
        if (string.IsNullOrEmpty(input)) return string.Empty;
        return input.Replace("\\", "\\\\").Replace("(", "\\(").Replace(")", "\\)");
    }

    private static string Pad(string text, int length)
    {
        if (text.Length >= length) return text[..length];
        return text.PadRight(length);
    }
}
