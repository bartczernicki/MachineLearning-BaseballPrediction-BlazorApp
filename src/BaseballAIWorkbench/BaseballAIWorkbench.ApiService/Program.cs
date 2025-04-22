using Azure.Identity;
using Azure.Security.KeyVault.Secrets;
using BaseballAIWorkbench.ApiService;
using BaseballAIWorkbench.Common.MachineLearning;
using BaseballAIWorkbench.ApiService.Services;
using Microsoft.Extensions.ML;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Agents;
using Microsoft.SemanticKernel.ChatCompletion;
using static BaseballAIWorkbench.ApiService.Util;
using Microsoft.Extensions.Logging.Console;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.SemanticKernel.Agents.AzureAI;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.SemanticKernel.Connectors.AzureOpenAI;


var builder = WebApplication.CreateBuilder(args);

// Add service defaults & Aspire client integrations.
builder.AddServiceDefaults();

builder.Configuration.AddAzureKeyVaultSecrets(connectionName: "AOAIEastUS2Gpt41");

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

var sharedCredential = new DefaultAzureCredential(false);
var aoaiEndPoint = Environment.GetEnvironmentVariable("ConnectionStrings__AOAIEndpoint");
var aoaiApiKey = Environment.GetEnvironmentVariable("ConnectionStrings__AOAIApiKey");
var aoaiDeploymentName = Environment.GetEnvironmentVariable("ConnectionStrings__AOAIModelDeploymentName");

// Add custom Http Client
HttpClient httpClient = new HttpClient();
httpClient.Timeout = TimeSpan.FromMinutes(2);

var semanticKernel = Kernel.CreateBuilder()
    .AddAzureOpenAIChatCompletion(
        deploymentName: aoaiDeploymentName!,
        endpoint: aoaiEndPoint!,
        apiKey: aoaiApiKey!,
        serviceId: "azureOpenAIGeneralPurpose",
        httpClient: httpClient)
    .Build();
builder.Services.AddSingleton<Kernel>(builder => semanticKernel);


AzureOpenAIChatCompletionService reasoningCompletionService = new(
        deploymentName: "o4-mini",
        endpoint: aoaiEndPoint!,
        apiKey: aoaiApiKey!,
        httpClient: httpClient
);

var app = builder.Build();


// Configure the HTTP request pipeline.
app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

var baseballDataSampleService = app.Services.GetRequiredService<BaseballDataService>();
var semanticKernelService = app.Services.GetRequiredService<Kernel>();
var machineLearningService = app.Services.GetRequiredService<PredictionEnginePool<MLBBaseballBatter, MLBHOFPrediction>>();
var aiAgents = new AIAgents(sharedCredential, machineLearningService, reasoningCompletionService,
    semanticKernelService, baseballDataSampleService);


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