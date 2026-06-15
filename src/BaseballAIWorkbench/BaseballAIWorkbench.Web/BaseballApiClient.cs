
using BaseballAIWorkbench.Common.Agents;
using Markdig;
using Newtonsoft.Json;
using System.Net.Http.Json;
using System.Text.RegularExpressions;

namespace BaseballAIWorkbench.Web
{
    public class BaseballApiClient(HttpClient httpClient)
    {
        private static readonly MarkdownPipeline MarkdownPipeline = new MarkdownPipelineBuilder()
            .UseAdvancedExtensions()
            .UsePipeTables()
            .UseGridTables()
            .Build();

        public async Task<int> GetBaseballPlayerCountAsync(CancellationToken cancellationToken = default)
        {
            var playerCount = await httpClient.GetFromJsonAsync<int>("/Players", cancellationToken);
            return playerCount;
        }

        public async Task<string> GetBaseballPlayerAnalysis(AgenticAnalysisConfig agenticAnalysisConfig, CancellationToken cancellationToken = default)
        {
            var playerAnalysis = await httpClient.PostAsJsonAsync<AgenticAnalysisConfig>("/BaseballPlayerAnalysisML", agenticAnalysisConfig, cancellationToken);

            playerAnalysis.EnsureSuccessStatusCode();

            var playerAnalysisString = await playerAnalysis.Content.ReadAsStringAsync(cancellationToken);

            string bingSourcePattern = @"【[^】]*】";        // match an opening 【, then any chars except 】, then a closing 】
            string cleanedAnalysis = Regex.Replace(playerAnalysisString, bingSourcePattern, "");

            return ConvertAgentMarkdownToHtml(cleanedAnalysis);
        }

        public async Task<string> GetBaseballPlayerAnalysisMultipleModels(AgenticAnalysisConfig agenticAnalysisConfig, CancellationToken cancellationToken = default)
        {
            var playerAnalysis = await httpClient.PostAsJsonAsync<AgenticAnalysisConfig>("/BaseballPlayerAnalysisMultipleAgents", agenticAnalysisConfig, cancellationToken);

            playerAnalysis.EnsureSuccessStatusCode();

            var playerAnalysisString = await playerAnalysis.Content.ReadAsStringAsync(cancellationToken);

            string bingSourcePattern = @"【[^】]*】";        // match an opening 【, then any chars except 】, then a closing 】
            string cleanedAnalysis = Regex.Replace(playerAnalysisString, bingSourcePattern, "");

            return ConvertAgentMarkdownToHtml(cleanedAnalysis);
        }

        private static string ConvertAgentMarkdownToHtml(string jsonString)
        {
            var markdown = NormalizeAgentMarkdown(JsonConvert.DeserializeObject<string>(jsonString) ?? string.Empty);
            return Markdown.ToHtml(markdown, MarkdownPipeline);
        }

        private static string NormalizeAgentMarkdown(string markdown)
        {
            markdown = markdown.Replace("\r\n", "\n").Replace('\r', '\n').Trim();

            markdown = Regex.Replace(
                markdown,
                @"\A\s*```(?:markdown|md)?\s*\n(?<content>[\s\S]*?)\n```\s*\z",
                "${content}",
                RegexOptions.IgnoreCase);

            markdown = Regex.Replace(markdown, @"(?m)^[ \t]{1,3}(\|)", "$1");
            markdown = Regex.Replace(markdown, @"(?m)([^\n])\n(\|.+\|\s*$)", "$1\n\n$2");
            markdown = Regex.Replace(markdown, @"(?m)^(\|[ \t:\-|\u2013\u2014]+\|\s*)\n([^\n|])", "$1\n\n$2");

            return markdown.Trim();
        }
    }
}
