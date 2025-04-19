using System;
using BaseballAIWorkbench.Web.MachineLearning;

namespace BaseballAIWorkbench.Web.Services
{
    public interface IBaseballHallofFamePrediction
    {
        MLBHOFPrediction PredictInductedToHOF(MLBBaseballBatter mLBBaseballBatter);
        MLBHOFPrediction PredictOnHallOfFameBallot(MLBBaseballBatter mLBBaseballBatter);
    }
}
