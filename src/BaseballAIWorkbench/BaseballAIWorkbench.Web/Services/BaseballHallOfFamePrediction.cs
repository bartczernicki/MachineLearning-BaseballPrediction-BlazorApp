﻿using BaseballAIWorkbench.Common.MachineLearning;
using Microsoft.Extensions.ML;
using Microsoft.ML;
using System.IO;

namespace BaseballAIWorkbench.Web.Services
{
    public class BaseballHallofFamePrediction : IBaseballHallofFamePrediction
    {
        protected readonly ITransformer _loadedModelInductedToHallOfFame;
        protected readonly ITransformer _loadedModelOnHallOfFameBallot;

        protected readonly MLContext _mlContext;
        protected readonly DataViewSchema _schema;

        public BaseballHallofFamePrediction(string modelPathInductedToHallOfFame, string modelPathOnHallOfFameBallot)
        {
            _mlContext = new MLContext(seed: 0);

            using (var stream = new FileStream(modelPathInductedToHallOfFame, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                _loadedModelInductedToHallOfFame = _mlContext.Model.Load(stream, out _schema);
            }

            using (var stream = new FileStream(modelPathOnHallOfFameBallot, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                _loadedModelOnHallOfFameBallot = _mlContext.Model.Load(stream, out _schema);
            }
        }

        public MLBHOFPrediction PredictInductedToHOF(MLBBaseballBatter mLBBaseballBatter)
        {
            var predEngineOnHallOfFameBallot = 
                _mlContext.Model.CreatePredictionEngine<MLBBaseballBatter, MLBHOFPrediction>(_loadedModelInductedToHallOfFame);

            return predEngineOnHallOfFameBallot.Predict(mLBBaseballBatter);
        }

        public MLBHOFPrediction PredictOnHallOfFameBallot(MLBBaseballBatter mLBBaseballBatter)
        {
            var predEngineOnHallOfFameBallot =
                _mlContext.Model.CreatePredictionEngine<MLBBaseballBatter, MLBHOFPrediction>(_loadedModelOnHallOfFameBallot);

            return predEngineOnHallOfFameBallot.Predict(mLBBaseballBatter);
        }
    }

}