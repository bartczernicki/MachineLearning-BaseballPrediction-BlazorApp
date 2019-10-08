using System;
using BaseballPredictionBlazor.MachineLearning;

namespace BaseballPredictionBlazor.Services
{
    public interface IBaseballHallofFamePrediction
    {
        MLBHOFPrediction PredictInductedToHOF(MLBBaseballBatter mLBBaseballBatter);
        MLBHOFPrediction PredictOnHallOfFameBallot(MLBBaseballBatter mLBBaseballBatter);
    }
}
