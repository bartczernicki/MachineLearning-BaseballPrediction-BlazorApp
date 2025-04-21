using System;
using BaseballAIWorkbench.Common.MachineLearning;

namespace BaseballAIWorkbench.ApiService.Services
{
    public interface IBaseballHallofFamePrediction
    {
        MLBHOFPrediction PredictInductedToHOF(MLBBaseballBatter mLBBaseballBatter);
        MLBHOFPrediction PredictOnHallOfFameBallot(MLBBaseballBatter mLBBaseballBatter);
    }
}
