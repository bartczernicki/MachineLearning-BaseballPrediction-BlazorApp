using Azure.AI.OpenAI;
using BaseballAIWorkbench.ApiService.Services;
using BaseballAIWorkbench.Common.Agents;
using BaseballAIWorkbench.Common.MachineLearning;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.ML;
using System.ComponentModel;
using System.Globalization;
using System.Text.RegularExpressions;

namespace BaseballAIWorkbench.ApiService
{
    public class AIAgents
    {
        private const string LuceConfidenceIntervalToolName = "calculate_luce_confidence_interval";
        private static readonly double[] DefaultLuceKValues = [0.5, 1.0, 2.0];

        private readonly BaseballDataService _baseballDataService;
        private readonly PredictionEnginePool<MLBBaseballBatter, MLBHOFPrediction> _predictionEnginePool;
        private readonly AzureOpenAIClient _azureOpenAIClient;
        private readonly AzureOpenAIModelOptions _modelOptions;
        private readonly WebIqMcpToolProvider _webIqMcpToolProvider;
        private readonly ILoggerFactory _loggerFactory;

        public AIAgents(PredictionEnginePool<MLBBaseballBatter, MLBHOFPrediction> predictionEngine,
            AzureOpenAIClient azureOpenAIClient,
            AzureOpenAIModelOptions modelOptions,
            WebIqMcpToolProvider webIqMcpToolProvider,
            ILoggerFactory loggerFactory,
            BaseballDataService baseballDataService)
        {
            _predictionEnginePool = predictionEngine;
            _azureOpenAIClient = azureOpenAIClient;
            _modelOptions = modelOptions;
            _webIqMcpToolProvider = webIqMcpToolProvider;
            _loggerFactory = loggerFactory;
            _baseballDataService = baseballDataService;
        }

        public async Task<MLBBaseballBatter> GetPlayerData(string playerID)
        {
            var battingData = await _baseballDataService.GetBaseballData();

            // Set the initial batter to the parameters passed in
            var player = battingData.Where(a => (a.ID == playerID)).FirstOrDefault()!;

            return player;
        }

        public async Task<IResult> GetPlayers()
        {
            var players = await _baseballDataService.GetBaseballData();
            var count = players.Count;
            return TypedResults.Ok(count);
        }

        public async Task<IResult> PerformBaseballPlayerAnalysisML(AgenticAnalysisConfig agenticAnalysisConfig)
        {
            Console.WriteLine("Agentic Analysis...");
            Console.WriteLine("Agentic Analysis Config - Selected Agents: " + string.Join(", ", agenticAnalysisConfig.AgentsToUse));

            var batter = agenticAnalysisConfig.BaseballBatter;
            Console.WriteLine("Agentic Analysis Config - Baseball Player: " + batter.FullPlayerName);

            var agentType = agenticAnalysisConfig.AgentsToUse.FirstOrDefault();
            if (string.IsNullOrWhiteSpace(agentType))
            {
                return TypedResults.Problem("Agent type not found");
            }

            try
            {
                var analysis = await RunAnalysisAgentAsync(agentType, batter);
                return TypedResults.Ok(analysis);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
                return TypedResults.Problem(ex.Message);
            }
        }

        public async Task<IResult> PerformBaseballPlayerAnalysisMupltipleAgents(AgenticAnalysisConfig agenticAnalysisConfig)
        {
            Console.WriteLine("Multi-Agentic Analysis...");
            Console.WriteLine("Multi-Agentic Analysis - Config Selected Agents: " + string.Join(", ", agenticAnalysisConfig.AgentsToUse));

            var batter = agenticAnalysisConfig.BaseballBatter;
            Console.WriteLine("Multi-Agentic Analysis - Config Baseball Player: " + batter.FullPlayerName);

            var agentAnalyses = new List<CompletedAgentAnalysis>();

            try
            {
                foreach (var agentTypeInConfig in agenticAnalysisConfig.AgentsToUse)
                {
                    Console.WriteLine("Agentic Analysis - Agent Type: " + agentTypeInConfig);

                    var analysis = await RunAnalysisAgentAsync(agentTypeInConfig, batter);
                    var agentName = Agents.GetAgentName(agentTypeInConfig);
                    agentAnalyses.Add(new CompletedAgentAnalysis(agentTypeInConfig, agentName, analysis));
                }

                Console.WriteLine("Agentic Analysis - Agent Type: Final Quantitative Analysis");

                var parsedProbabilityAssessments = ParseAgentProbabilityAssessments(agentAnalyses);
                if (parsedProbabilityAssessments.Included.Count == 0)
                {
                    return TypedResults.Problem("No parsable Probability Assessment tables were found in the completed agent analyses.");
                }

                var quantitativeAnalysisAgent = CreateAgent(
                    Agents.GetAgent("QuantitativeAnalysis"),
                    [CreateLuceConfidenceIntervalTool()]);
                var quantitativeAnalysisPrompt =
                    $"""
                    Treat the following completed agent analyses as the chat history referenced by your instructions.

                    <Agent Analyses>
                    {FormatAgentAnalyses(agentAnalyses)}
                    </Agent Analyses>

                    {FormatDeterministicQuantitativeInputs(parsedProbabilityAssessments)}

                    {Agents.GetQuantitativeAnalysisPrompt()}
                    """;

                return TypedResults.Ok(await RunAgentAsync(
                    quantitativeAnalysisAgent,
                    quantitativeAnalysisPrompt,
                    CreateRequiredToolRunOptions(LuceConfidenceIntervalToolName)));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
                return TypedResults.Problem(ex.Message);
            }
        }

        private async Task<string> RunAnalysisAgentAsync(string agentType, MLBBaseballBatter batter)
        {
            return agentType switch
            {
                "MachineLearningExpert" => await RunMachineLearningExpertAsync(batter),
                "BaseballStatistician" => await RunBaseballStatisticianAsync(batter),
                "BaseballEncyclopedia" => await RunBaseballEncyclopediaAsync(batter),
                _ => throw new InvalidOperationException("Agent type not found")
            };
        }

        private async Task<string> RunMachineLearningExpertAsync(MLBBaseballBatter batter)
        {
            var agent = CreateAgent(Agents.GetAgent("MachineLearningExpert"));
            var hallOfFameBallotProbabilities = GetHallOfFameBallotProbabilities(batter);
            var hallOfFameInductionProbabilities = GetHallOfFameInductionProbabilities(batter);
            var hallOfFameBallotAverageProbability = hallOfFameBallotProbabilities.Average();
            var hallOfFameInductionAverageProbability = hallOfFameInductionProbabilities.Average();

            var decisionPrompt = Agents.GetMachineLearningAgentDecisionPrompt(
                hallOfFameBallotProbabilities,
                hallOfFameBallotAverageProbability,
                hallOfFameInductionProbabilities,
                hallOfFameInductionAverageProbability);

            return await RunAgentAsync(agent, decisionPrompt);
        }

        private async Task<string> RunBaseballStatisticianAsync(MLBBaseballBatter batter)
        {
            var agent = CreateAgent(Agents.GetAgent("BaseballStatistician"));
            var battingStatistics = batter.ToStringWithoutFullPlayerName();
            var decisionPrompt = Agents.GetStatisticsAgentDecisionPrompt(battingStatistics);

            return await RunAgentAsync(agent, decisionPrompt);
        }

        private async Task<string> RunBaseballEncyclopediaAsync(MLBBaseballBatter batter)
        {
            await using var webIqTools = await _webIqMcpToolProvider.CreateToolScopeAsync();
            var agent = CreateAgent(Agents.GetAgent("BaseballEncyclopedia"), webIqTools.Tools);
            var decisionPrompt = Agents.GetInternetResearchAgentDecisionPrompt(batter);

            return await RunAgentAsync(agent, decisionPrompt);
        }

        private ChatClientAgent CreateAgent(Agent agentMeta, IReadOnlyList<AITool>? tools = null)
        {
            var chatClient = _azureOpenAIClient.GetChatClient(_modelOptions.DeploymentName).AsIChatClient();

            return tools is { Count: > 0 }
                ? chatClient.AsBuilder()
                    .UseFunctionInvocation(_loggerFactory)
                    .BuildAIAgent(
                        name: agentMeta.AgentType,
                        description: agentMeta.Description,
                        instructions: agentMeta.Instructions,
                        tools: tools.ToList(),
                        loggerFactory: _loggerFactory)
                : chatClient.AsAIAgent(
                    name: agentMeta.AgentType,
                    description: agentMeta.Description,
                    instructions: agentMeta.Instructions);
        }

        private static async Task<string> RunAgentAsync(
            ChatClientAgent agent,
            string prompt,
            ChatClientAgentRunOptions? runOptions = null)
        {
            var response = await agent.RunAsync(prompt, options: runOptions);
            return response.ToString();
        }

        private static ChatClientAgentRunOptions CreateRequiredToolRunOptions(string toolName)
        {
            return new ChatClientAgentRunOptions(new ChatOptions
            {
                ToolMode = ChatToolMode.RequireSpecific(toolName),
                AllowMultipleToolCalls = false,
                Reasoning = new ReasoningOptions
                {
                    Effort = ReasoningEffort.High
                }
            });
        }

        private static AITool CreateLuceConfidenceIntervalTool()
        {
            return AIFunctionFactory.Create(
                (Func<double[], double[], double[]?, LuceConfidenceIntervalResult>)CalculateLuceConfidenceInterval,
                new AIFunctionFactoryOptions
                {
                    Name = LuceConfidenceIntervalToolName,
                    Description = "Calculates deterministic Luce's-choice point estimates and sensitivity ranges for Hall-of-Fame ballot appearance and induction probabilities."
                });
        }

        [Description("Calculates deterministic Luce's-choice point estimates and sensitivity ranges for Hall-of-Fame probability assessments.")]
        private static LuceConfidenceIntervalResult CalculateLuceConfidenceInterval(
            [Description("Prior agent probabilities for the Ballot Appearance outcome, expressed as decimals from 0 to 1.")] double[] ballotAppearanceProbabilities,
            [Description("Prior agent probabilities for the Induction outcome, expressed as decimals from 0 to 1.")] double[] inductionProbabilities,
            [Description("Positive sensitivity k values. Use [0.5, 1.0, 2.0] when no custom sweep is needed.")] double[]? kValues)
        {
            var effectiveKValues = GetEffectiveKValues(kValues);
            var ballotValues = ValidateProbabilityInputs(ballotAppearanceProbabilities, nameof(ballotAppearanceProbabilities));
            var inductionValues = ValidateProbabilityInputs(inductionProbabilities, nameof(inductionProbabilities));

            Console.WriteLine(
                $"AgentQ Tool Invocation - {LuceConfidenceIntervalToolName}: ballotAppearanceProbabilities={FormatDoubleArray(ballotValues)}, inductionProbabilities={FormatDoubleArray(inductionValues)}, kValues={FormatDoubleArray(effectiveKValues)}");

            return new LuceConfidenceIntervalResult(
                CalculateOutcomeConfidenceInterval("Ballot Appearance", ballotValues, effectiveKValues),
                CalculateOutcomeConfidenceInterval("Induction", inductionValues, effectiveKValues),
                effectiveKValues);
        }

        private static LuceOutcomeConfidenceInterval CalculateOutcomeConfidenceInterval(
            string criterion,
            IReadOnlyList<double> probabilities,
            IReadOnlyList<double> kValues)
        {
            var sensitivityValues = kValues
                .Select(k => new LuceSensitivityValue(k, CalculateLuceProbability(probabilities, k)))
                .ToArray();

            var pointEstimate = CalculateLuceProbability(probabilities, 1.0);

            return new LuceOutcomeConfidenceInterval(
                criterion,
                pointEstimate,
                sensitivityValues.Min(value => value.Probability),
                sensitivityValues.Max(value => value.Probability),
                sensitivityValues);
        }

        private static double CalculateLuceProbability(IReadOnlyList<double> probabilities, double k)
        {
            var positiveEvidence = probabilities.Sum(probability => Math.Pow(probability, k));
            var negativeEvidence = probabilities.Sum(probability => Math.Pow(1.0 - probability, k));

            return positiveEvidence / (positiveEvidence + negativeEvidence);
        }

        private static double[] ValidateProbabilityInputs(double[]? probabilities, string parameterName)
        {
            if (probabilities is null || probabilities.Length == 0)
            {
                throw new ArgumentException("At least one probability is required.", parameterName);
            }

            foreach (var probability in probabilities)
            {
                if (!double.IsFinite(probability) || probability is < 0.0 or > 1.0)
                {
                    throw new ArgumentOutOfRangeException(parameterName, "Probabilities must be finite values between 0 and 1.");
                }
            }

            return probabilities;
        }

        private static double[] GetEffectiveKValues(double[]? kValues)
        {
            var effectiveKValues = kValues is { Length: > 0 }
                ? kValues
                    .Where(k => double.IsFinite(k) && k > 0.0)
                    .Distinct()
                    .Order()
                    .ToArray()
                : DefaultLuceKValues;

            return effectiveKValues.Length > 0 ? effectiveKValues : DefaultLuceKValues;
        }

        private static ParsedAgentProbabilityAssessments ParseAgentProbabilityAssessments(
            IReadOnlyList<CompletedAgentAnalysis> agentAnalyses)
        {
            var included = new List<AgentProbabilityAssessment>();
            var omitted = new List<OmittedAgentProbabilityAssessment>();

            foreach (var agentAnalysis in agentAnalyses)
            {
                if (TryParseAgentProbabilityAssessment(agentAnalysis, out var assessment, out var omittedReason))
                {
                    included.Add(assessment);
                }
                else
                {
                    omitted.Add(new OmittedAgentProbabilityAssessment(agentAnalysis.AgentName, omittedReason));
                }
            }

            return new ParsedAgentProbabilityAssessments(included, omitted);
        }

        private static bool TryParseAgentProbabilityAssessment(
            CompletedAgentAnalysis agentAnalysis,
            out AgentProbabilityAssessment assessment,
            out string omittedReason)
        {
            var probabilityAssessmentSection = ExtractProbabilityAssessmentSection(agentAnalysis.Analysis)
                ?? agentAnalysis.Analysis;
            double? ballotAppearanceProbability = null;
            double? inductionProbability = null;

            foreach (var line in probabilityAssessmentSection.Split('\n'))
            {
                var cells = SplitMarkdownTableRow(line);
                if (cells.Length < 2 || IsMarkdownTableSeparator(cells) || IsMarkdownTableHeader(cells))
                {
                    continue;
                }

                var criterion = cells[0];
                if (!TryParseProbabilityValue(cells[1], out var probability))
                {
                    continue;
                }

                if (criterion.Contains("ballot", StringComparison.OrdinalIgnoreCase))
                {
                    ballotAppearanceProbability = probability;
                }
                else if (criterion.Contains("induction", StringComparison.OrdinalIgnoreCase))
                {
                    inductionProbability = probability;
                }
            }

            if (ballotAppearanceProbability.HasValue && inductionProbability.HasValue)
            {
                assessment = new AgentProbabilityAssessment(
                    agentAnalysis.AgentName,
                    ballotAppearanceProbability.Value,
                    inductionProbability.Value);
                omittedReason = string.Empty;
                return true;
            }

            assessment = default!;
            omittedReason = "Could not parse both Ballot Appearance and Induction probabilities.";
            return false;
        }

        private static string? ExtractProbabilityAssessmentSection(string analysis)
        {
            var lines = analysis.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
            var startLine = -1;

            for (var i = 0; i < lines.Length; i++)
            {
                if (lines[i].Trim().Equals("### Probability Assessment", StringComparison.OrdinalIgnoreCase))
                {
                    startLine = i + 1;
                    break;
                }
            }

            if (startLine < 0)
            {
                return null;
            }

            var endLine = lines.Length;
            for (var i = startLine; i < lines.Length; i++)
            {
                if (lines[i].TrimStart().StartsWith("### ", StringComparison.Ordinal))
                {
                    endLine = i;
                    break;
                }
            }

            return string.Join('\n', lines[startLine..endLine]);
        }

        private static string[] SplitMarkdownTableRow(string line)
        {
            var trimmedLine = line.Trim();
            if (!trimmedLine.StartsWith('|') || !trimmedLine.EndsWith('|'))
            {
                return [];
            }

            return trimmedLine
                .Trim('|')
                .Split('|')
                .Select(cell => cell.Trim())
                .ToArray();
        }

        private static bool IsMarkdownTableHeader(IReadOnlyList<string> cells)
        {
            return cells[0].Equals("Criterion", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsMarkdownTableSeparator(IReadOnlyList<string> cells)
        {
            return cells.All(cell => Regex.IsMatch(cell, @"^:?-{3,}:?$"));
        }

        private static bool TryParseProbabilityValue(string probabilityText, out double probability)
        {
            var normalizedProbabilityText = probabilityText.Replace('\u00A0', ' ').Trim();
            var percentMatch = Regex.Match(normalizedProbabilityText, @"(?<value>\d+(?:\.\d+)?)\s*%");

            if (percentMatch.Success
                && double.TryParse(percentMatch.Groups["value"].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var percentValue))
            {
                probability = Math.Clamp(percentValue / 100.0, 0.0, 1.0);
                return true;
            }

            var decimalMatch = Regex.Match(normalizedProbabilityText, @"(?<value>\d+(?:\.\d+)?)");
            if (decimalMatch.Success
                && double.TryParse(decimalMatch.Groups["value"].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var decimalValue))
            {
                probability = Math.Clamp(decimalValue > 1.0 ? decimalValue / 100.0 : decimalValue, 0.0, 1.0);
                return true;
            }

            probability = 0.0;
            return false;
        }

        private static string FormatAgentAnalyses(IReadOnlyList<CompletedAgentAnalysis> agentAnalyses)
        {
            return string.Join(
                Environment.NewLine + Environment.NewLine,
                agentAnalyses.Select(agentAnalysis =>
                    $"### {agentAnalysis.AgentName} Agent Analysis:{Environment.NewLine}{agentAnalysis.Analysis}"));
        }

        private static string FormatDeterministicQuantitativeInputs(ParsedAgentProbabilityAssessments assessments)
        {
            var includedAgentNames = string.Join(", ", assessments.Included.Select(assessment => assessment.AgentName));
            var omittedAgentNames = assessments.Omitted.Count == 0
                ? "None"
                : string.Join(", ", assessments.Omitted.Select(omitted => $"{omitted.AgentName} ({omitted.Reason})"));

            return $"""
                <Deterministic Quantitative Inputs>
                Included agents: {includedAgentNames}
                Omitted agents: {omittedAgentNames}
                Selected agent probability inputs:
                {FormatAgentProbabilityInputTable(assessments.Included)}
                ballotAppearanceProbabilities: {FormatDoubleArray(assessments.Included.Select(assessment => assessment.BallotAppearanceProbability))}
                inductionProbabilities: {FormatDoubleArray(assessments.Included.Select(assessment => assessment.InductionProbability))}
                kValues: {FormatDoubleArray(DefaultLuceKValues)}
                </Deterministic Quantitative Inputs>
                """;
        }

        private static string FormatAgentProbabilityInputTable(IEnumerable<AgentProbabilityAssessment> assessments)
        {
            var tableRows = assessments.Select(assessment =>
                $"| {assessment.AgentName} | {FormatDecimalProbability(assessment.BallotAppearanceProbability)} | {FormatDecimalProbability(assessment.InductionProbability)} | Yes |");

            return string.Join(
                Environment.NewLine,
                [
                    "| Agent | Ballot Appearance Input | Induction Input | Use In Calculation |",
                    "|---|---:|---:|---|",
                    .. tableRows
                ]);
        }

        private static string FormatDoubleArray(IEnumerable<double> values)
        {
            return $"[{string.Join(", ", values.Select(FormatDecimalProbability))}]";
        }

        private static string FormatDecimalProbability(double value)
        {
            return value.ToString("0.####", CultureInfo.InvariantCulture);
        }

        private float[] GetHallOfFameBallotProbabilities(MLBBaseballBatter batter)
        {
            return
            [
                _predictionEnginePool.Predict(MLModelPredictionType.OnHallOfFameBallotGeneralizedAdditiveModel.ToString(), batter).Probability,
                _predictionEnginePool.Predict(MLModelPredictionType.OnHallOfFameBallotLightGbmModel.ToString(), batter).Probability,
                _predictionEnginePool.Predict(MLModelPredictionType.OnHallOfFameBallotFastTreeModel.ToString(), batter).Probability
            ];
        }

        private float[] GetHallOfFameInductionProbabilities(MLBBaseballBatter batter)
        {
            return
            [
                _predictionEnginePool.Predict(MLModelPredictionType.InductedToHallOfFameGeneralizedAdditiveModel.ToString(), batter).Probability,
                _predictionEnginePool.Predict(MLModelPredictionType.InductedToHallOfFameLightGbmModel.ToString(), batter).Probability,
                _predictionEnginePool.Predict(MLModelPredictionType.InductedToHallOfFameFastTreeModel.ToString(), batter).Probability
            ];
        }

        private sealed record CompletedAgentAnalysis(string AgentType, string AgentName, string Analysis);

        private sealed record AgentProbabilityAssessment(
            string AgentName,
            double BallotAppearanceProbability,
            double InductionProbability);

        private sealed record OmittedAgentProbabilityAssessment(string AgentName, string Reason);

        private sealed record ParsedAgentProbabilityAssessments(
            List<AgentProbabilityAssessment> Included,
            List<OmittedAgentProbabilityAssessment> Omitted);

        public sealed record LuceConfidenceIntervalResult(
            LuceOutcomeConfidenceInterval BallotAppearance,
            LuceOutcomeConfidenceInterval Induction,
            double[] KValues);

        public sealed record LuceOutcomeConfidenceInterval(
            string Criterion,
            double PointEstimate,
            double LowerBound,
            double UpperBound,
            LuceSensitivityValue[] SensitivityValues);

        public sealed record LuceSensitivityValue(double K, double Probability);
    }
}
