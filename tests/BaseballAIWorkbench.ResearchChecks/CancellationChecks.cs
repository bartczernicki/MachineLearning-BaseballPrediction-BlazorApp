using System.Net;
using System.Net.Http.Json;
using BaseballAIWorkbench.ApiService;
using BaseballAIWorkbench.Common.Agents;
using BaseballAIWorkbench.Common.MachineLearning;

namespace BaseballAIWorkbench.ResearchChecks;

internal static class CancellationChecks
{
    private static readonly TimeSpan GuardTimeout = TimeSpan.FromSeconds(10);

    internal static async Task RunAsync(Func<ScriptedResponses, AIAgents> createAgents,
        MLBBaseballBatter batter, McpFixture mcp)
    {
        foreach (var multiple in new[] { false, true })
        foreach (var serverTimeout in new[] { false, true })
        {
            string[] selected = multiple
                ? ["BaseballEncyclopedia", "BaseballStatistician"]
                : ["BaseballEncyclopedia"];
            var started = selected.ToDictionary(agent => agent, _ => Check.Signal());
            var canceled = selected.ToDictionary(agent => agent, _ => Check.Signal());
            var qCalls = 0;
            using var transport = new ScriptedResponses(async (agent, _, token) =>
            {
                if (agent == "Q")
                {
                    Interlocked.Increment(ref qCalls);
                    return ScriptedResponses.Message("Unexpected quantitative synthesis after cancellation.");
                }

                started[agent].TrySetResult();
                try
                {
                    await Task.Delay(Timeout.InfiniteTimeSpan, token);
                    throw new InvalidOperationException("The blocked model call unexpectedly completed.");
                }
                catch (OperationCanceledException) when (token.IsCancellationRequested)
                {
                    canceled[agent].TrySetResult();
                    throw;
                }
            });
            var agents = createAgents(transport);
            Func<AgenticAnalysisConfig, CancellationToken, Task<IResult>> handler = multiple
                ? agents.PerformBaseballPlayerAnalysisMupltipleAgents
                : agents.PerformBaseballPlayerAnalysisML;
            var config = new AgenticAnalysisConfig { AgentsToUse = selected.ToList(), BaseballBatter = batter };
            var allStarted = Task.WhenAll(started.Values.Select(signal => signal.Task));
            var before = mcp.Disposals;
            var scenario = $"{(multiple ? "Multiple-agent" : "Single-agent")} {(serverTimeout ? "server timeout" : "request cancellation")}";

            if (serverTimeout)
                await AssertServerTimeoutAsync(handler, config, allStarted, scenario);
            else
                await AssertRequestCancellationAsync(handler, config, allStarted, scenario);

            await Task.WhenAll(canceled.Values.Select(signal => signal.Task)).WaitAsync(GuardTimeout);
            Check.That(mcp.Disposals == before + 1, $"{scenario} disposes the MCP scope before completing");
            Check.That(Volatile.Read(ref qCalls) == 0, $"{scenario} never starts Agent Q");
        }

        Console.WriteLine("PASS cancellation: both real analysis handlers propagate cancellation, cancel every pending model call, dispose MCP, skip Q, and return HTTP 504 for request timeouts.");
    }

    private static async Task AssertRequestCancellationAsync(
        Func<AgenticAnalysisConfig, CancellationToken, Task<IResult>> handler,
        AgenticAnalysisConfig config, Task allStarted, string scenario)
    {
        using var requestCancellation = new CancellationTokenSource();
        var operation = handler(config, requestCancellation.Token);
        try
        {
            await allStarted.WaitAsync(GuardTimeout);
            requestCancellation.Cancel();
            var propagatedCancellation = false;
            try
            {
                await operation.WaitAsync(GuardTimeout);
            }
            catch (OperationCanceledException) when (requestCancellation.IsCancellationRequested)
            {
                propagatedCancellation = true;
            }

            Check.That(propagatedCancellation, $"{scenario} propagates OperationCanceledException instead of producing a 500 result");
        }
        finally
        {
            requestCancellation.Cancel();
        }
    }

    private static async Task AssertServerTimeoutAsync(
        Func<AgenticAnalysisConfig, CancellationToken, Task<IResult>> handler,
        AgenticAnalysisConfig config, Task allStarted, string scenario)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Production" });
        builder.Logging.ClearProviders();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Services.AddProblemDetails();
        builder.Services.AddRequestTimeouts();
        await using var app = builder.Build();
        app.UseExceptionHandler();
        app.UseRouting();
        app.UseRequestTimeouts();
        app.MapPost("/analysis", handler).WithRequestTimeout(TimeSpan.FromSeconds(2));
        await app.StartAsync();
        try
        {
            using var client = new HttpClient { Timeout = GuardTimeout };
            var responseTask = client.PostAsJsonAsync(app.Urls.Single() + "/analysis", config);
            await allStarted.WaitAsync(GuardTimeout);
            using var response = await responseTask.WaitAsync(GuardTimeout);
            Check.That(response.StatusCode == HttpStatusCode.GatewayTimeout,
                $"{scenario} returns HTTP 504, received {(int)response.StatusCode}");
        }
        finally
        {
            await app.StopAsync();
        }
    }
}
