using System;
using System.Collections.Generic;
using System.Text;

namespace BaseballPredictionBlazor.Shared
{
    public static class MLModelPredictionType
    {
        /// <summary>
        /// Predict the probability of the baseball player being on the BALLOT of the Hall of Fame.
        /// </summary>
        public const string OnHallOfFameBallot = "OnHallOfFameBallot";

        /// <summary>
        /// Predict the probability of the baseball player being INDUCTED to the Hall of Fame.
        /// </summary>
        public const string InductedToHallOfFame = "InductedToHallOfFame";

        public static string GetPredictionLabel(string mlModelPredictionType)
        {
            var predictionLabel = string.Empty;

            if (mlModelPredictionType == OnHallOfFameBallot)
            {
                predictionLabel = "On Hall of Fame Ballot";
            }
            else
            {
                predictionLabel = "Hall Of Fame Induction";
            }

            return predictionLabel;
        }
    }
}
