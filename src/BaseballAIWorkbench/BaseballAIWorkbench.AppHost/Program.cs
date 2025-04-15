var builder = DistributedApplication.CreateBuilder(args);

var apiService = builder.AddProject<Projects.BaseballAIWorkbench_ApiService>("apiservice");

builder.AddProject<Projects.BaseballAIWorkbench_Web>("webfrontend")
    .WithExternalHttpEndpoints()
    .WithReference(apiService)
    .WaitFor(apiService);

builder.Build().Run();
