using Microsoft.Extensions.Hosting;
using Aspire.Hosting.Azure;
using Azure.Identity;

var builder = DistributedApplication.CreateBuilder(args);

// Add Key Vault Configuration
var keyVaultConnString = builder.AddConnectionString("AOAIEastUS2Gpt41");

// API Service
var apiService =
    builder.AddProject<Projects.BaseballAIWorkbench_ApiService>("apiservice")
    .WithReference(keyVaultConnString);

// Web Frontend
builder.AddProject<Projects.BaseballAIWorkbench_Web>("webfrontend")
    .WithExternalHttpEndpoints()
    .WithReference(apiService)
    .WaitFor(apiService);

// Build
builder.Build().Run();
