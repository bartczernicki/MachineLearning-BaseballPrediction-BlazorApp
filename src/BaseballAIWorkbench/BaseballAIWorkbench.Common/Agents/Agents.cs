namespace BaseballAIWorkbench.Common.Agents
{
    public static class Agents
    {
        public static Agent GetAgent(string agentType)
        {
            return agentType switch
            {
                "BaseballStatistician" => new Agent
                {
                    Name = GetAgentName(agentType),
                    AgentType = agentType,
                    IsSelected = true,
                    Description = GetAgentDescription(agentType),
                    Instructions = GetAgentInstructions(agentType)
                },
                "MachineLearningExpert" => new Agent
                {
                    Name = GetAgentName(agentType),
                    AgentType = agentType,
                    IsSelected = true,
                    Description = GetAgentDescription(agentType),
                    Instructions = GetAgentInstructions(agentType)
                },
                "BaseballEncyclopedia" => new Agent
                {
                    Name = GetAgentName(agentType),
                    AgentType = agentType,
                    IsSelected = true,
                    Description = GetAgentDescription(agentType),
                    Instructions = GetAgentInstructions(agentType)
                }
            };
        }

        public static string GetAgentDescription(string agentType)
        {
            return agentType switch
            {
                "BaseballStatistician" =>
                """
                A dedicated AI agent focused exclusively on the quantitative side of baseball, the Baseball Statistician processes and analyzes player performance data to deliver clear, numbers-driven insights. It has access to a wide range of metrics allowing it to compare players, track statistical trends over time, and answer complex statistical queries. By design, it ignores narrative context, scouting reports, and subjective opinions, ensuring that every response is grounded in hard data and rigorous statistical calculations.
                """,
                "MachineLearningExpert" =>
                """
                An AI agent that harnesses machine learning to dissect and predict baseball player performance. It ingests historical statistics for Machine Learning analysis, and outputs clear probabilistic forecasts. No scouting reports or narrative opinions, just rigorous, ML‑driven insights.
                """,
                "BaseballEncyclopedia" =>
                """
                An AI agent that taps into the internet via Bing to research every angle of a baseball player’s story. It scours news outlets, feature articles, expert opinions, and fan commentary to build comprehensive profiles. Focusing purely on narrative and media sources, it delivers rich context from biographical details to the latest headlines, while intentionally setting aside raw statistical analysis.
                """,
                _ => "Unknown agent"
            };
        }

        public static string GetAgentName(string agentType)
        {
            return agentType switch
            {
                "BaseballStatistician" => "Baseball Statistician",
                "MachineLearningExpert" => "Machine Learning Expert",
                "BaseballEncyclopedia" => "Baseball Encyclopedia",
                _ => "Unknown agent"
            };
        }

        public static string GetAgentInstructions(string agentType)
        {
            return agentType switch
            {
                "BaseballStatistician" =>
                """
                You are Baseball Statistician, a dedicated AI agent focused exclusively on the quantitative side of baseball. 
                You process and analyze player performance data to deliver clear, numbers‑driven insights. 
                You have access to a wide range of metrics, allowing you to compare players, track statistical trends over time, and answer complex statistical queries. 
                By design, you ignore narrative context, scouting reports, and subjective opinions, ensuring that every response is grounded in hard data and rigorous statistical calculations.
                """,
                "MachineLearningExpert" =>
                """
                You are Baseball Machine Learning Expert, a specialist AI agent devoted entirely to the quantitative analysis of baseball. You have two dedicated machine‑learning models at your disposal:
                Hall of Fame Ballot Model — Based on provided batting statistics, forecasts a probability of belonging in the Baseball Hall of Fame Ballot.
                Hall of Fame Induction Model — Based on provided batting statistics, forecasts the probability of a player’s induction.
                """,
                "BaseballEncyclopedia" =>
                """
                You are Baseball Encyclopedia, an AI agent that taps into the internet via Bing to research every angle of a baseball player’s story. You scour news outlets, feature articles, expert opinions, and fan commentary to build comprehensive profiles. Focusing purely on narrative and media sources, you deliver rich context from biographical details to the latest headlines, while intentionally setting aside raw statistical analysis.
                """,
                _ => "Unknown agent"
            };
        }

        public static string GetStatisticsAgentDecisionPrompt(string battingStatistics)
        {
            var decisionPrompt = $"""
            Analyze the following baseball player statistics and provide a detailed analysis of the player's performance. 
            This is a position player, not a pitcher. 
            Determine the likelihood of two things happening: 
            -- The player being on the Hall of Fame ballot (be nominated to be voted on by the BWAA) 
            -- The player being inducted into the Hall of Fame (getting the actual 75% of ballot votes needed) 

            Analysis Output
            -- A concise breakdown of each criterion and how the player measures up. 
            -- A final probability score (0–100%) for both “Ballot Appearance” and “Induction,” with a brief rationale. 
            -- A final qualitative recommendation as well ranging from very unlikely to very likely.
            

            <Batting Statistics>
            Select batting statistics of the player: 
            {battingStatistics}
            </Batting Statistics>

            Important return Markdown ONLY, no other HTML or tic marks etc.
            """;

            return decisionPrompt;
        }

        public static string GetMachineLearningAgentDecisionPrompt(string[] hallOfFameBallotProbabilities, string[] hallOfFameInductionProbabilities)
        {
            var decisionPrompt = $"""
            Analyze the following Machine Learning probabilities from 3 different expert models 
            These probabilities are based on the player's statistics, awards and historical data. 
            You intentionally do not have access to the player's statistics, awards or historical data.
            
            Determine the likelihood of two things happening: 
            -- The player being on the Hall of Fame ballot (be nominated to be voted on by the BWAA) 
            -- The player being inducted into the Hall of Fame (getting the actual 75% of ballot votes needed) 

            Analysis Output
            -- A concise breakdown of based on three different expert ML model probabilities.
            -- A final probability score (0–100%) for both “Ballot Appearance” and “Induction,” with a brief rationale. 
            -- A final qualitative recommendation as well ranging from very unlikely to very likely.
            

            <Batter Probabilities from Different Expert Machine Learning Models>
            3 Hall of Fame Ballot Probabilities: {string.Join(", ", hallOfFameBallotProbabilities)}
            3 Hall of Fame Ballot Induction Probabilities: {string.Join(", ", hallOfFameInductionProbabilities)}
            </Batter Probabilities from Different Expert Machine Learning Models>

            Important return Markdown ONLY, no other HTML or tic marks etc.
            """;

            return decisionPrompt;
        }
    }
}
