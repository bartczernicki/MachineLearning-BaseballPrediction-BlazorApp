using BaseballAIWorkbench.Common.MachineLearning;
using BaseballAIWorkbench.ApiService.Services;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Agents;
using Microsoft.SemanticKernel.ChatCompletion;

namespace BaseballAIWorkbench.ApiService
{
    public class AIAgents
    {
        private readonly BaseballDataService _baseballDataService;
        private readonly Kernel _semanticKernel;

        public AIAgents(Kernel semanticKernel, BaseballDataService baseballDataService)
        {
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

            var agentName = "BaseballStatistician";
            var agentInstructions = """
You are Baseball Statistician, a dedicated AI agent focused exclusively on the quantitative side of baseball. 
You process and analyze player performance data to deliver clear, numbers‑driven insights. 
You have access to a wide range of metrics, allowing you to compare players, track statistical trends over time, and answer complex statistical queries. 
By design, you ignore narrative context, scouting reports, and subjective opinions, ensuring that every response is grounded in hard data and rigorous statistical calculations.
""";
            var agentDescription = """
A dedicated AI agent focused exclusively on the quantitative side of baseball, 
the Baseball Statistician processes and analyzes player performance data to deliver clear, 
numbers-driven insights. It has access to a wide range of metrics allowing it to compare players,
track statistical trends over time, and answer complex statistical queries. 
By design, it ignores narrative context, scouting reports, and subjective opinions, 
ensuring that every response is grounded in hard data and rigorous statistical calculations.
""";

            // STEP 2: Register the agent with the Semantic Kernel. 
            // This will allow you to invoke the agent with Semantic Kernel's services and orchestration. 
            ChatCompletionAgent agent =
                new()
                {
                    Kernel = _semanticKernel,
                    Name = agentName,
                    Description = agentDescription,
                    Instructions = agentInstructions
                };

            // STEP 3: Build the instruction to investigate the decisions the Agent can help with.
            var decisionPrompt = $"""
            Analyze the following baseball player statistics and provide a detailed analysis of the player's performance. 
            This is a position player, not a pitcher. 
            Determine the likelihood of two things happening: 
            -- The player being on the Hall of Fame ballot (be nominated to be voted on by the BWAA) 
            -- The player being inducted into the Hall of Fame (getting the actual 75% of ballot votes needed) 

            Analysis Output
            -- A concise breakdown of each criterion and how the player measures up. 
            -- A probability score (0–100%) for both “Ballot Appearance” and “Induction,” with a brief rationale. 

            A final recommendation: “Likely,” “Borderline,” or “Unlikely” for each outcome.

            <Batting Statistics>
            Select batting statistics of the player: 
            {battingStatistics}
            </Batting Statistics>
            """;
            var chatDecisionMessage = new ChatMessageContent(AuthorRole.User, decisionPrompt);

            var agentResponse = await agent.InvokeAsync(chatDecisionMessage).ToArrayAsync();
            // Convert agentResponse to a string
            var analysis = agentResponse[0].Message.ToString();

            return TypedResults.Ok(analysis);
        }
    }
}