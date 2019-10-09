using System;
using System.Collections.Generic;
using System.Text;

namespace BaseballPredictionBlazor.MachineLearning
{
    public enum MLModelPredictionType
    {
        /// <summary>
        /// Predict the probability of the baseball player being on the BALLOT of the Hall of Fame.
        /// </summary>
        OnHallOfFameBallot = 0,

        /// <summary>
        /// Predict the probability of the baseball player being INDUCTED to the Hall of Fame.
        /// </summary>
        InductedToHallOfFame = 1
    };
}
