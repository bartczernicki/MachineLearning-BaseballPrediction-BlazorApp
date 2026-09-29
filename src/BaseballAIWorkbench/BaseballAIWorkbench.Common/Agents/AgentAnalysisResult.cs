namespace BaseballAIWorkbench.Common.Agents;

// Internal analysis data; HTTP endpoints continue to return the Markdown string.
public sealed record AgentAnalysisResult
{
    public required string AnalysisMarkdown { get; init; }
    public required double? BallotAppearanceProbability { get; init; }
    public required double? InductionProbability { get; init; }
    public required string? AbstentionReason { get; init; }
}
