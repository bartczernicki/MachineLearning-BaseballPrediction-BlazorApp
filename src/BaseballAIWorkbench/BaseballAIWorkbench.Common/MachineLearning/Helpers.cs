using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Reflection;

namespace BaseballAIWorkbench.Common.MachineLearning
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

        public static string GetCurrentAssemblyPath()
        {
            var currentAssembly = Assembly.GetExecutingAssembly();
            string assemblyPath = Path.GetDirectoryName(currentAssembly.Location);

            return assemblyPath;
        }
    }
}
