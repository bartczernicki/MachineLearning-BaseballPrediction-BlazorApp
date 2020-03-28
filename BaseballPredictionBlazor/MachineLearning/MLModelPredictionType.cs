using System;
using System.Collections.Generic;
using System.Text;

namespace BaseballMachineLearningWorkbench.MachineLearning
{
    public enum MLModelPredictionType
    {
        /// <summary>
        /// Predict the probability of the baseball player being on the BALLOT of the Hall of Fame.
        /// </summary>
        OnHallOfFameBallotGeneralizedAdditiveModel = 0,

        /// <summary>
        /// Predict the probability of the baseball player being INDUCTED to the Hall of Fame.
        /// </summary>
        InductedToHallOfFameGeneralizedAdditiveModel = 1
    };
}
