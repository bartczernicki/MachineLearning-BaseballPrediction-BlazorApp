using BaseballAIWorkbench.ApiService.MachineLearning;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace BaseballAIWorkbench.ApiService.Services
{
    public class BaseballDataService
    {
        private static List<MLBBaseballBatter> _batters;

        public Task<List<MLBBaseballBatter>> GetBaseballData()
        {
            // Returns historical baseball players (batters)

            // Note: In a production system this service would load the list of batters
            // from distributed persisted storage, searched in information retrieval engine (i.e. Azure Search, Lucene),
            // a relational database etc.

            // Load MLB baseball batters from local CSV file
            string filePathMLBBaseballBatters = "Data/MLBBaseballBattersPositionPlayers.csv";

            if (_batters == null)
            {
                _batters = File.ReadAllLines(filePathMLBBaseballBatters)
                            .Skip(1)
                            .Select(v => MLBBaseballBatter.FromCsv(v))
                            .OrderByDescending(a => a.YearsPlayed)
                            .ToList();
            }

            return Task.FromResult(
                _batters.OrderByDescending(a => a.YearsPlayed).ToList()
            ); ;
        }
    }
}
