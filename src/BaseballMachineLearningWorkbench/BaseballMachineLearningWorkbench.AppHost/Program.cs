var builder = DistributedApplication.CreateBuilder(args);

var apiService = builder.AddProject<Projects.BaseballMachineLearningWorkbench_ApiService>("apiservice");

builder.AddProject<Projects.BaseballMachineLearningWorkbench_Web>("webfrontend")
    .WithExternalHttpEndpoints()
    .WithReference(apiService)
    .WaitFor(apiService);

builder.Build().Run();
