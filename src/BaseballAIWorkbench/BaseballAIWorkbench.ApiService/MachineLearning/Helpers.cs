using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace BaseballAIWorkbench.ApiService.MachineLearning
{
    public static class Helpers
    {
        public static string GetPredictionLabel(MLModelPredictionType mlModelPredictionType, bool isProbability)
        {
            var predictionLabel = string.Empty;
            var predictionLabelSuffix = "Probability";

            if (mlModelPredictionType == MLModelPredictionType.OnHallOfFameBallotGeneralizedAdditiveModel)
            {
                predictionLabel = "Ballot";
            }
            else
            {
                predictionLabel = "Induction";
            }

            if (!isProbability)
            {
                predictionLabelSuffix = "Category";
            }

            return string.Format("{0} {1}", predictionLabel, predictionLabelSuffix);
        }
    }
}
