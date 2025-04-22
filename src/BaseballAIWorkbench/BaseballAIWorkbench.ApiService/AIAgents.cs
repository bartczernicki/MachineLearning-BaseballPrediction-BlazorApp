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
using OpenAI.Chat;

namespace BaseballAIWorkbench.ApiService
{
    public class AIAgents
    {
        private readonly BaseballDataService _baseballDataService;
        private readonly Kernel _semanticKernel;
        private readonly AzureOpenAIChatCompletionService _chatCompletionReasoning;
        private readonly PredictionEnginePool<MLBBaseballBatter, MLBHOFPrediction> _predictionEnginePool;
        private readonly DefaultAzureCredential _sharedAzureCredential;

        public AIAgents(DefaultAzureCredential sharedAzureCredential, PredictionEnginePool<MLBBaseballBatter, MLBHOFPrediction> predictionEngine,
                AzureOpenAIChatCompletionService chatCompletionReasoning, Kernel semanticKernel, BaseballDataService baseballDataService)
        {
            _sharedAzureCredential = sharedAzureCredential;
            _predictionEnginePool = predictionEngine;
            _chatCompletionReasoning = chatCompletionReasoning;
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

        //public async Task<IResult> PerformBaseballPlayerAnalysis(MLBBaseballBatter batter)
        //{
        //    var battingStatistics = batter.ToStringWithoutFullPlayerName();

        //    var agentType = "BaseballStatistician";

        //    var agentMeta = Agents.GetAgent(agentType);

        //    // STEP 2: Register the agent with the Semantic Kernel. 
        //    // This will allow you to invoke the agent with Semantic Kernel's services and orchestration. 
        //    ChatCompletionAgent agent =
        //        new()
        //        {
        //            Kernel = _semanticKernel,
        //            Name = agentMeta.AgentType, // Ensure no spaces or it will fail
        //            Description = agentMeta.Description,
        //            Instructions = agentMeta.Instructions
        //        };

        //    // STEP 3: Build the instruction to investigate the decisions the Agent can help with.
        //    var decisionPrompt = Agents.GetStatisticsAgentDecisionPrompt(battingStatistics);

        //    // STEP 4: Create the ChatMessageContent object with the decision prompt.
        //    var chatDecisionMessage = new ChatMessageContent(AuthorRole.User, decisionPrompt);

        //    var agentResponse = await agent.InvokeAsync(chatDecisionMessage).ToArrayAsync();
        //    // Convert agentResponse to a string
        //    var analysis = agentResponse[0].Message.ToString();

        //    return TypedResults.Ok(analysis);
        //}

        public async Task<IResult> PerformBaseballPlayerAnalysisML(AgenticAnalysisConfig agenticAnalysisConfig)
        {
#pragma warning disable SKEXP0110
            Console.WriteLine("Agentic Analysis...");
            Console.WriteLine("Agentic Analysis Config - Selected Agents: " + string.Join(", ", agenticAnalysisConfig.AgentsToUse));

            var batter = agenticAnalysisConfig.BaseballBatter;
            Console.WriteLine("Agentic Analysis Config - Baseball Player: " + batter.FullPlayerName);
            
            var decisionPrompt = string.Empty;
            var agentType = agenticAnalysisConfig.AgentsToUse.FirstOrDefault();
            var agentMeta = Agents.GetAgent(agentType!);
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

                // STEP 4: Create the ChatMessageContent object with the decision prompt.
                var chatDecisionMessage = new Microsoft.SemanticKernel.ChatMessageContent(AuthorRole.User, decisionPrompt);

                var agentResponse = await agent.InvokeAsync(chatDecisionMessage).ToArrayAsync();
                // Convert agentResponse to a string
                var analysis = agentResponse[0].Message.ToString();

                return TypedResults.Ok(analysis);
            }
            else if (agentType == "BaseballStatistician")
            {
                var battingStatistics = batter.ToStringWithoutFullPlayerName();
                // STEP 3: Build the instruction to investigate the decisions the Agent can help with.
                decisionPrompt = Agents.GetStatisticsAgentDecisionPrompt(battingStatistics);

                // STEP 4: Create the ChatMessageContent object with the decision prompt.
                var chatDecisionMessage = new Microsoft.SemanticKernel.ChatMessageContent(AuthorRole.User, decisionPrompt);

                var agentResponse = await agent.InvokeAsync(chatDecisionMessage).ToArrayAsync();
                // Convert agentResponse to a string
                var analysis = agentResponse[0].Message.ToString();

                return TypedResults.Ok(analysis);
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
                decisionPrompt = Agents.GetInternetResearchAgentDecisionPrompt(batter);
                Microsoft.SemanticKernel.ChatMessageContent message = new(AuthorRole.User, decisionPrompt);
                Microsoft.SemanticKernel.Agents.AgentThread agentThread = new AzureAIAgentThread(agentsClient);
                try
                {
                    await foreach (Microsoft.SemanticKernel.ChatMessageContent chatResponse in baseballEncyclopediaAgent.InvokeAsync(message, agentThread))
                    {
                        Console.WriteLine(chatResponse.Content);
                        response += chatResponse.Content;
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error: {ex.Message}");
                    return TypedResults.Problem(ex.Message);
                }
                finally
                {
                    if (!agentThread.IsDeleted)
                    {
                        // Delete the agent thread if it was created and not deleted
                        await agentThread.DeleteAsync();
                    }
                    //await agentsClient.DeleteAgentAsync(agentThread.Id);
                }

                return TypedResults.Ok(response);
            }
            else
            {
                return TypedResults.Problem("Agent type not found");
            }
        }

        public async Task<IResult> PerformBaseballPlayerAnalysisMupltipleAgents(AgenticAnalysisConfig agenticAnalysisConfig)
        {

            //var chatCompletionServicetest = _semanticKernelReasoning.Services.GetRequiredService<IChatCompletionService>();

#pragma warning disable SKEXP0110
            Console.WriteLine("Agentic Analysis...");
            Console.WriteLine("Agentic Analysis - Config Selected Agents: " + string.Join(", ", agenticAnalysisConfig.AgentsToUse));

            var batter = agenticAnalysisConfig.BaseballBatter;
            Console.WriteLine("Agentic Analysis - Config Baseball Player: " + batter.FullPlayerName);

            // Set Up Chat History
            ChatHistory agentsAnalysisHistory = [];

            foreach (var agentTypeInConfig in agenticAnalysisConfig.AgentsToUse)
            {
                Console.WriteLine("Agentic Analysis - Agent Type: " + agentTypeInConfig);

                var decisionPrompt = string.Empty;
                var agentMeta = Agents.GetAgent(agentTypeInConfig!);
                ChatCompletionAgent agent = agentMeta.GetChatCompletionAgent(_semanticKernel);

                // STEP 4: Build the instruction to investigate the decisions the Agent can help with.
                if (agentTypeInConfig == "MachineLearningExpert")
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

                    // STEP 4: Create the ChatMessageContent object with the decision prompt.
                    var chatDecisionMessage = new Microsoft.SemanticKernel.ChatMessageContent(AuthorRole.User, decisionPrompt);

                    var agentResponse = await agent.InvokeAsync(chatDecisionMessage).ToArrayAsync();
                    //// Convert agentResponse to a string
                    var analysis = agentResponse[0].Message.ToString();

                    // Add the analysis to the agents analysis history
                    agentsAnalysisHistory.AddAssistantMessage(Environment.NewLine + "## Machine Learning Expert Agent Analysis: " + Environment.NewLine + analysis);
                }
                else if (agentTypeInConfig == "BaseballStatistician")
                {
                    var battingStatistics = batter.ToStringWithoutFullPlayerName();
                    // STEP 3: Build the instruction to investigate the decisions the Agent can help with.
                    decisionPrompt = Agents.GetStatisticsAgentDecisionPrompt(battingStatistics);

                    // STEP 4: Create the ChatMessageContent object with the decision prompt.
                    var chatDecisionMessage = new Microsoft.SemanticKernel.ChatMessageContent(AuthorRole.User, decisionPrompt);

                    var agentResponse = await agent.InvokeAsync(chatDecisionMessage).ToArrayAsync();
                    //// Convert agentResponse to a string
                    var analysis = agentResponse[0].Message.ToString();
                    // Add the analysis to the agents analysis history
                    agentsAnalysisHistory.AddAssistantMessage(Environment.NewLine + "## Baseball Statistician Agent Analysis: " + Environment.NewLine + analysis);
                }
                else if (agentTypeInConfig == "BaseballEncyclopedia")
                {
                    // Build Azure AI Agent Connection
                    var projectConnectionString = Environment.GetEnvironmentVariable("ConnectionStrings__AzureAIFoundryProject");
                    var clientOptions = new AIProjectClientOptions();
                    AIProjectClient projectClient = new AIProjectClient(projectConnectionString, _sharedAzureCredential); //, new DefaultAzureCredential(), clientOptions);
                    AgentsClient agentsClient = projectClient.GetAgentsClient();

                    var baseballEncyclopediaAgentDefinition = agentsClient.GetAgent("asst_fVoQfqtvwLZKdQM32ZZstzHQ");
                    AzureAIAgent baseballEncyclopediaAgent = new(baseballEncyclopediaAgentDefinition, agentsClient);

                    var analysis = string.Empty;
                    decisionPrompt = Agents.GetInternetResearchAgentDecisionPrompt(batter);
                    Microsoft.SemanticKernel.ChatMessageContent message = new(AuthorRole.User, decisionPrompt);
                    Microsoft.SemanticKernel.Agents.AgentThread agentThread = new AzureAIAgentThread(agentsClient);
                    try
                    {
                        await foreach (Microsoft.SemanticKernel.ChatMessageContent chatResponse in baseballEncyclopediaAgent.InvokeAsync(message, agentThread))
                        {
                            Console.WriteLine(chatResponse.Content);
                            analysis += chatResponse.Content;
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error: {ex.Message}");
                        return TypedResults.Problem(ex.Message);
                    }
                    finally
                    {
                        // Add the analysis to the agents analysis history
                        agentsAnalysisHistory.AddAssistantMessage(Environment.NewLine + "Baseball Encyclopedia Agent Analysis: " + Environment.NewLine + analysis);

                        if (agentThread.Id != null && !agentThread.IsDeleted)
                        {
                            // Delete the agent thread if it was created and not deleted
                            await agentThread.DeleteAsync();
                        }
                        //await agentsClient.DeleteAgentAsync(agentThread.Id);
                    }
                }
                else
                {
                    return TypedResults.Problem("Agent type not found");
                }
            } // End of Agents foreach

            // Convert ChatHistory to a string
            // var agentsAnalysisHistoryString = string.Join(Environment.NewLine, agentsAnalysisHistory.Select(a => a.Content));

            Console.WriteLine("Agentic Analysis - Final Quantitative Analysis");

            //agentsAnalysisHistory.AddSystemMessage(Agents.GetAgentInstructions("QuantitativeAnalysis"));
            //var openAIPromptExecutionSettings = new AzureOpenAIPromptExecutionSettings()
            //{
            //    ReasoningEffort = ChatReasoningEffortLevel.High
            //};
            //agentsAnalysisHistory.AddUserMessage(Agents.GetQuantitativeAnalysisPrompt());
            ////var chatCompletionService = _semanticKernelReasoning.Services.GetRequiredService<IChatCompletionService>();
            //var quantAnalysisResponse = await _chatCompletionReasoning.GetChatMessageContentAsync(agentsAnalysisHistory, openAIPromptExecutionSettings);

            var chatMessageContent = new Microsoft.SemanticKernel.ChatMessageContent(AuthorRole.User, Agents.GetQuantitativeAnalysisPrompt());
            var quantitativeAnalysisAgent = Agents.GetAgent("QuantitativeAnalysis");

            ChatCompletionAgent quantAgent = quantitativeAnalysisAgent.GetChatCompletionAgent(_semanticKernel);

            Microsoft.SemanticKernel.Agents.AgentThread quantAgentThread = new ChatHistoryAgentThread(agentsAnalysisHistory);
            var quantitativeAnalysisResponse = await quantAgent.InvokeAsync(chatMessageContent, quantAgentThread).ToArrayAsync();
            var quantitativeAnalysisString = quantitativeAnalysisResponse[0].Message.ToString();

            return TypedResults.Ok(quantitativeAnalysisString);
        }
    }
}