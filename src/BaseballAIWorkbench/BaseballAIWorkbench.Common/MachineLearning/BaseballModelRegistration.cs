using System.Collections.ObjectModel;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.ML;

namespace BaseballAIWorkbench.Common.MachineLearning;

public static class BaseballModelRegistration
{
    public static IReadOnlyDictionary<MLModelPredictionType, string> ModelFiles { get; } =
        new ReadOnlyDictionary<MLModelPredictionType, string>(new Dictionary<MLModelPredictionType, string>
        {
            [MLModelPredictionType.InductedToHallOfFameGeneralizedAdditiveModel] = "InductedToHoF-GeneralizedAdditiveModels.mlnet",
            [MLModelPredictionType.OnHallOfFameBallotGeneralizedAdditiveModel] = "OnHoFBallot-GeneralizedAdditiveModels.mlnet",
            [MLModelPredictionType.InductedToHallOfFameFastTreeModel] = "InductedToHoF-FastTree.mlnet",
            [MLModelPredictionType.OnHallOfFameBallotFastTreeModel] = "OnHoFBallot-FastTree.mlnet",
            [MLModelPredictionType.InductedToHallOfFameLightGbmModel] = "InductedToHoF-LightGbm.mlnet",
            [MLModelPredictionType.OnHallOfFameBallotLightGbmModel] = "OnHoFBallot-LightGbm.mlnet"
        });

    // With no selection, register all models. The web app needs only the two GAM models.
    public static IServiceCollection AddBaseballPredictionModels(
        this IServiceCollection services, params MLModelPredictionType[] modelTypes)
    {
        var pools = services.AddPredictionEnginePool<MLBBaseballBatter, MLBHOFPrediction>();
        var selectedTypes = modelTypes.Length == 0 ? ModelFiles.Keys : modelTypes;

        foreach (var modelType in selectedTypes)
        {
            // Model files are packaged beside the application, regardless of its working directory.
            var path = Path.Combine(AppContext.BaseDirectory, "Models", ModelFiles[modelType]);
            pools.FromFile(modelType.ToString(), path);
        }

        return services;
    }
}
