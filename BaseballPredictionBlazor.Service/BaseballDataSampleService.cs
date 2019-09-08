using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using BaseballPredictionBlazor.Shared;

namespace BaseballPredictionBlazor.Service
{
    public class BaseballDataSampleService
    {
        public Task<List<MLBBaseballBatter>> GetSampleBaseballData()
        {
            MLBBaseballBatter badMLBBatter = new MLBBaseballBatter
            {
                FullPlayerName = "Barry Badd",
                ID = 100f,
                InductedToHallOfFame = false,
                LastYearPlayed = 0f,
                OnHallOfFameBallot = false,
                YearsPlayed = 2f,
                AB = 100f,
                R = 10f,
                H = 30f,
                Doubles = 1f,
                Triples = 1f,
                HR = 1f,
                RBI = 10f,
                SB = 10f,
                BattingAverage = 0.3f,
                SluggingPct = 0.15f,
                AllStarAppearances = 0f,
                MVPs = 0f,
                TripleCrowns = 0f,
                GoldGloves = 0f,
                MajorLeaguePlayerOfTheYearAwards = 0f,
                TB = 200f
            };
            MLBBaseballBatter averageMLBBatter = new MLBBaseballBatter
            {
                FullPlayerName = "Andy Average",
                ID = 200f,
                InductedToHallOfFame = false,
                LastYearPlayed = 0f,
                OnHallOfFameBallot = false,
                YearsPlayed = 17f,
                AB = 8393f,
                R = 1162f,
                H = 2300f,
                Doubles = 410f,
                Triples = 8f,
                HR = 400f,
                RBI = 1312f,
                SB = 9f,
                BattingAverage = 0.278f,
                SluggingPct = 0.476f,
                AllStarAppearances = 5f,
                MVPs = 0f,
                TripleCrowns = 0f,
                GoldGloves = 0f,
                MajorLeaguePlayerOfTheYearAwards = 0f,
                TB = 3910f
            };
            MLBBaseballBatter greatMLBBatter = new MLBBaseballBatter
            {
                FullPlayerName = "Gary The Great",
                ID = 300f,
                InductedToHallOfFame = false,
                LastYearPlayed = 0f,
                OnHallOfFameBallot = false,
                YearsPlayed = 20f,
                AB = 10000f,
                R = 1900f,
                H = 3500f,
                Doubles = 500f,
                Triples = 150f,
                HR = 600f,
                RBI = 1800f,
                SB = 400f,
                BattingAverage = 0.350f,
                SluggingPct = 0.65f,
                AllStarAppearances = 14f,
                MVPs = 2f,
                TripleCrowns = 1f,
                GoldGloves = 4f,
                MajorLeaguePlayerOfTheYearAwards = 2f,
                TB = 7000f
            };
            MLBBaseballBatter mikeTrout = new MLBBaseballBatter
            {
                FullPlayerName = "Mike Trout",
                ID = 400f,
                InductedToHallOfFame = false,
                LastYearPlayed = 2019f,
                OnHallOfFameBallot = false,
                YearsPlayed = 8f,
                AB = 3870f,
                R = 793f,
                H = 1187f,
                Doubles = 224f,
                Triples = 44f,
                HR = 240f,
                RBI = 648f,
                SB = 189f,
                BattingAverage = 0.306f,
                SluggingPct = 0.573f,
                AllStarAppearances = 7f,
                MVPs = 2f,
                TripleCrowns = 0f,
                GoldGloves = 0f,
                MajorLeaguePlayerOfTheYearAwards = 0f,
                TB = 2727f
            };

            List<MLBBaseballBatter> batters = new List<MLBBaseballBatter>() { badMLBBatter, averageMLBBatter, greatMLBBatter, mikeTrout };
            return Task.FromResult(
                batters
            );
        }
    }
}
