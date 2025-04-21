
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

            var obj = JsonConvert.DeserializeObject<string>(playerAnalysisString);
            var obj2 = Regex.Unescape(playerAnalysisString);

            // now obj.Markdown contains real newline characters

            var playerAnalysisHtml = Markdown.ToHtml(obj);
            return Markdown.ToHtml(playerAnalysisHtml);
        }
    }
}
