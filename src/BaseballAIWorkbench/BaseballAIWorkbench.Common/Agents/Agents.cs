namespace BaseballAIWorkbench.Common.Agents
{
    public static class Agents
    {
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
    }
}
