namespace BaseballAIWorkbench.Common.Agents;

// Strict model-response contract; the public HTTP response is AgenticAnalysisResponse.
public sealed record AgentAnalysisResult
{
    public required string AnalysisMarkdown { get; init; }
    public required double? BallotAppearanceProbability { get; init; }
    public required double? InductionProbability { get; init; }
    public required string? AbstentionReason { get; init; }
}
