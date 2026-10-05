using BaseballAIWorkbench.WebChecks;

await MarkdownRenderingChecks.RunAsync();
await TypedAnalysisChecks.RunAsync();
Console.WriteLine("PASS: all offline typed analysis and sanitized narrative rendering checks.");
