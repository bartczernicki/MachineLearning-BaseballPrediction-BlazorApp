using BaseballAIWorkbench.Common.MachineLearning;
using BaseballAIWorkbench.ApiService.Services;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Agents;
using Microsoft.SemanticKernel.ChatCompletion;
using BaseballAIWorkbench.Common.Agents;
using Microsoft.Extensions.ML;
using Azure.Identity;
using Azure.AI.Projects;
using Microsoft.SemanticKernel.Connectors.AzureOpenAI;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using Microsoft.SemanticKernel.Agents.AzureAI;
using Microsoft.Extensions.Azure;

namespace BaseballAIWorkbench.ApiService
{
    public class AIAgents
    {
        private readonly BaseballDataService _baseballDataService;
        private readonly Kernel _semanticKernel;
        private readonly PredictionEnginePool<MLBBaseballBatter, MLBHOFPrediction> _predictionEnginePool;
        private readonly DefaultAzureCredential _sharedAzureCredential;

        public AIAgents(DefaultAzureCredential sharedAzureCredential, PredictionEnginePool<MLBBaseballBatter, MLBHOFPrediction> predictionEngine, Kernel semanticKernel, BaseballDataService baseballDataService)
        {
            _sharedAzureCredential = sharedAzureCredential;
            _predictionEnginePool = predictionEngine;
            _semanticKernel = semanticKernel;
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

        public async Task<IResult> PerformBaseballPlayerAnalysis(MLBBaseballBatter batter)
        {
            var battingStatistics = batter.ToStringWithoutFullPlayerName();

            var agentType = "BaseballStatistician";

            var agentMeta = Agents.GetAgent(agentType);

            // STEP 2: Register the agent with the Semantic Kernel. 
            // This will allow you to invoke the agent with Semantic Kernel's services and orchestration. 
            ChatCompletionAgent agent =
                new()
                {
                    Kernel = _semanticKernel,
                    Name = agentMeta.AgentType, // Ensure no spaces or it will fail
                    Description = agentMeta.Description,
                    Instructions = agentMeta.Instructions
                };

            // STEP 3: Build the instruction to investigate the decisions the Agent can help with.
            var decisionPrompt = Agents.GetStatisticsAgentDecisionPrompt(battingStatistics);

            // STEP 4: Create the ChatMessageContent object with the decision prompt.
            var chatDecisionMessage = new ChatMessageContent(AuthorRole.User, decisionPrompt);

            var agentResponse = await agent.InvokeAsync(chatDecisionMessage).ToArrayAsync();
            // Convert agentResponse to a string
            var analysis = agentResponse[0].Message.ToString();

            return TypedResults.Ok(analysis);
        }

        public async Task<IResult> PerformBaseballPlayerAnalysisML(AgenticAnalysisConfig agenticAnalysisConfig)
        {
#pragma warning disable SKEXP0110
            Console.WriteLine("Agentic Analysis...");
            Console.WriteLine("Agentic Analysis Config: Selected Agents: " + string.Join(", ", agenticAnalysisConfig.AgentsToUse));

            var batter = agenticAnalysisConfig.BaseballBatter;

            var agentType = agenticAnalysisConfig.AgentsToUse.FirstOrDefault();
            var agentMeta = Agents.GetAgent(agentType);
            var decisionPrompt = string.Empty;

            // STEP 3: Register the agent with the Semantic Kernel. 
            // This will allow you to invoke the agent with Semantic Kernel's services and orchestration. 
            ChatCompletionAgent agent = agentMeta.GetChatCompletionAgent(_semanticKernel);
            // STEP 4: Build the instruction to investigate the decisions the Agent can help with.
            if (agentType == "MachineLearningExpert")
            {
                // Run the prediction engine to get the prediction
                var onHallOfFameBallotPredictionModel1 = _predictionEnginePool.Predict(Common.MachineLearning.MLModelPredictionType.OnHallOfFameBallotGeneralizedAdditiveModel.ToString(),
                    batter);
                var inductedToHallOfFamePredictionModel1 = _predictionEnginePool.Predict(Common.MachineLearning.MLModelPredictionType.InductedToHallOfFameGeneralizedAdditiveModel.ToString(),
                    batter);
                var onHallOfFameBallotPredictionModel2 = _predictionEnginePool.Predict(Common.MachineLearning.MLModelPredictionType.OnHallOfFameBallotLightGbmModel.ToString(),
                    batter);
                var inductedToHallOfFamePredictionModel2 = _predictionEnginePool.Predict(Common.MachineLearning.MLModelPredictionType.InductedToHallOfFameLightGbmModel.ToString(),
                    batter);
                var onHallOfFameBallotPredictionModel3 = _predictionEnginePool.Predict(Common.MachineLearning.MLModelPredictionType.OnHallOfFameBallotFastTreeModel.ToString(),
                    batter);
                var inductedToHallOfFamePredictionModel3 = _predictionEnginePool.Predict(Common.MachineLearning.MLModelPredictionType.InductedToHallOfFameFastTreeModel.ToString(),
                    batter);
                // Convert ML Probabilities into a string array
                string[] onHallOfFameBallotProbabilities = {
                    onHallOfFameBallotPredictionModel1.Probability.ToString(),
                    onHallOfFameBallotPredictionModel2.Probability.ToString(),
                    onHallOfFameBallotPredictionModel3.Probability.ToString()
                };
                string[] inductedToHallOfFameProbabilities = {
                    inductedToHallOfFamePredictionModel1.Probability.ToString(),
                    inductedToHallOfFamePredictionModel2.Probability.ToString(),
                    inductedToHallOfFamePredictionModel3.Probability.ToString()
                };

                // STEP 2: Build the decision prompt to investigate the decisions the Agent can help with.
                decisionPrompt = Agents.GetMachineLearningAgentDecisionPrompt(
                    onHallOfFameBallotProbabilities, inductedToHallOfFameProbabilities);
            }
            else if (agentType == "BaseballStatistician")
            {
                var battingStatistics = batter.ToStringWithoutFullPlayerName();
                // STEP 3: Build the instruction to investigate the decisions the Agent can help with.
                decisionPrompt = Agents.GetStatisticsAgentDecisionPrompt(battingStatistics);
            }
            else if (agentType == "BaseballEncyclopedia")
            {
                // Build Azure AI Agent Connection
                var projectConnectionString = Environment.GetEnvironmentVariable("ConnectionStrings__AzureAIFoundryProject");
                var clientOptions = new AIProjectClientOptions();
                AIProjectClient projectClient = new AIProjectClient(projectConnectionString, _sharedAzureCredential); //, new DefaultAzureCredential(), clientOptions);
                AgentsClient agentsClient = projectClient.GetAgentsClient();

                var baseballEncyclopediaAgentDefinition = agentsClient.GetAgent("asst_fVoQfqtvwLZKdQM32ZZstzHQ");
                AzureAIAgent baseballEncyclopediaAgent = new(baseballEncyclopediaAgentDefinition, agentsClient);

                var response = string.Empty;
                ChatMessageContent message = new(AuthorRole.User,
                    $"Perform MLB Baseball Hall of Fame Ballot research on: {batter.FullPlayerName}. Respond in Markdown only.");
                Microsoft.SemanticKernel.Agents.AgentThread agentThread = new AzureAIAgentThread(agentsClient);
                try
                {
                    await foreach (ChatMessageContent chatResponse in baseballEncyclopediaAgent.InvokeAsync(message, agentThread))
                    {
                        Console.WriteLine(chatResponse.Content);
                        response += chatResponse.Content;
                    }
                }
                finally
                {
                    await agentThread.DeleteAsync();
                    //await agentsClient.DeleteAgentAsync(agentThread.Id);
                }
                //// Native
                //Azure.AI.Projects.AgentThread thread = await agentsClient.CreateThreadAsync();

                //// Create message to thread
                //ThreadMessage message = agentsClient.CreateMessage(
                //    thread.Id,
                //    MessageRole.User,
                //    $"Perform MLB Baseball Hall of Fame Ballot research on: {batter.FullPlayerName}. Respond in Markdown only.");
                //ThreadRun run = agentsClient.CreateRun(thread, baseballEncyclopediaAgent);



                return TypedResults.Ok(response);
            }

            // STEP 4: Create the ChatMessageContent object with the decision prompt.
            var chatDecisionMessage = new ChatMessageContent(AuthorRole.User, decisionPrompt);

            var agentResponse = await agent.InvokeAsync(chatDecisionMessage).ToArrayAsync();
            // Convert agentResponse to a string
            var analysis = agentResponse[0].Message.ToString();

            return TypedResults.Ok(analysis);
        }
    }
}