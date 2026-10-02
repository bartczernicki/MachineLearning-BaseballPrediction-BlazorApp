using Microsoft.Extensions.Hosting;
using Aspire.Hosting.Azure;
using Aspire.Hosting.Pipelines;
using Azure.Identity;
using Microsoft.Extensions.Configuration;

var builder = DistributedApplication.CreateBuilder(args);

if (builder.ExecutionContext.IsPublishMode && !builder.Environment.IsDevelopment())
{
    // Publish/deploy defaults to Production, which does not automatically load user secrets.
    // Fill missing values so deployment settings, environment variables, and CLI arguments win.
    using var userSecrets = new ConfigurationManager();
    userSecrets.AddUserSecrets<Program>(optional: true);
    var defaults = userSecrets.AsEnumerable()
        .Where(setting => builder.Configuration[setting.Key] is null)
        .ToArray();

    // Append instead of inserting: insertion reloads Aspire's already-consumed deployment-state stream.
    builder.Configuration.AddInMemoryCollection(defaults);
}

var containerAppEnvironment = builder.AddAzureContainerAppEnvironment("baseball-env");

// Add Key Vault Configuration
// var keyVaultConnString = builder.AddConnectionString("AOAIEastUS2KeyVault");

// Add AOAI Configuration
var aoaiEndPoint = builder.AddConnectionString("AOAIEndpoint");
var aoaiApiKey = builder.AddConnectionString("AOAIAPIKey");
var aoaiDeploymentName = builder.AddConnectionString("AOAIModelDeploymentName");
var webIQMcpApiKey = builder.AddConnectionString("WebIQMcpApiKey");

// API Service
var apiService =
    builder.AddProject<Projects.BaseballAIWorkbench_ApiService>("apiservice")
    // .WithReference(keyVaultConnString)
    .WithReference(aoaiEndPoint)
    .WithReference(aoaiApiKey)
    .WithReference(aoaiDeploymentName)
    .WithReference(webIQMcpApiKey);

// Web Frontend
var webFrontend = builder.AddProject<Projects.BaseballAIWorkbench_Web>("webfrontend")
    .WithHttpsEndpoint(port: 7295, name: "https")
    .WithHttpEndpoint(port: 5044, name: "http")
    .WithExternalHttpEndpoints()
    .WithReference(apiService)
    .WaitFor(apiService);

// Zero minimum replicas allows idle apps to stop. The 3,600-second (one-hour) cooldown
// applies before the final replica scales to zero; actual timing depends on the scaler.
// A new HTTP request can start a fresh replica, with cold-start latency.
apiService.PublishAsAzureContainerApp((_, app) =>
{
    app.Template.Scale.MinReplicas = 0;
    app.Template.Scale.CooldownPeriod = 3600;
});

// Scaling the frontend to zero discards in-memory Blazor circuits and disconnects users.
// Azure SignalR does not preserve that session state; returning users may need to reload.
webFrontend.PublishAsAzureContainerApp((_, app) =>
{
    app.Template.Scale.MinReplicas = 0;
    app.Template.Scale.CooldownPeriod = 3600;
});

// Use the managed SignalR service only in Azure deployments; local Blazor connections stay local.
if (builder.ExecutionContext.IsPublishMode)
{
    // Bind the existing environment explicitly so Front Door can resolve the origin's
    // published hostname even when Aspire inspects steps before preparing deployment targets.
    webFrontend.WithComputeEnvironment(containerAppEnvironment);

    var signalR = builder.AddAzureSignalR("signalr");
    webFrontend.WithReference(signalR)
        .WithEnvironment("AzureSignalR__Enabled", "true");

    // Provision Front Door only when publishing to Azure; local execution needs no edge service.
    // Only the web frontend is an origin. API traffic stays internal, and negotiated SignalR
    // connections continue directly through Azure SignalR rather than through the edge cache.
    var frontDoor = builder.AddAzureFrontDoor("frontdoor")
        .WithOrigin(webFrontend)
        .ConfigureInfrastructure(FrontDoorConfiguration.Configure);

    var frontDoorEndpoint = frontDoor.Resource.GetEndpointUrl(webFrontend.Resource.Name);
    frontDoor.WithUrl($"{frontDoorEndpoint}", "Web frontend (Front Door)");

    // URL annotations alone do not appear in Aspire's deployment summary. Resolve the
    // existing module output after provisioning and print the generated *.azurefd.net URL.
#pragma warning disable ASPIREPIPELINES001
    frontDoor.WithPipelineStepFactory("print-frontdoor-summary", async context =>
    {
        var url = await frontDoorEndpoint.GetValueAsync(context.CancellationToken)
            ?? throw new InvalidOperationException("The Front Door endpoint URL was not returned by deployment.");
        context.Summary.Add("Web frontend (Front Door)", new MarkdownString($"[{url}]({url})"));
    }, dependsOn: [$"provision-{frontDoor.Resource.Name}"], requiredBy: [WellKnownPipelineSteps.Deploy],
        description: "Shows the generated Front Door URL in the deployment summary.");
#pragma warning restore ASPIREPIPELINES001
}

// Build
builder.Build().Run();
