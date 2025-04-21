
using Azure;
using BaseballAIWorkbench.Web.MachineLearning;

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

            return await playerAnalysis.Content.ReadAsStringAsync();
        }
    }
}
