using GcsPreCheck.Domain.Entities;
using GcsPreCheck.Domain.Enums;

namespace GcsPreCheck.Domain.Services;

public static class OverallStatusAggregator
{
    public static OverallResult Aggregate(IEnumerable<ParameterCheckResult> results)
    {
        var list = results.ToList();
        if (list.Count == 0)
            return OverallResult.NOT_VERIFIED;

        if (list.Any(r => r.ComparisonStatus == ComparisonStatus.FAIL))
            return OverallResult.FAIL;

        if (list.Any(r => r.ComparisonStatus == ComparisonStatus.NEEDS_REVIEW))
            return OverallResult.NEEDS_REVIEW;

        if (list.Any(r => r.ComparisonStatus == ComparisonStatus.NOT_VERIFIED))
            return OverallResult.NOT_VERIFIED;

        var passCount = list.Count(r => r.ComparisonStatus == ComparisonStatus.PASS);
        var naCount = list.Count(r => r.ComparisonStatus == ComparisonStatus.NOT_APPLICABLE);

        if (passCount > 0 && (passCount + naCount == list.Count))
            return OverallResult.PASS;

        if (naCount == list.Count)
            return OverallResult.NOT_APPLICABLE;

        return OverallResult.NEEDS_REVIEW;
    }
}
