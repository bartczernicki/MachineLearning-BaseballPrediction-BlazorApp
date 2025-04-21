using System;
using BaseballAIWorkbench.Common.MachineLearning;

namespace BaseballAIWorkbench.Web.Services
{
    public interface IBaseballHallofFamePrediction
    {
        MLBHOFPrediction PredictInductedToHOF(MLBBaseballBatter mLBBaseballBatter);
        MLBHOFPrediction PredictOnHallOfFameBallot(MLBBaseballBatter mLBBaseballBatter);
    }
}
