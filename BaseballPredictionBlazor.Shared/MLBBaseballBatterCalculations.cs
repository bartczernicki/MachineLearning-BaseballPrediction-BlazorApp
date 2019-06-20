using System;
using System.Collections.Generic;
using System.Text;


namespace BaseballPredictionBlazor.Shared
{
    public class MLBBaseballBatterCalculations
    {
        MLBBaseballBatter _mlbBaseBallBatter;

        public MLBBaseballBatterCalculations(MLBBaseballBatter mlbBaseballBatter)
        {
            this._mlbBaseBallBatter = mlbBaseballBatter;
        }

        public MLBBaseballBatter CalculateStatisticsProratedBySeason(int numberOfSeasons)
        {
            var baseBallBatterAdjusted = new MLBBaseballBatter
            {
                FullPlayerName = "Great Player",
                ID = 100f,
                InductedToHallOfFame = false,
                LastYearPlayed = 0f,
                OnHallOfFameBallot = false,
                YearsPlayed = numberOfSeasons * 1f,
                AB = (this._mlbBaseBallBatter.AB / this._mlbBaseBallBatter.YearsPlayed) * numberOfSeasons,
                R = (this._mlbBaseBallBatter.R / this._mlbBaseBallBatter.YearsPlayed) * numberOfSeasons,
                H = (this._mlbBaseBallBatter.H / this._mlbBaseBallBatter.YearsPlayed) * numberOfSeasons,
                Doubles = (this._mlbBaseBallBatter.Doubles / this._mlbBaseBallBatter.YearsPlayed) * numberOfSeasons,
                Triples = (this._mlbBaseBallBatter.Triples / this._mlbBaseBallBatter.YearsPlayed) * numberOfSeasons,
                HR = (this._mlbBaseBallBatter.HR / this._mlbBaseBallBatter.YearsPlayed) * numberOfSeasons,
                RBI = (this._mlbBaseBallBatter.RBI / this._mlbBaseBallBatter.YearsPlayed) * numberOfSeasons,
                SB = (this._mlbBaseBallBatter.SB / this._mlbBaseBallBatter.YearsPlayed) * numberOfSeasons,
                BattingAverage = 0.350f,
                SluggingPct = 0.65f,
                AllStarAppearances = (this._mlbBaseBallBatter.AllStarAppearances / this._mlbBaseBallBatter.YearsPlayed) * numberOfSeasons,
                MVPs = (this._mlbBaseBallBatter.MVPs / this._mlbBaseBallBatter.YearsPlayed) * numberOfSeasons,
                TripleCrowns = (this._mlbBaseBallBatter.TripleCrowns / this._mlbBaseBallBatter.YearsPlayed) * numberOfSeasons,
                GoldGloves = (this._mlbBaseBallBatter.GoldGloves / this._mlbBaseBallBatter.YearsPlayed) * numberOfSeasons,
                MajorLeaguePlayerOfTheYearAwards = (this._mlbBaseBallBatter.MajorLeaguePlayerOfTheYearAwards / this._mlbBaseBallBatter.YearsPlayed) * numberOfSeasons,
                TB = (this._mlbBaseBallBatter.TB / this._mlbBaseBallBatter.YearsPlayed) * numberOfSeasons,
            };

            return baseBallBatterAdjusted;
        }
    }
}
