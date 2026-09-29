using BaseballAIWorkbench.Common.MachineLearning;
using Microsoft.Extensions.ML;

namespace BaseballAIWorkbench.ResearchChecks;

internal static class ModelPackagingChecks
{
    public static void Run()
    {
        var modelTypes = Enum.GetValues<MLModelPredictionType>();
        var packagedFiles = Directory.EnumerateFiles(Path.Combine(AppContext.BaseDirectory, "Models"))
            .Select(Path.GetFileName).ToHashSet(StringComparer.Ordinal);
        Check.That(BaseballModelRegistration.ModelFiles.Count == modelTypes.Length,
            "Every prediction type has a model registration");
        foreach (var modelType in modelTypes)
        {
            Check.That(BaseballModelRegistration.ModelFiles.TryGetValue(modelType, out var fileName)
                && packagedFiles.Contains(fileName), $"{modelType} is packaged with the exact filename casing");
        }

        var originalDirectory = Directory.GetCurrentDirectory();
        var emptyDirectory = Directory.CreateTempSubdirectory("baseball-model-checks-");
        try
        {
            Directory.SetCurrentDirectory(emptyDirectory.FullName);
            using (var provider = new ServiceCollection().AddLogging()
                .AddBaseballPredictionModels().BuildServiceProvider())
            {
                Predict(provider, modelTypes);
            }

            MLModelPredictionType[] webModels =
            [
                MLModelPredictionType.InductedToHallOfFameGeneralizedAdditiveModel,
                MLModelPredictionType.OnHallOfFameBallotGeneralizedAdditiveModel
            ];
            using var webProvider = new ServiceCollection().AddLogging()
                .AddBaseballPredictionModels(webModels).BuildServiceProvider();
            Predict(webProvider, webModels);
        }
        finally
        {
            Directory.SetCurrentDirectory(originalDirectory);
            emptyDirectory.Delete();
        }

        Console.WriteLine("PASS: packaged API and Web models load and predict from an unrelated working directory.");
    }

    private static void Predict(IServiceProvider provider, IEnumerable<MLModelPredictionType> modelTypes)
    {
        var pool = provider.GetRequiredService<PredictionEnginePool<MLBBaseballBatter, MLBHOFPrediction>>();
        var batter = new MLBBaseballBatter
        {
            FullPlayerName = "Packaging fixture", ID = "fixture", YearsPlayed = 20,
            AB = 10000, H = 3000, R = 1500, Doubles = 500, Triples = 50, HR = 450,
            RBI = 1500, SB = 200, BattingAverage = 0.3f, SluggingPct = 0.495f,
            TB = 4950, AllStarAppearances = 10, TotalPlayerAwards = 20, LastYearPlayed = 2020
        };
        foreach (var modelType in modelTypes)
        {
            var prediction = pool.Predict(modelType.ToString(), batter);
            Check.That(float.IsFinite(prediction.Probability) && prediction.Probability is >= 0 and <= 1,
                $"{modelType} returns a valid probability using the production registration");
        }
    }
}
