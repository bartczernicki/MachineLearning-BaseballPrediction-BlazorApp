using System.Text.Json.Serialization;

namespace BaseballAIWorkbench.Common.Agents;

// HTTP contract shared by the API and Razor application. Narrative never supplies these numbers.
public sealed record AgenticAnalysisResponse
{
    public required string AnalysisMarkdown { get; init; }
    public required AgentProbabilityEstimate[] AgentEstimates { get; init; }
    public LuceSensitivityResult? Aggregate { get; init; }
    public string[] Notices { get; init; } = [];
}

public sealed record AgentProbabilityEstimate(
    [property: JsonRequired] string AgentType,
    [property: JsonRequired] string AgentName,
    [property: JsonRequired] double? BallotAppearanceProbability,
    [property: JsonRequired] double? InductionProbability,
    [property: JsonRequired] bool IncludedInAggregate,
    [property: JsonRequired] string? AbstentionReason);

public sealed record LuceSensitivityResult(
    [property: JsonRequired] LuceOutcomeSensitivityResult BallotAppearance,
    [property: JsonRequired] LuceOutcomeSensitivityResult Induction,
    [property: JsonRequired] double[] KValues,
    [property: JsonRequired] int ContributingAgentCount);

public sealed record LuceOutcomeSensitivityResult(
    [property: JsonRequired] string Criterion,
    [property: JsonRequired] double PointEstimate,
    [property: JsonRequired] double SensitivityLowerBound,
    [property: JsonRequired] double SensitivityUpperBound,
    [property: JsonRequired] LuceSensitivityValue[] SensitivityValues,
    [property: JsonRequired] double AgentEstimateMinimum,
    [property: JsonRequired] double AgentEstimateMaximum,
    [property: JsonRequired] double? AgentSpreadPercentagePoints);

public sealed record LuceSensitivityValue(
    [property: JsonRequired] double K, [property: JsonRequired] double Probability);
