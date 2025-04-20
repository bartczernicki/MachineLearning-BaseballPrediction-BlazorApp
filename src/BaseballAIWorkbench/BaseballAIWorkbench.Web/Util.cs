using BaseballAIWorkbench.Web.MachineLearning;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace BaseballAIWorkbench
{
    public static class Util
    {
        public static string GetWhatIfUrl(bool isAgenticAnalysis, bool useMachineLearningModel, bool MultipleModels, MLBBaseballBatter baseballBatter, int numberOfSeasonsPlayed)
        {
            var basePage = string.Empty;

            if (isAgenticAnalysis)
            {
                basePage = "WhatIfAnalysisAgentic";
            }
            else if (!useMachineLearningModel)
            {
                basePage = "WhatIfAnalysisRulesEngine";
            }
            else
            {
                if (MultipleModels)
                {
                    basePage = "WhatIfAnalysisMultipleModels";
                }
                else
                {
                    basePage = "WhatIfAnalysisSingleModel";
                }
            }

            string whatIfUrl = string.Format(
                "/{0}/{1}-{2}/YearsPlayed/{3}", basePage, baseballBatter.ID,
                    Util.RemoveWhiteSpace(baseballBatter.FullPlayerName), numberOfSeasonsPlayed);

            return whatIfUrl;
        }

        public static string RemoveWhiteSpace(string stringWithPotentialWhiteSpace)
        {
            var whiteSpeaceRemoved = String.Concat(stringWithPotentialWhiteSpace.Where(c => !Char.IsWhiteSpace(c)));

            return whiteSpeaceRemoved;
        }
    }
}
