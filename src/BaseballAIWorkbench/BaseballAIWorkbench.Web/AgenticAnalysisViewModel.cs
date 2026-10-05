using BaseballAIWorkbench.Common.Agents;

namespace BaseballAIWorkbench.Web;

// Keep trusted numeric fields distinct from the sanitized, model-generated explanation.
public sealed record AgenticAnalysisViewModel(AgenticAnalysisResponse Response, string NarrativeHtml);
