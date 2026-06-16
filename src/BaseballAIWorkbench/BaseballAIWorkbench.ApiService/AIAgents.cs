using Azure.AI.OpenAI;
using BaseballAIWorkbench.ApiService.Services;
using BaseballAIWorkbench.Common.Agents;
using BaseballAIWorkbench.Common.MachineLearning;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.ML;

namespace BaseballAIWorkbench.ApiService
{
    public class AIAgents
    {
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

            var agentsAnalysisHistory = new List<string>();

            try
            {
                foreach (var agentTypeInConfig in agenticAnalysisConfig.AgentsToUse)
                {
                    Console.WriteLine("Agentic Analysis - Agent Type: " + agentTypeInConfig);

                    var analysis = await RunAnalysisAgentAsync(agentTypeInConfig, batter);
                    var agentName = Agents.GetAgentName(agentTypeInConfig);
                    agentsAnalysisHistory.Add($"### {agentName} Agent Analysis:{Environment.NewLine}{analysis}");
                }

                Console.WriteLine("Agentic Analysis - Agent Type: Final Quantitative Analysis");

                var quantitativeAnalysisAgent = CreateAgent(Agents.GetAgent("QuantitativeAnalysis"));
                var quantitativeAnalysisPrompt =
                    $"""
                    Treat the following completed agent analyses as the chat history referenced by your instructions.

                    <Agent Analyses>
                    {string.Join(Environment.NewLine + Environment.NewLine, agentsAnalysisHistory)}
                    </Agent Analyses>

                    {Agents.GetQuantitativeAnalysisPrompt()}
                    """;

                return TypedResults.Ok(await RunAgentAsync(quantitativeAnalysisAgent, quantitativeAnalysisPrompt));
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

        private static async Task<string> RunAgentAsync(ChatClientAgent agent, string prompt)
        {
            var response = await agent.RunAsync(prompt);
            return response.ToString();
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
    }
}
