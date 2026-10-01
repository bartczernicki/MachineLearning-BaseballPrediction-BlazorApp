using BaseballAIWorkbench.Web;
using BaseballAIWorkbench.Web.Components;
using BaseballAIWorkbench.Common.MachineLearning;
using BaseballAIWorkbench.Web.Services;
using Microsoft.Azure.SignalR;

var builder = WebApplication.CreateBuilder(args);

// Add service defaults & Aspire client integrations.
builder.AddServiceDefaults();


// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddOutputCache();

builder.Services.AddHttpClient<BaseballApiClient>(client =>
{
    // The shared resilience pipeline owns the 210-second budget; avoid a competing HttpClient timeout.
    client.Timeout = Timeout.InfiniteTimeSpan;

    // This URL uses "https+http://" to indicate HTTPS is preferred over HTTP.
    // Learn more about service discovery scheme resolution at https://aka.ms/dotnet/sdschemes.
    client.BaseAddress = new("https+http://apiservice");
});

// -- Custom

// Aspire enables the managed service only for the deployed web frontend.
if (builder.Configuration.GetValue<bool>("AzureSignalR:Enabled"))
{
    var connectionString = builder.Configuration.GetConnectionString("signalr");
    if (string.IsNullOrWhiteSpace(connectionString))
    {
        throw new InvalidOperationException(
            "Azure SignalR is enabled, but ConnectionStrings:signalr is missing. Configure the Aspire SignalR resource reference.");
    }

    builder.Services.AddSignalR().AddAzureSignalR(options =>
    {
        options.ConnectionString = connectionString;
        options.ServerStickyMode = ServerStickyMode.Required;
    });
}

// Add Data service (provides historical Baseball data to the application)
builder.Services.AddSingleton<BaseballDataService>();

// Add the ML.NET models and a prediction object pool to the service
builder.Services.AddBaseballPredictionModels(
    MLModelPredictionType.InductedToHallOfFameGeneralizedAdditiveModel,
    MLModelPredictionType.OnHallOfFameBallotGeneralizedAdditiveModel);

// TODO: Add App Insights Telemetry

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseAntiforgery();

app.UseOutputCache();

app.MapStaticAssets();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.MapDefaultEndpoints();

app.Run();
