using Microsoft.Extensions.Hosting;
using Aspire.Hosting.Azure;
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

builder.AddAzureContainerAppEnvironment("baseball-env");

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

// Use the managed SignalR service only in Azure deployments; local Blazor connections stay local.
if (builder.ExecutionContext.IsPublishMode)
{
    var signalR = builder.AddAzureSignalR("signalr");
    webFrontend.WithReference(signalR)
        .WithEnvironment("AzureSignalR__Enabled", "true");
}

// Build
builder.Build().Run();
