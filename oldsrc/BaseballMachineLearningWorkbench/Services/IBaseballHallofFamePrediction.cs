using System;
using BaseballMachineLearningWorkbench.MachineLearning;

namespace BaseballMachineLearningWorkbench.Services
{
    public interface IBaseballHallofFamePrediction
    {
        MLBHOFPrediction PredictInductedToHOF(MLBBaseballBatter mLBBaseballBatter);
        MLBHOFPrediction PredictOnHallOfFameBallot(MLBBaseballBatter mLBBaseballBatter);
    }
}
