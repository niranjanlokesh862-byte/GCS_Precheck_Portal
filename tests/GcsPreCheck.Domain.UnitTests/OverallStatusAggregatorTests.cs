using FluentAssertions;
using GcsPreCheck.Domain.Entities;
using GcsPreCheck.Domain.Enums;
using GcsPreCheck.Domain.Services;
using Xunit;

namespace GcsPreCheck.Domain.UnitTests;

public class OverallStatusAggregatorTests
{
    [Fact]
    public void Aggregate_WhenAnyParamFails_ReturnsFail()
    {
        var results = new List<ParameterCheckResult>
        {
            new() { ParameterCode = "PARAM1", ComparisonStatus = ComparisonStatus.PASS },
            new() { ParameterCode = "PARAM2", ComparisonStatus = ComparisonStatus.FAIL },
            new() { ParameterCode = "PARAM3", ComparisonStatus = ComparisonStatus.NEEDS_REVIEW },
        };

        var overall = OverallStatusAggregator.Aggregate(results);
        overall.Should().Be(OverallResult.FAIL);
    }

    [Fact]
    public void Aggregate_WhenNoFailsButNeedsReviewPresent_ReturnsNeedsReview()
    {
        var results = new List<ParameterCheckResult>
        {
            new() { ParameterCode = "PARAM1", ComparisonStatus = ComparisonStatus.PASS },
            new() { ParameterCode = "PARAM2", ComparisonStatus = ComparisonStatus.NEEDS_REVIEW },
            new() { ParameterCode = "PARAM3", ComparisonStatus = ComparisonStatus.PASS },
        };

        var overall = OverallStatusAggregator.Aggregate(results);
        overall.Should().Be(OverallResult.NEEDS_REVIEW);
    }

    [Fact]
    public void Aggregate_WhenNoFailsOrNeedsReviewButNotVerifiedPresent_ReturnsNotVerified()
    {
        var results = new List<ParameterCheckResult>
        {
            new() { ParameterCode = "PARAM1", ComparisonStatus = ComparisonStatus.PASS },
            new() { ParameterCode = "PARAM2", ComparisonStatus = ComparisonStatus.NOT_VERIFIED },
            new() { ParameterCode = "PARAM3", ComparisonStatus = ComparisonStatus.NOT_APPLICABLE },
        };

        var overall = OverallStatusAggregator.Aggregate(results);
        overall.Should().Be(OverallResult.NOT_VERIFIED);
    }

    [Fact]
    public void Aggregate_WhenAllPassOrNotApplicableWithAtLeastOnePass_ReturnsPass()
    {
        var results = new List<ParameterCheckResult>
        {
            new() { ParameterCode = "PARAM1", ComparisonStatus = ComparisonStatus.PASS },
            new() { ParameterCode = "PARAM2", ComparisonStatus = ComparisonStatus.NOT_APPLICABLE },
            new() { ParameterCode = "PARAM3", ComparisonStatus = ComparisonStatus.PASS },
        };

        var overall = OverallStatusAggregator.Aggregate(results);
        overall.Should().Be(OverallResult.PASS);
    }

    [Fact]
    public void Aggregate_WhenAllParametersAreNotApplicable_ReturnsNotApplicable()
    {
        var results = new List<ParameterCheckResult>
        {
            new() { ParameterCode = "PARAM1", ComparisonStatus = ComparisonStatus.NOT_APPLICABLE },
            new() { ParameterCode = "PARAM2", ComparisonStatus = ComparisonStatus.NOT_APPLICABLE },
        };

        var overall = OverallStatusAggregator.Aggregate(results);
        overall.Should().Be(OverallResult.NOT_APPLICABLE);
    }
}
