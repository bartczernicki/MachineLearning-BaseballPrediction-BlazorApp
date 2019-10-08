using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using BaseballPredictionBlazor.MachineLearning;
using BaseballPredictionBlazor.Shared;

namespace BaseballPredictionBlazor.Services
{
    public class BaseballDataSampleService
    {
        public Task<List<MLBBaseballBatter>> GetSampleBaseballData()
        {
            MLBBaseballBatter badMLBBatter = new MLBBaseballBatter
            {
                FullPlayerName = "Barry Badd (fictitious)",
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
                FullPlayerName = "Andy Average (fictitious)",
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
                FullPlayerName = "Gary The Great (fictitious)",
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
            MLBBaseballBatter babeRuth = new MLBBaseballBatter
            {
                FullPlayerName = "Babe Ruth",
                ID = 400f,
                InductedToHallOfFame = true,
                LastYearPlayed = 1935f,
                OnHallOfFameBallot = true,
                YearsPlayed = 22f,
                AB = 8399f,
                R = 2174f,
                H = 2873f,
                Doubles = 506f,
                Triples = 136f,
                HR = 714f,
                RBI = 2214f,
                SB = 123f,
                BattingAverage = 0.342f,
                SluggingPct = 0.690f,
                AllStarAppearances = 2f,
                MVPs = 1f,
                TripleCrowns = 0f,
                GoldGloves = 0f,
                MajorLeaguePlayerOfTheYearAwards = 0f,
                TB = 5793f
            };
            MLBBaseballBatter mikeTrout = new MLBBaseballBatter
            {
                FullPlayerName = "Mike Trout",
                ID = 500f,
                InductedToHallOfFame = false,
                LastYearPlayed = 2019f,
                OnHallOfFameBallot = false,
                YearsPlayed = 9f,
                AB = 4340f,
                R = 903f,
                H = 1324f,
                Doubles = 251f,
                Triples = 46f,
                HR = 285f,
                RBI = 752f,
                SB = 200f,
                BattingAverage = 0.305f,
                SluggingPct = 0.581f,
                AllStarAppearances = 8f,
                MVPs = 2f,
                TripleCrowns = 0f,
                GoldGloves = 0f,
                MajorLeaguePlayerOfTheYearAwards = 0f,
                TB = 2522f
            };
            MLBBaseballBatter haroldBaines = new MLBBaseballBatter
            {
                FullPlayerName = "Harold Baines",
                ID = 600f,
                InductedToHallOfFame = false,
                LastYearPlayed = 2001f,
                OnHallOfFameBallot = false,
                YearsPlayed = 22f,
                AB = 9908f,
                R = 1299f,
                H = 2866f,
                Doubles = 488f,
                Triples = 49f,
                HR = 384f,
                RBI = 1628f,
                SB = 34f,
                BattingAverage = 0.289f,
                SluggingPct = 0.289f,
                AllStarAppearances = 6f,
                MVPs = 0f,
                TripleCrowns = 0f,
                GoldGloves = 0f,
                MajorLeaguePlayerOfTheYearAwards = 0f,
                TB = 5525f
            };
            MLBBaseballBatter bryceHarper = new MLBBaseballBatter
            {
                FullPlayerName = "Bryce Harper",
                ID = 700f,
                InductedToHallOfFame = false,
                LastYearPlayed = 2019f,
                OnHallOfFameBallot = false,
                YearsPlayed = 8f,
                AB = 3879f,
                R = 708f,
                H = 1071f,
                Doubles = 219f,
                Triples = 19f,
                HR = 219f,
                RBI = 635f,
                SB = 90f,
                BattingAverage = 0.276f,
                SluggingPct = 0.512f,
                AllStarAppearances = 6f,
                MVPs = 1f,
                TripleCrowns = 0f,
                GoldGloves = 0f,
                MajorLeaguePlayerOfTheYearAwards = 0f,
                TB = 1985f
            };

            List<MLBBaseballBatter> batters = new List<MLBBaseballBatter>() {
                badMLBBatter, averageMLBBatter, greatMLBBatter,
                haroldBaines, bryceHarper, mikeTrout, babeRuth};

            return Task.FromResult(
                batters
            );
        }
    }
}
