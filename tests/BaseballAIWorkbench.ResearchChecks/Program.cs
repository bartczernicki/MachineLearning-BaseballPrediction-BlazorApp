using BaseballAIWorkbench.ResearchChecks;

if (args.Contains("--prompt-eval"))
    return await PromptEvaluation.RunAsync(args);

if (args.Contains("--citations-only"))
{
    await CitationChecks.RunAsync();
    return 0;
}

if (args.Contains("--telemetry-only") || args.Contains("--telemetry-export"))
{
    await TelemetryChecks.RunAsync(exportToDashboard: args.Contains("--telemetry-export"));
    return 0;
}

ModelPackagingChecks.Run();
if (args.Contains("--models-only"))
    return 0;

await TelemetryChecks.RunAsync();
await HttpResilienceChecks.RunAsync();
await RetrievalChecks.RunAsync();
await CitationChecks.RunAsync();
await AgentChecks.RunAsync();
Console.WriteLine("PASS: all offline telemetry, HTTP resilience, research and agent orchestration checks.");
return 0;
