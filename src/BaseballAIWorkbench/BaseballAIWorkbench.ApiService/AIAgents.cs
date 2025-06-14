﻿using Azure.AI.Agents.Persistent;
using Azure.Identity;
using BaseballAIWorkbench.ApiService.Services;
using BaseballAIWorkbench.Common.Agents;
using BaseballAIWorkbench.Common.MachineLearning;
using Microsoft.Extensions.ML;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Agents;
using Microsoft.SemanticKernel.Agents.AzureAI;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.AzureOpenAI;

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
                var projectConnectionStringUri = new Uri(projectConnectionString!);
                var agentAdminClientOptions = 
                    new PersistentAgentsAdministrationClientOptions(PersistentAgentsAdministrationClientOptions.ServiceVersion.V2025_05_01);
                var agentsClient = 
                    new PersistentAgentsClient(projectConnectionString, _sharedAzureCredential, agentAdminClientOptions);
                
                var azureAIFoundrySportsAgentID = Environment.GetEnvironmentVariable("ConnectionStrings__AzureAIFoundrySportsAgentID");
                var baseballEncyclopediaPersistentAgent = agentsClient.Administration.GetAgent(azureAIFoundrySportsAgentID).Value;

                //ConnectionID in this format: /subscriptions/<subscription_id>/resourceGroups/<resource_group_name>/providers/Microsoft.CognitiveServices/accounts/<ai_service_name>/projects/<project_name>/connections/<connection_name> 
                var bingWebGroundingConnectionID = Environment.GetEnvironmentVariable("ConnectionStrings__BingWebGroundingConnectionID");

                var bingSearchConfig = new BingGroundingSearchConfiguration(bingWebGroundingConnectionID)
                {
                    Count = 10,
                    Market = "en-US"
                };

                BingGroundingToolDefinition bingGroundingTool = new BingGroundingToolDefinition(
        new BingGroundingSearchToolParameters(
                            [
                                bingSearchConfig
                            ])
                    );

                // Use Azure AI Data Zone Deployment (dz-) prefix for the model deployment name
                var modelDeploymentName = "dz-" + Environment.GetEnvironmentVariable("ConnectionStrings__AOAIModelDeploymentName");

                // Copy key settings to the new transient agent
                PersistentAgent transientAzureAIFoundryAgent = agentsClient.Administration.CreateAgent(
                    model: modelDeploymentName,
                    name: "transientSportsAgent",
                    instructions: baseballEncyclopediaPersistentAgent.Instructions,
                    tools: [bingGroundingTool],
                    temperature: baseballEncyclopediaPersistentAgent.Temperature,
                    topP: baseballEncyclopediaPersistentAgent.TopP
                );

                AzureAIAgent transientSemanticKernelAgent = new(transientAzureAIFoundryAgent, agentsClient);

                var response = string.Empty;
                decisionPrompt = Agents.GetInternetResearchAgentDecisionPrompt(batter);

                // Create the Thread
                AzureAIAgentThread agentSemanticKernelThread = new(agentsClient);
                // Create the ChatMessageContent object with the decision prompt.
                ChatMessageContent message = new(AuthorRole.User, decisionPrompt);

                try
                {
                    await foreach (ChatMessageContent agentResponse in transientSemanticKernelAgent.InvokeAsync(message, agentSemanticKernelThread))
                    {
                        response += agentResponse.Content;
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error: {ex.Message}");
                    return TypedResults.Problem(ex.Message);
                }
                finally
                {
                    // Remove the Thread and the AI Agent
                    await agentsClient.Threads.DeleteThreadAsync(agentSemanticKernelThread.Id);
                    await agentsClient.Administration.DeleteAgentAsync(transientAzureAIFoundryAgent.Id);
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
            Console.WriteLine("Multi-Agentic Analysis...");
            Console.WriteLine("Multi-Agentic Analysis - Config Selected Agents: " + string.Join(", ", agenticAnalysisConfig.AgentsToUse));

            var batter = agenticAnalysisConfig.BaseballBatter;
            Console.WriteLine("Multi-Agentic Analysis - Config Baseball Player: " + batter.FullPlayerName);

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
                    var projectConnectionStringUri = new Uri(projectConnectionString!);
                    var agentAdminClientOptions =
                        new PersistentAgentsAdministrationClientOptions(PersistentAgentsAdministrationClientOptions.ServiceVersion.V2025_05_01);
                    var agentsClient =
                        new PersistentAgentsClient(projectConnectionString, _sharedAzureCredential, agentAdminClientOptions);
                    var azureAIFoundrySportsAgentID = Environment.GetEnvironmentVariable("ConnectionStrings__AzureAIFoundrySportsAgentID");
                    var baseballEncyclopediaPersistentAgent = agentsClient.Administration.GetAgent(azureAIFoundrySportsAgentID).Value;

                    //ConnectionID in this format: /subscriptions/<subscription_id>/resourceGroups/<resource_group_name>/providers/Microsoft.CognitiveServices/accounts/<ai_service_name>/projects/<project_name>/connections/<connection_name> 
                    var bingWebGroundingConnectionID = Environment.GetEnvironmentVariable("ConnectionStrings__BingWebGroundingConnectionID");

                    var bingSearchConfig = new BingGroundingSearchConfiguration(bingWebGroundingConnectionID)
                    {
                        Count = 10,
                        Market = "en-US"
                    };

                    BingGroundingToolDefinition bingGroundingTool = new BingGroundingToolDefinition(
            new BingGroundingSearchToolParameters(
                                [
                                    bingSearchConfig
                                ])
                        );

                    // Use Azure AI Data Zone Deployment (dz-) prefix for the model deployment name
                    var modelDeploymentName = "dz-" + Environment.GetEnvironmentVariable("ConnectionStrings__AOAIModelDeploymentName");

                    // Copy key settings to the new transient agent
                    PersistentAgent transientAzureAIFoundryAgent = agentsClient.Administration.CreateAgent(
                        model: modelDeploymentName,
                        name: "transientSportsAgent",
                        instructions: baseballEncyclopediaPersistentAgent.Instructions,
                        tools: [bingGroundingTool],
                        temperature: baseballEncyclopediaPersistentAgent.Temperature,
                        topP: baseballEncyclopediaPersistentAgent.TopP
                    );

                    AzureAIAgent transientSemanticKernelAgent = new(transientAzureAIFoundryAgent, agentsClient);

                    var response = string.Empty;
                    decisionPrompt = Agents.GetInternetResearchAgentDecisionPrompt(batter);

                    var analysis = string.Empty;
                    // Create the Thread
                    AzureAIAgentThread agentSemanticKernelThread = new(agentsClient);
                    // Create the ChatMessageContent object with the decision prompt.
                    ChatMessageContent message = new(AuthorRole.User, decisionPrompt);

                    try
                    {
                        await foreach (ChatMessageContent agentResponse in transientSemanticKernelAgent.InvokeAsync(message, agentSemanticKernelThread))
                        {
                            analysis += agentResponse.Content;
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

                        await agentsClient.Threads.DeleteThreadAsync(agentSemanticKernelThread.Id);
                        await agentsClient.Administration.DeleteAgentAsync(transientSemanticKernelAgent.Id);
                    }
                }
                else
                {
                    return TypedResults.Problem("Agent type not found");
                }
            } // End of Agents foreach

            // Convert ChatHistory to a string
            // var agentsAnalysisHistoryString = string.Join(Environment.NewLine, agentsAnalysisHistory.Select(a => a.Content));

            Console.WriteLine("Agentic Analysis - Agent Type: Final Quantitative Analysis");

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