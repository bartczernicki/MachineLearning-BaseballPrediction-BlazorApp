using BaseballAIWorkbench.ResearchChecks;

if (args.Contains("--prompt-eval"))
    return await PromptEvaluation.RunAsync(args);

ModelPackagingChecks.Run();
if (args.Contains("--models-only"))
    return 0;

await RetrievalChecks.RunAsync();
await AgentChecks.RunAsync();
Console.WriteLine("PASS: all offline research and agent orchestration checks.");
return 0;
