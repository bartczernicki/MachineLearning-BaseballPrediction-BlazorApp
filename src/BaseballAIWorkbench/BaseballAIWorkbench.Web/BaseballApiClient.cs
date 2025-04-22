
using Azure;
using BaseballAIWorkbench.Common.Agents;
using BaseballAIWorkbench.Common.MachineLearning;
using Markdig;
using Newtonsoft.Json;
using System.Net.Http.Json;
using System.Text.RegularExpressions;

namespace BaseballAIWorkbench.Web
{
    public class BaseballApiClient(HttpClient httpClient)
    {
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

            var cleanedReadyForHtml = JsonConvert.DeserializeObject<string>(cleanedAnalysis);


            //var cleanedReadyForHtml = Regex.Unescape(playerAnalysisString);

            var playerAnalysisHtml = Markdown.ToHtml(cleanedReadyForHtml);
            return Markdown.ToHtml(playerAnalysisHtml);
        }

        public async Task<string> GetBaseballPlayerAnalysisMultipleModels(AgenticAnalysisConfig agenticAnalysisConfig, CancellationToken cancellationToken = default)
        {
            var playerAnalysis = await httpClient.PostAsJsonAsync<AgenticAnalysisConfig>("/BaseballPlayerAnalysisMultipleAgents", agenticAnalysisConfig, cancellationToken);

            playerAnalysis.EnsureSuccessStatusCode();

            var playerAnalysisString = await playerAnalysis.Content.ReadAsStringAsync(cancellationToken);

            string bingSourcePattern = @"【[^】]*】";        // match an opening 【, then any chars except 】, then a closing 】
            string cleanedAnalysis = Regex.Replace(playerAnalysisString, bingSourcePattern, "");

            var cleanedReadyForHtml = JsonConvert.DeserializeObject<string>(cleanedAnalysis);


            //var cleanedReadyForHtml = Regex.Unescape(playerAnalysisString);

            var playerAnalysisHtml = Markdown.ToHtml(cleanedReadyForHtml);
            return Markdown.ToHtml(playerAnalysisHtml);
        }
    }
}
