using BaseballAIWorkbench.Common.MachineLearning;

namespace BaseballAIWorkbench.Common.Agents
{
    public static class Agents
    {
        public static Agent GetAgent(string agentType)
        {
#pragma warning disable CS8509 // The switch expression does not handle all possible values of its input type (it is not exhaustive).
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
                },
                "QuantitativeAnalysis" => new Agent
                {
                    Name = GetAgentName(agentType),
                    AgentType = agentType,
                    IsSelected = true,
                    Description = GetAgentDescription(agentType),
                    Instructions = GetAgentInstructions(agentType)
                }
            };
#pragma warning restore CS8509 // The switch expression does not handle all possible values of its input type (it is not exhaustive).
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
                An AI agent that harnesses multiple expert machine learning models to dissect and predict baseball player performance. It ingests historical statistics for Machine Learning analysis, and outputs clear probabilistic forecasts. No scouting reports or narrative opinions, just rigorous, ML‑driven insights.
                """,
                "BaseballEncyclopedia" =>
                """
                An AI agent that taps into the internet via Bing to research every angle of a baseball player’s story. It scours news outlets, feature articles, expert opinions, and fan commentary to build comprehensive profiles. Focusing purely on narrative and media sources, it delivers rich context from biographical details to the latest headlines, while intentionally setting aside raw statistical analysis.
                """,
                "QuantitativeAnalysis" =>
                """
                You are an AI “Meta‑Decision Agent”.
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
                "QuantitativeAnalysis" => "Quantitative Analysis",
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
                "QuantitativeAnalysis" =>
                """
                You are an AI “Meta‑Analyst” whose job is to read up to three expert analyses 
                already present in this chat history: 
                one based on historical statistics (“Baseball Statistician Agent”), 
                one on machine‑learning model probabilities (“Machine Learning Expert Agent”), 
                and one on internet research (“Baseball Encyclopedia Agent”).

                Not all three agents may be present, so do not include them if not in the Chat History.
                """,
                _ => "Unknown agent"
            };
        }

        public static string GetInternetResearchAgentDecisionPrompt(MLBBaseballBatter baseballBatter)
        {
            var decisionPrompt = $"""
            Use internet knowledge only. 
            The player being analyzed position player, not a pitcher. 

            Determine the likelihood of two things happening: 
            -- The player being on the Hall of Fame ballot (be nominated to be voted on by the BWAA) 
            -- The player being inducted into the Hall of Fame (getting the actual 75% of ballot votes needed) 

            Even though the player may have played in the past, respond as if the information is forward looking. 

            Analysis Output
            -- A concise research of the Baseball Player  
            -- A probabilistic recommendation (0–100%) for both “Ballot Appearance” and “Induction,” with a brief rationale. 
            -- A qualitative recommendation as well ranging from very unlikely to very likely.
            -- If not good information has been found, it is likely the player has not made a name for himself. 

            <Baseball Player>
            Player to Research
            {baseballBatter.FullPlayerName}, ID: {baseballBatter.ID}, Last Year Played: {baseballBatter.LastYearPlayed}
            </Baseball Player>

            Important return Markdown ONLY, no other HTML or tic marks etc.  
            Formatting rule: only use '###' (level 3) or smaller headings. Do not use '#' (level 1) nor '##' (level 2).
            """;

            return decisionPrompt;
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
            -- A probabilistic recommendation (0–100%) for both “Ballot Appearance” and “Induction,” with a brief rationale. 
            -- A qualitative recommendation as well ranging from very unlikely to very likely.
            

            <Batting Statistics>
            Select batting statistics of the player: 
            {battingStatistics}
            </Batting Statistics>

            Important return Markdown ONLY, no other HTML or tic marks etc.
            Formatting rule: only use '###' (level 3) or smaller headings. Do not use '#' (level 1) nor '##' (level 2).
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
            -- A probabilistic recommendation (0–100%) for both “Ballot Appearance” and “Induction,” with a brief rationale. 
            -- A qualitative recommendation as well ranging from very unlikely to very likely.
            

            <Batter Probabilities from Different Expert Machine Learning Models>
            3 Hall of Fame Ballot Probabilities: {string.Join(", ", hallOfFameBallotProbabilities)}
            3 Hall of Fame Ballot Induction Probabilities: {string.Join(", ", hallOfFameInductionProbabilities)}
            </Batter Probabilities from Different Expert Machine Learning Models>

            Important return Markdown ONLY, no other HTML or tic marks etc.
            Formatting rule: only use '###' (level 3) or smaller headings. Do not use '#' (level 1) nor '##' (level 2).
            """;

            return decisionPrompt;
        }

        public static string GetQuantitativeAnalysisPrompt()
        {
            var decisionPrompt =
                """
                Produce two unified confidence intervals:
                1) Hall‑of‑Fame Ballot probability estimate with a confidence interval.
                2) Hall‑of‑Fame Induction probability estimate with a confidence interval.
                
                Use the following approach:
                
                1. **Extract** from the chat history each agent’s key numeric outputs:
                   - **Ballot Appearance Probability** \(p_{i,B}\)
                   - **Induction Probability** \(p_{i,I}\)
                   for \(i=1,2,3\).
                
                2. **Combine** each set of three probabilities using a Luce’s‑Choice–inspired soft‑max fusion:
                   \[
                     P_{\rm fused}(k) 
                     \;=\;
                     \frac{\sum_{i=1}^3 \bigl(p_{i}\bigr)^{\,k}}
                          {\sum_{i=1}^3 \bigl(p_{i}\bigr)^{\,k}
                           \;+\;\sum_{i=1}^3 \bigl(1 - p_{i}\bigr)^{\,k}
                          }.
                   \]
                   Do this separately for the ballot set \(\{p_{1,B},p_{2,B},p_{3,B}\}\) and the induction set \(\{p_{1,I},p_{2,I},p_{3,I}\}\).
                
                3. **Sweep** \(k\) over a range of values (for example, \(k \in [0.5,1.0,2.0]\)) to generate three fused probabilities for each outcome. Record the minimum and maximum \(P_{\rm fused}\) across that \(k\) grid to form a **sensitivity band**.
                
                4. **Report**:
                   - **Point estimate** at \(k=1\) (“standard” fusion).
                   - **Lower/upper bounds** from the sensitivity sweep as your confidence interval.
                   - A brief interpretation of how varying \(k\) dampens or amplifies agent consensus.
                
                5. **Output format**:
                   ```Markdown format ONLY no other HTML or tic marks etc.```
                   ```Formatting rule: only use '###' (level 3) or smaller headings. Do not use '#' (level 1) nor '##' (level 2).```
                   ```Do not include the math from above (Steps 1 - 4)```
                   ### Unified Agentic Hall‑of‑Fame Probabilities
                
                   - **Hall of Fame Ballot Appearance**  
                     • Point estimate (k=1): XX.X%  
                     • Sensitivity band (k=0.5–2.0): [YY.Y%, ZZ.Z%]
                
                   - **Hall of Fame Induction**  
                     • Point estimate (k=1): AA.A%  
                     • Sensitivity band (k=0.5–2.0): [BB.B%, CC.C%]
                
                   **Interpretation:**  
                   A short paragraph explaining the meaning of the point estimates 
                   and the size of the confidence intervals.
                """;

            return decisionPrompt;
        }
    }
}
