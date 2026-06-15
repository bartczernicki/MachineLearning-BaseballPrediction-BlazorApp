using Azure.AI.OpenAI;
using BaseballAIWorkbench.ApiService;
using BaseballAIWorkbench.ApiService.Services;
using BaseballAIWorkbench.Common.MachineLearning;
using Microsoft.Extensions.ML;
using System.ClientModel;

var builder = WebApplication.CreateBuilder(args);

// Add service defaults & Aspire client integrations.
builder.AddServiceDefaults();

// Add services to the container.
builder.Services.AddProblemDetails();

// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

// Add Data service (provides historical Baseball data to the application)
builder.Services.AddSingleton<BaseballDataService>();

// Add the ML.NET models and a prediction object pool to the service
string modelPathInductedToHallOfFameGeneralizedAdditiveModel = Path.Combine(Environment.CurrentDirectory, "Models", "InductedToHoF-GeneralizedAdditiveModels.mlnet");
string modelPathOnHallOfFameBallotGeneralizedAdditiveModel = Path.Combine(Environment.CurrentDirectory, "Models", "OnHoFBallot-GeneralizedAdditiveModels.mlnet");
string modelPathInductedToHallOfFameFastTreeModel = Path.Combine(Environment.CurrentDirectory, "Models", "InductedToHoF-FastTree.mlnet");
string modelPathOnHallOfFameBallotFastTreeModel = Path.Combine(Environment.CurrentDirectory, "Models", "OnHoFBallot-FastTree.mlnet");
string modelPathInductedToHallOfFameLightGBMModel = Path.Combine(Environment.CurrentDirectory, "Models", "InductedToHoF-LightGBM.mlnet");
string modelPathOnHallOfFameBallotLightGBMModel = Path.Combine(Environment.CurrentDirectory, "Models", "OnHoFBallot-LightGBM.mlnet");

builder.Services.AddPredictionEnginePool<MLBBaseballBatter, MLBHOFPrediction>()
    .FromFile("InductedToHallOfFameGeneralizedAdditiveModel", modelPathInductedToHallOfFameGeneralizedAdditiveModel)
    .FromFile("OnHallOfFameBallotGeneralizedAdditiveModel", modelPathOnHallOfFameBallotGeneralizedAdditiveModel)
    .FromFile("InductedToHallOfFameFastTreeModel", modelPathInductedToHallOfFameFastTreeModel)
    .FromFile("OnHallOfFameBallotFastTreeModel", modelPathOnHallOfFameBallotFastTreeModel)
    .FromFile("InductedToHallOfFameLightGbmModel", modelPathInductedToHallOfFameLightGBMModel)
    .FromFile("OnHallOfFameBallotLightGbmModel", modelPathOnHallOfFameBallotLightGBMModel);

var aoaiEndPoint = GetRequiredConnectionString(builder.Configuration, "AOAIEndpoint");
var aoaiApiKey = GetRequiredConnectionString(builder.Configuration, "AOAIApiKey", "AOAIAPIKey");
var aoaiDeploymentName = GetRequiredConnectionString(builder.Configuration, "AOAIModelDeploymentName");

builder.Services.AddSingleton(new AzureOpenAIClient(new Uri(aoaiEndPoint), new ApiKeyCredential(aoaiApiKey)));
builder.Services.AddSingleton(new AzureOpenAIModelOptions(aoaiDeploymentName));
builder.Services.AddSingleton<WebIqMcpToolProvider>();

var app = builder.Build();

// Configure the HTTP request pipeline.
app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

var baseballDataSampleService = app.Services.GetRequiredService<BaseballDataService>();
var machineLearningService = app.Services.GetRequiredService<PredictionEnginePool<MLBBaseballBatter, MLBHOFPrediction>>();
var azureOpenAIClient = app.Services.GetRequiredService<AzureOpenAIClient>();
var modelOptions = app.Services.GetRequiredService<AzureOpenAIModelOptions>();
var webIqMcpToolProvider = app.Services.GetRequiredService<WebIqMcpToolProvider>();
var loggerFactory = app.Services.GetRequiredService<ILoggerFactory>();
var aiAgents = new AIAgents(machineLearningService, azureOpenAIClient, modelOptions, webIqMcpToolProvider, loggerFactory, baseballDataSampleService);

// Define the API Endpoints

//app.MapGet("/Players", aiAgents.GetPlayers)
//    .WithName("GetPlayers")
//    .WithRequestTimeout(TimeSpan.FromSeconds(120));
app.MapPost("/BaseballPlayerAnalysisML", aiAgents.PerformBaseballPlayerAnalysisML)
    .WithName("BaseballPlayerAnalysisML")
    .WithRequestTimeout(TimeSpan.FromSeconds(120));
app.MapPost("/BaseballPlayerAnalysisMultipleAgents", aiAgents.PerformBaseballPlayerAnalysisMupltipleAgents)
    .WithName("BaseballPlayerAnalysisMultipleAgents")
    .WithRequestTimeout(TimeSpan.FromSeconds(180));

// Map the default API routes
app.MapDefaultEndpoints();
app.Run();

static string GetRequiredConnectionString(IConfiguration configuration, string name, params string[] aliases)
{
    foreach (var candidate in new[] { name }.Concat(aliases))
    {
        var value = configuration.GetConnectionString(candidate)
            ?? configuration[$"ConnectionStrings:{candidate}"]
            ?? Environment.GetEnvironmentVariable($"ConnectionStrings__{candidate}");

        if (!string.IsNullOrWhiteSpace(value))
        {
            return value;
        }
    }

    var expectedNames = string.Join(", ", new[] { name }.Concat(aliases).Select(candidate => $"ConnectionStrings__{candidate}"));
    throw new InvalidOperationException($"Missing required connection string. Set one of: {expectedNames}.");
}
