using BaseballAIWorkbench.ResearchChecks;

if (args.Contains("--prompt-eval"))
    return await PromptEvaluation.RunAsync(args);

await RetrievalChecks.RunAsync();
await AgentChecks.RunAsync();
Console.WriteLine("PASS: all offline research and agent orchestration checks.");
return 0;
