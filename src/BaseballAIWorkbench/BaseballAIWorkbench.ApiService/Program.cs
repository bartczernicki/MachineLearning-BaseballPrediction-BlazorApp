using Azure.Identity;
using Azure.Security.KeyVault.Secrets;
using BaseballAIWorkbench.ApiService;
using BaseballAIWorkbench.ApiService.MachineLearning;
using BaseballAIWorkbench.ApiService.Services;
using Microsoft.Extensions.ML;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Agents;
using Microsoft.SemanticKernel.ChatCompletion;
using static BaseballAIWorkbench.ApiService.Util;


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

builder.Services.AddPredictionEnginePool<MLBBaseballBatter, MLBHOFPrediction>()
    .FromFile("InductedToHallOfFameGeneralizedAdditiveModel", modelPathInductedToHallOfFameGeneralizedAdditiveModel)
    .FromFile("OnHallOfFameBallotGeneralizedAdditiveModel", modelPathOnHallOfFameBallotGeneralizedAdditiveModel);

var aoaiEndPoint = Environment.GetEnvironmentVariable("ConnectionStrings__AOAIEndpoint");
var aoaiApiKey = Environment.GetEnvironmentVariable("ConnectionStrings__AOAIApiKey");
var aoaiDeploymentName = Environment.GetEnvironmentVariable("ConnectionStrings__AOAIModelDeploymentName");

var semanticKernel = Kernel.CreateBuilder()
    .AddAzureOpenAIChatCompletion(
        deploymentName: aoaiDeploymentName!,
        endpoint: aoaiEndPoint!,
        apiKey: aoaiApiKey!,
        serviceId: "azureOpenAIGeneralPurpose")
    .Build();
builder.Services.AddSingleton<Kernel>(builder => semanticKernel);


var app = builder.Build();

// Configure the HTTP request pipeline.
app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

var baseballDataSampleService = app.Services.GetRequiredService<BaseballDataService>();
var semanticKernelService = app.Services.GetRequiredService<Kernel>();
var aiAgents = new AIAgents(semanticKernelService, baseballDataSampleService);


// Define the API Endpoints

app.MapGet("/weatherforecast", () =>
{
    var baseballDataSampleService = app.Services.GetRequiredService<BaseballDataService>();
    //var aiAgents = new AIAgents(baseballDataSampleService);

    //var connStringKV = Environment.GetEnvironmentVariable("ConnectionStrings__AOAIEastUS2KeyVault");
    //Console.WriteLine(connStringKV);
    //var kvUri = new Uri(connStringKV!);
    //var kvClient = new SecretClient(kvUri, new DefaultAzureCredential());
    //var secretGpt41ConnString = kvClient.GetSecret("AOAIEastUS2Gpt41");

    var forecast = Enumerable.Range(1, 5).Select(index =>
        new WeatherForecast
        (
            DateOnly.FromDateTime(DateTime.Now.AddDays(index)),
            Random.Shared.Next(-20, 55),
            Summaries[Random.Shared.Next(Summaries.Length)]
        ))
        .ToArray();
    return forecast;
})
.WithName("GetWeatherForecast");

app.MapGet("/Players", aiAgents.GetPlayers)
    .WithName("GetPlayers");
app.MapPost("/BaseballPlayerAnalysis", aiAgents.PerformBaseballPlayerAnalysis)
    .WithName("BaseballPlayerAnalysis");

// Map the default API routes
app.MapDefaultEndpoints();
app.Run();

static async Task<IResult> GetPlayers(BaseballDataService service)
{
    var players = await service.GetBaseballData();
    var count = players.Count;
    return TypedResults.Ok(count);
}

static async Task<IResult> PerformBaseballPlayerAnalysis(MLBBaseballBatter batter, BaseballDataService service)
{
    var test = batter.FullPlayerName + batter.H;
    return TypedResults.Ok(test);
}