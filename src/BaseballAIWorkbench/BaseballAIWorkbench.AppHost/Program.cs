using Microsoft.Extensions.Hosting;
using Aspire.Hosting.Azure;
using Azure.Identity;

var builder = DistributedApplication.CreateBuilder(args);

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
builder.AddProject<Projects.BaseballAIWorkbench_Web>("webfrontend")
    .WithHttpsEndpoint(port: 7295, name: "https")
    .WithHttpEndpoint(port: 5044, name: "http")
    .WithExternalHttpEndpoints()
    .WithReference(apiService)
    .WaitFor(apiService);

// Build
builder.Build().Run();
