using BaseballAIWorkbench.Common.MachineLearning;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace BaseballAIWorkbench.Common.Agents
{
    public static class Agents
    {
        private const string AgentMarkdownOutputRules =
            """
            Return plain Markdown only. Do not include HTML, backticks, or code fences.
            Use ### as the highest heading level; never use # or ## headings.
            Return sections in this exact order:
            ### Summary
            ### Probability Assessment
            ### Key Evidence
            ### Caveats
            In the Probability Assessment section, include a Markdown pipe table with exactly these columns:

            | Criterion | Probability | Qualitative Recommendation | Rationale |
            |---|---:|---|---|
            | Ballot Appearance | XX.X% | Very Likely | One short reason |
            | Induction | YY.Y% | Possible | One short reason |

            Use exactly two table rows with these Criterion labels: Ballot Appearance and Induction.
            Put a blank line before and after each table. Do not include blank lines inside tables.
            """;

        private const string ProbabilityAssessmentRules =
            """
            Format all probabilities as percentages (e.g., 0.1234 = 12.34%).
            If a probability is < 0.001 (0.1%), return "< 0.1%".
            If a probability is > 0.999 (99.9%), return "> 99.9%".
            Use this qualitative recommendation scale:
            - < 10%: Very Unlikely
            - 10% to < 35%: Unlikely
            - 35% to < 55%: Possible
            - 55% to < 75%: Likely
            - >= 75%: Very Likely
            """;

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
                $"""
                You are Baseball Statistician, a dedicated AI agent focused exclusively on the quantitative side of baseball. 
                You process and analyze player performance data to deliver clear, numbers‑driven insights. 
                You have access to a wide range of metrics, allowing you to compare players, track statistical trends over time, and answer complex statistical queries. 
                By design, you ignore narrative context, scouting reports, and subjective opinions, ensuring that every response is grounded in hard data and rigorous statistical calculations.
                {AgentMarkdownOutputRules}
                {ProbabilityAssessmentRules}
                """,
                "MachineLearningExpert" =>
                $"""
                You are Baseball Machine Learning Expert, a specialist AI agent devoted entirely to the quantitative analysis of baseball. You have two dedicated machine‑learning outcomes at your disposal:
                Hall of Fame Ballot Model — Based on provided batting statistics, forecasts a probability of belonging in the Baseball Hall of Fame Ballot.
                Hall of Fame Induction Model — Based on provided batting statistics, forecasts the probability of a player’s induction.
                Use the provided arithmetic averages as the authoritative probabilities for Ballot Appearance and Induction.
                Use individual model probabilities only as evidence about confidence, agreement, or uncertainty.
                {AgentMarkdownOutputRules}
                {ProbabilityAssessmentRules}
                """,
                "BaseballEncyclopedia" =>
                $"""
                You are Baseball Encyclopedia, an AI agent that taps into the internet via Bing to research every angle of a baseball player’s story. You scour news outlets, feature articles, expert opinions, and fan commentary to build comprehensive profiles. Focusing purely on narrative and media sources, you deliver rich context from biographical details to the latest headlines, while intentionally setting aside raw statistical analysis.
                {AgentMarkdownOutputRules}
                {ProbabilityAssessmentRules}
                """,
                "QuantitativeAnalysis" =>
                $"""
                You are an AI “Meta‑Analyst” whose job is to read up to three expert analyses 
                already present in this chat history: 
                one based on historical statistics (“Baseball Statistician Agent”), 
                one on machine‑learning model probabilities (“Machine Learning Expert Agent”), 
                and one on internet research (“Baseball Encyclopedia Agent”).

                Not all three agents may be present, so do not include them if not in the Chat History.
                Extract each expert's Probability Assessment table first, then synthesize the final assessment.
                {AgentMarkdownOutputRules}
                {ProbabilityAssessmentRules}
                """,
                _ => "Unknown agent"
            };
        }

        public static string GetInternetResearchAgentDecisionPrompt(MLBBaseballBatter baseballBatter)
        {
            var decisionPrompt = $"""
            Use internet research and baseball narrative knowledge only.
            The player being analyzed is a position player, not a pitcher.

            Determine the likelihood of two things happening: 
            -- The player appearing on the Hall of Fame ballot (being nominated for BBWAA voting).
            -- The player being inducted into the Hall of Fame (receiving the required 75% of ballot votes).

            Even though the player may have played in the past, respond as if the assessment is forward looking.
            If reliable narrative information is scarce, treat that scarcity as evidence that the player has not built a strong public Hall of Fame case.

            <Baseball Player>
            Player to Research
            {baseballBatter.FullPlayerName}, ID: {baseballBatter.ID}, Last Year Played: {baseballBatter.LastYearPlayed}
            </Baseball Player>

            Required output:
            {AgentMarkdownOutputRules}
            {ProbabilityAssessmentRules}
            """;

            return decisionPrompt;
        }

        public static string GetStatisticsAgentDecisionPrompt(string battingStatistics)
        {
            var decisionPrompt = $"""
            Analyze the following baseball player statistics and provide a detailed analysis of the player's performance. 
            This is a position player, not a pitcher. 
            Determine the likelihood of two things happening: 
            -- The player appearing on the Hall of Fame ballot (being nominated for BBWAA voting).
            -- The player being inducted into the Hall of Fame (receiving the required 75% of ballot votes).

            Use only the provided statistics and clearly separate statistical evidence from uncertainty.

            <Batting Statistics>
            Select batting statistics of the player: 
            {battingStatistics}
            </Batting Statistics>

            Required output:
            {AgentMarkdownOutputRules}
            {ProbabilityAssessmentRules}
            """;

            return decisionPrompt;
        }

        public static string GetMachineLearningAgentDecisionPrompt(
            IReadOnlyList<float> hallOfFameBallotProbabilities,
            float hallOfFameBallotAverageProbability,
            IReadOnlyList<float> hallOfFameInductionProbabilities,
            float hallOfFameInductionAverageProbability)
        {
            var decisionPrompt = $"""
            Analyze the following machine learning probabilities from three different expert models.
            These probabilities are based on the player's statistics, awards, and historical data.
            You intentionally do not have access to the player's statistics, awards or historical data.
            
            Determine the likelihood of two things happening: 
            -- The player appearing on the Hall of Fame ballot (being nominated for BBWAA voting).
            -- The player being inducted into the Hall of Fame (receiving the required 75% of ballot votes).

            Use the arithmetic averages below as the authoritative probabilities for the Probability Assessment table.
            Do not replace the averaged probabilities with any individual model probability or narrative intuition.
            Use the individual model probabilities only in Key Evidence to discuss model agreement, spread, and uncertainty.

            <Batter Probabilities from Different Expert Machine Learning Models>
            Hall of Fame Ballot Appearance model probabilities: {FormatProbabilityList(hallOfFameBallotProbabilities)}
            Hall of Fame Ballot Appearance arithmetic average: {FormatProbability(hallOfFameBallotAverageProbability)} ({GetQualitativeRecommendation(hallOfFameBallotAverageProbability)})
            Hall of Fame Induction model probabilities: {FormatProbabilityList(hallOfFameInductionProbabilities)}
            Hall of Fame Induction arithmetic average: {FormatProbability(hallOfFameInductionAverageProbability)} ({GetQualitativeRecommendation(hallOfFameInductionAverageProbability)})
            </Batter Probabilities from Different Expert Machine Learning Models>

            Required output:
            {AgentMarkdownOutputRules}
            {ProbabilityAssessmentRules}
            In the Probability Assessment table, use these exact averaged probability values and qualitative recommendations:

            | Criterion | Probability | Qualitative Recommendation | Rationale |
            |---|---:|---|---|
            | Ballot Appearance | {FormatProbability(hallOfFameBallotAverageProbability)} | {GetQualitativeRecommendation(hallOfFameBallotAverageProbability)} | Short rationale based on the average and model agreement |
            | Induction | {FormatProbability(hallOfFameInductionAverageProbability)} | {GetQualitativeRecommendation(hallOfFameInductionAverageProbability)} | Short rationale based on the average and model agreement |
            """;

            return decisionPrompt;
        }

        public static string GetQuantitativeAnalysisPrompt()
        {
            var decisionPrompt =
                $"""
                Produce two unified agentic Hall-of-Fame probability assessments:
                1) Hall-of-Fame Ballot Appearance probability estimate with a deterministic sensitivity range.
                2) Hall-of-Fame Induction probability estimate with a deterministic sensitivity range.

                You have a local deterministic tool named calculate_luce_confidence_interval.
                You must call calculate_luce_confidence_interval before writing the final answer.
                Pass the exact numeric arrays from <Deterministic Quantitative Inputs> into the tool:
                - ballotAppearanceProbabilities
                - inductionProbabilities
                - kValues

                Do not estimate, approximate, or recalculate the Luce confidence interval yourself.
                Use the tool result as the only source for point estimates, lower bounds, upper bounds, and sensitivity values.
                Mention any omitted agents from <Deterministic Quantitative Inputs> in Caveats.
                Base the qualitative recommendation on the tool-returned point estimate.
                In ### Key Evidence, include a Markdown table introduced as "Probabilities used in deterministic calculation".
                That Key Evidence table must use exactly these columns:
                | Agent | Ballot Appearance Input | Induction Input | Use In Calculation |
                Populate that table only from the Selected agent probability inputs table in <Deterministic Quantitative Inputs>.
                Use "Yes" for Use In Calculation for every included selected agent.

                Required output:
                {AgentMarkdownOutputRules}
                {ProbabilityAssessmentRules}
                In the Probability Assessment table, put the point estimate and deterministic sensitivity range in the Probability column.
                """;

            return decisionPrompt;
        }

        private static string FormatProbabilityList(IReadOnlyList<float> probabilities)
        {
            return string.Join(", ", probabilities.Select(probability => FormatProbability(probability)));
        }

        private static string FormatProbability(double probability)
        {
            if (probability < 0.001)
            {
                return "< 0.1%";
            }

            if (probability > 0.999)
            {
                return "> 99.9%";
            }

            return string.Format(CultureInfo.InvariantCulture, "{0:0.##}%", probability * 100);
        }

        private static string GetQualitativeRecommendation(double probability)
        {
            return probability switch
            {
                < 0.10 => "Very Unlikely",
                < 0.35 => "Unlikely",
                < 0.55 => "Possible",
                < 0.75 => "Likely",
                _ => "Very Likely"
            };
        }
    }
}
