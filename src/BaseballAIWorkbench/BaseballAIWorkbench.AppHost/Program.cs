using Microsoft.Extensions.Hosting;
using Aspire.Hosting.Azure;

var builder = DistributedApplication.CreateBuilder(args);

// Add Key Vault Configuration
var keyVault = builder.AddConnectionString("AOAIEastUS2Gpt41");

// API Service
var apiService =
    builder.AddProject<Projects.BaseballAIWorkbench_ApiService>("apiservice")
    .WithReference(keyVault);

// Web Frontend
builder.AddProject<Projects.BaseballAIWorkbench_Web>("webfrontend")
    .WithExternalHttpEndpoints()
    .WithReference(apiService)
    .WaitFor(apiService);

// Build
builder.Build().Run();
