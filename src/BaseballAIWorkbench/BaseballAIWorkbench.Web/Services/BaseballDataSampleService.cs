using BaseballAIWorkbench.Web.MachineLearning;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace BaseballAIWorkbench.Web.Services
{
    public class BaseballDataSampleService
    {
        public Task<List<MLBBaseballBatter>> GetSampleBaseballData()
        {
            // Return sample baseball players (batters)
            // Mix of fictitious, active & retired players of all skills

            // Note: In a production system this service would load the list of batters
            // from distributed persisted storage, searched in information retrieval engine (i.e. Azure Search, Lucene),
            // a relational database etc.

            // Load MLB baseball batters from local CSV file
            string filePathMLBBaseballBatters = "Data/MLBBaseballBattersPositionPlayers.csv";

            var batters = File.ReadAllLines(filePathMLBBaseballBatters)
                        .Skip(1)
                        .Select(v => MLBBaseballBatter.FromCsv(v))
                        .ToList();

            return Task.FromResult(
                batters.OrderByDescending(a => a.YearsPlayed).ToList()
            ); ;
        }
    }
}
