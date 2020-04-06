using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using BaseballMachineLearningWorkbench.MachineLearning;
using BaseballMachineLearningWorkbench.Shared;

namespace BaseballMachineLearningWorkbench.Services
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
            string filePathMLBBaseballBatters = "Data/MLBBaseballBatters.csv";

            var batters = File.ReadAllLines(filePathMLBBaseballBatters)
                        .Skip(1)
                        .Select(v => MLBBaseballBatter.FromCsv(v))
                        .ToList();

            // Create Fictitious Players
            //MLBBaseballBatter badMLBBatter = new MLBBaseballBatter
            //{
            //    FullPlayerName = "Barry Badd (Fictitious Player)",
            //    ID = 100f,
            //    InductedToHallOfFame = false,
            //    LastYearPlayed = 0f,
            //    OnHallOfFameBallot = false,
            //    YearsPlayed = 2f,
            //    AB = 100f,
            //    R = 10f,
            //    H = 30f,
            //    Doubles = 1f,
            //    Triples = 1f,
            //    HR = 1f,
            //    RBI = 10f,
            //    SB = 10f,
            //    BattingAverage = 0.3f,
            //    SluggingPct = 0.15f,
            //    AllStarAppearances = 0f,
            //    MVPs = 0f,
            //    TripleCrowns = 0f,
            //    GoldGloves = 0f,
            //    MajorLeaguePlayerOfTheYearAwards = 0f,
            //    TB = 200f
            //};
            //MLBBaseballBatter averageMLBBatter = new MLBBaseballBatter
            //{
            //    FullPlayerName = "Andy Average (Fictitious Player)",
            //    ID = 200f,
            //    InductedToHallOfFame = false,
            //    LastYearPlayed = 0f,
            //    OnHallOfFameBallot = false,
            //    YearsPlayed = 17f,
            //    AB = 8393f,
            //    R = 1162f,
            //    H = 2300f,
            //    Doubles = 410f,
            //    Triples = 8f,
            //    HR = 400f,
            //    RBI = 1312f,
            //    SB = 9f,
            //    BattingAverage = 0.278f,
            //    SluggingPct = 0.476f,
            //    AllStarAppearances = 5f,
            //    MVPs = 0f,
            //    TripleCrowns = 0f,
            //    GoldGloves = 0f,
            //    MajorLeaguePlayerOfTheYearAwards = 0f,
            //    TB = 3910f
            //};
            //MLBBaseballBatter greatMLBBatter = new MLBBaseballBatter
            //{
            //    FullPlayerName = "Gary The Great (Fictitious Player)",
            //    ID = 300f,
            //    InductedToHallOfFame = false,
            //    LastYearPlayed = 0f,
            //    OnHallOfFameBallot = false,
            //    YearsPlayed = 20f,
            //    AB = 10000f,
            //    R = 1900f,
            //    H = 3500f,
            //    Doubles = 500f,
            //    Triples = 150f,
            //    HR = 600f,
            //    RBI = 1800f,
            //    SB = 400f,
            //    BattingAverage = 0.350f,
            //    SluggingPct = 0.65f,
            //    AllStarAppearances = 14f,
            //    MVPs = 2f,
            //    TripleCrowns = 1f,
            //    GoldGloves = 4f,
            //    MajorLeaguePlayerOfTheYearAwards = 2f,
            //    TB = 7000f
            //};

            //// Add Fictitious Players
            //batters.Add(badMLBBatter);
            //batters.Add(averageMLBBatter);
            //batters.Add(greatMLBBatter);

            return Task.FromResult(
                batters.OrderByDescending(a => a.YearsPlayed).ToList()
            ); ;
        }
    }
}
