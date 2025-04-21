
using Azure;
using BaseballAIWorkbench.Web.MachineLearning;
using Markdig;

namespace BaseballAIWorkbench.Web
{
    public class BaseballApiClient(HttpClient httpClient)
    {
        public async Task<int> GetBaseballPlayerCountAsync(CancellationToken cancellationToken = default)
        {
            var playerCount = await httpClient.GetFromJsonAsync<int>("/Players", cancellationToken);

            return playerCount;
        }

        public async Task<string> GetBaseballPlayerAnalysis(MLBBaseballBatter baseballPlayer, CancellationToken cancellationToken = default)
        {
            var playerAnalysis = await httpClient.PostAsJsonAsync<MLBBaseballBatter>("/BaseballPlayerAnalysis", baseballPlayer, cancellationToken);

            playerAnalysis.EnsureSuccessStatusCode();

            var playerAnalysisString = await playerAnalysis.Content.ReadAsStringAsync();

            var cleanString = playerAnalysisString
                .Replace("\r\n", " ")
                .Replace("\n\n", " ")
                .Replace("\n", " ")
                .Replace("\"", string.Empty);

            var playerAnalysisHtml = Markdown.ToHtml(cleanString);
            return Markdown.ToHtml(playerAnalysisHtml);
        }
    }
}
