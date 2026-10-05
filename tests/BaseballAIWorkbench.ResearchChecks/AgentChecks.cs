using System.ClientModel;
using System.ClientModel.Primitives;
using System.Collections.Concurrent;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using BaseballAIWorkbench.ApiService;
using BaseballAIWorkbench.ApiService.Services;
using BaseballAIWorkbench.Common.Agents;
using BaseballAIWorkbench.Common.MachineLearning;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.ML;
using OpenAI;

namespace BaseballAIWorkbench.ResearchChecks;

internal static class AgentChecks
{
    private const string Encyclopedia = "BaseballEncyclopedia";
    private const string Statistician = "BaseballStatistician";
    private const string Ml = "MachineLearningExpert";
    private const string ReadTool = "read_commentary_source";
    // Intentionally false prose must never override the separately returned server result.
    private const string FinalQuantitativeAnswer = "### Summary\nFinal fixture quantitative answer claims 99.99% with a 0.00–100.00% range.";
    private const string SourcesHeading = "### Encyclopedia Sources";
    private static readonly EncyclopediaCitation[] ExpectedCitations =
    [
        new("First commentary", "https://example.com/1"),
        new("Second commentary", "https://example.com/2")
    ];

    public static async Task RunAsync()
    {
        var batter = File.ReadLines(Path.Combine(AppContext.BaseDirectory, "Data/MLBBaseballBattersPositionPlayers.csv"))
            .Skip(1).Select(MLBBaseballBatter.FromCsv).Single(b => b.FullPlayerName == "Mike Trout");
        var services = new ServiceCollection().AddLogging();
        services.AddBaseballPredictionModels();
        using var serviceProvider = services.BuildServiceProvider();
        var pool = serviceProvider.GetRequiredService<PredictionEnginePool<MLBBaseballBatter, MLBHOFPrediction>>();
        await using var mcp = await McpFixture.StartAsync();
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["MAIGrounding-MCP:url"] = mcp.Url,
            ["ConnectionStrings:WebIQMcpApiKey"] = "offline-fixture-key"
        }).Build();
        var provider = new WebIqMcpToolProvider(config, NullLoggerFactory.Instance);
        AIAgents Agents(ScriptedResponses transport) => new(pool,
            new OpenAIClient(new ApiKeyCredential("offline-fixture-key"), new OpenAIClientOptions
            {
                Endpoint = new Uri("http://fixture.invalid/openai/v1/"),
                Transport = new HttpClientPipelineTransport(new HttpClient(transport)),
                RetryPolicy = new ClientRetryPolicy(0)
            }), new AzureOpenAIModelOptions("fake-deployment"), provider, NullLoggerFactory.Instance, new BaseballDataService());
        AgenticAnalysisConfig Config(params string[] agents) => new() { AgentsToUse = agents.ToList(), BaseballBatter = batter };
        var mlBallot = new[]
        {
            MLModelPredictionType.OnHallOfFameBallotGeneralizedAdditiveModel,
            MLModelPredictionType.OnHallOfFameBallotLightGbmModel,
            MLModelPredictionType.OnHallOfFameBallotFastTreeModel
        }.Select(model => pool.Predict(model.ToString(), batter).Probability).Average();
        var mlInduction = new[]
        {
            MLModelPredictionType.InductedToHallOfFameGeneralizedAdditiveModel,
            MLModelPredictionType.InductedToHallOfFameLightGbmModel,
            MLModelPredictionType.InductedToHallOfFameFastTreeModel
        }.Select(model => pool.Predict(model.ToString(), batter).Probability).Average();

        var encyclopediaCalls = 0;
        using (var transport = new ScriptedResponses((agent, request, _) =>
        {
            Check.That(agent == Encyclopedia, "Single Encyclopedia request stays on its agent");
            Check.That(request.GetProperty("reasoning").GetProperty("effort").GetString() == "medium", "Encyclopedia explicitly uses medium reasoning");
            var call = Interlocked.Increment(ref encyclopediaCalls);
            if (call <= 2)
            {
                Check.That(ToolNames(request).SequenceEqual([ReadTool]), "Only the allowlisted read tool is advertised to Encyclopedia");
                return Task.FromResult(call == 1
                    ? ScriptedResponses.MessageAndFunction("Intermediate research commentary, not final JSON.", ReadTool, new { sourceId = "S1" }, "read_1")
                    : ScriptedResponses.Function(ReadTool, new { sourceId = "S2" }, "read_2"));
            }
            Check.That(call == 3, "Two tool rounds are followed by one final synthesis call");
            Check.That(!ToolNames(request).Any() || (request.TryGetProperty("tool_choice", out var choice) && choice.ToString() == "none"), "Final synthesis cannot request tools");
            var history = string.Join('\n', Strings(request));
            Check.That(history.Contains("read_1") && history.Contains("read_2") && history.Contains("Full article evidence"), "Final synthesis retains complete function-call and tool-result history");
            Check.That(history.Contains("Intermediate research commentary"), "Final synthesis retains intermediate assistant text");
            return Task.FromResult(ScriptedResponses.Message(AgentOutput(Encyclopedia)));
        }))
        {
            var before = mcp.Disposals;
            var result = await Agents(transport).PerformBaseballPlayerAnalysisML(Config(Encyclopedia)).WaitAsync(TimeSpan.FromSeconds(20));
            Check.That(result is Ok<AgenticAnalysisResponse> { Value: { AnalysisMarkdown: var markdown } } &&
                markdown == EncyclopediaCitations.AppendTo(Markdown(Encyclopedia), ExpectedCitations) && encyclopediaCalls == 3,
                "Bounded Encyclopedia analysis unwraps the final structured answer and appends its retrieved citations");
            AssertCitations(Response(result).AnalysisMarkdown, Markdown(Encyclopedia));
            AssertSingle(Response(result), Encyclopedia, 0.2, 0.1);
            Check.That(mcp.Disposals == before + 1, "Encyclopedia disposes MCP scope after synthesis");
        }

        const string uncitedAnalysis = "### Summary\nNo source links were cited in this fixture assessment.";
        using (var transport = new ScriptedResponses((_, _, _) => Task.FromResult(
            ScriptedResponses.Message(Structured(uncitedAnalysis, 0.2, 0.1)))))
        {
            var result = await Agents(transport).PerformBaseballPlayerAnalysisML(Config(Encyclopedia)).WaitAsync(TimeSpan.FromSeconds(20));
            Check.That(result is Ok<AgenticAnalysisResponse> { Value: { AnalysisMarkdown: var markdown } } &&
                markdown == EncyclopediaCitations.AppendTo(uncitedAnalysis, []),
                "Standalone Encyclopedia retains narrative and explicit empty citations alongside typed estimates");
            AssertEmptyCitations(Response(result).AnalysisMarkdown);
            AssertSingle(Response(result), Encyclopedia, 0.2, 0.1);
        }

        foreach (var selected in new[] { Statistician, Ml })
        {
            using var transport = new ScriptedResponses((agent, _, _) => Task.FromResult(ScriptedResponses.Message(AgentOutput(agent))));
            var result = await Agents(transport).PerformBaseballPlayerAnalysisML(Config(selected)).WaitAsync(TimeSpan.FromSeconds(20));
            Check.That(result is Ok<AgenticAnalysisResponse> { Value: { AnalysisMarkdown: var markdown } } && markdown == Markdown(selected),
                $"Single {selected} endpoint returns narrative alongside its typed estimate");
            AssertSingle(Response(result), selected, selected == Ml ? (double)mlBallot : 0.9,
                selected == Ml ? (double)mlInduction : 0.8);
        }

        // Real orchestration, deliberately completing research in reverse selection order.
        foreach (var selected in new[] { new[] { Encyclopedia, Statistician }, new[] { Encyclopedia, Statistician, Ml }, new[] { Statistician, Ml } })
        {
            var started = selected.ToDictionary(a => a, _ => Check.Signal());
            var releases = selected.ToDictionary(a => a, _ => new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously));
            var qRequests = new ConcurrentQueue<JsonElement>();
            using var transport = new ScriptedResponses(async (agent, request, token) =>
            {
                if (agent == "Q") return QuantitativeResponse(request, qRequests);
                started[agent].TrySetResult();
                return ScriptedResponses.Message(await releases[agent].Task.WaitAsync(token));
            });
            var before = mcp.Disposals;
            var operation = Agents(transport).PerformBaseballPlayerAnalysisMupltipleAgents(Config(selected));
            await Task.WhenAll(started.Values.Select(s => s.Task)).WaitAsync(TimeSpan.FromSeconds(20));
            Check.That(qRequests.IsEmpty, "All selected research agents run before Agent Q");
            foreach (var agent in selected.Reverse().SkipLast(1)) releases[agent].TrySetResult(AgentOutput(agent));
            await Task.Delay(50);
            Check.That(!operation.IsCompleted && qRequests.IsEmpty, "Agent Q waits for the final outstanding research result");
            releases[selected[0]].TrySetResult(AgentOutput(selected[0]));
            var result = await operation.WaitAsync(TimeSpan.FromSeconds(20));
            var expected = selected.Contains(Encyclopedia)
                ? EncyclopediaCitations.AppendTo(FinalQuantitativeAnswer, ExpectedCitations)
                : FinalQuantitativeAnswer;
            Check.That(result is Ok<AgenticAnalysisResponse> { Value: { AnalysisMarkdown: var markdown } } && markdown == expected && qRequests.Count == 1,
                "Q explains the server calculation in exactly one request and returns Markdown");
            if (selected.Contains(Encyclopedia))
                AssertCitations(Response(result).AnalysisMarkdown, FinalQuantitativeAnswer);
            else
                Check.That(!Response(result).AnalysisMarkdown.Contains(SourcesHeading),
                    "Combined analyses without Encyclopedia do not receive an Encyclopedia sources footer");
            var prompt = string.Join('\n', Strings(qRequests.First()));
            var positions = selected.Select(a => prompt.IndexOf($"RESULT_{a}_END", StringComparison.Ordinal)).ToArray();
            Check.That(positions.All(p => p >= 0) && positions.SequenceEqual(positions.Order()), "Q receives complete results in selection order");
            Check.That(selected.All(a => prompt.Split($"RESULT_{a}_END").Length == 2), "Every research result is included exactly once");
            AssertCalculation(qRequests.Single(),
                selected.Select(agent => agent == Ml ? (double)mlBallot : agent == Encyclopedia ? 0.2 : 0.9).ToArray(),
                selected.Select(agent => agent == Ml ? (double)mlInduction : agent == Encyclopedia ? 0.1 : 0.8).ToArray());
            var response = Response(result);
            Check.That(response.AgentEstimates.Select(estimate => estimate.AgentType).SequenceEqual(selected)
                && response.AgentEstimates.All(estimate => estimate.IncludedInAggregate) && response.Notices.Length == 0,
                "The API returns every agent's estimates in selection order with its actual aggregate participation");
            AssertAggregate(response,
                selected.Select(agent => agent == Ml ? (double)mlBallot : agent == Encyclopedia ? 0.2 : 0.9).ToArray(),
                selected.Select(agent => agent == Ml ? (double)mlInduction : agent == Encyclopedia ? 0.1 : 0.8).ToArray());
            if (selected.Contains(Ml))
            {
                Check.That(prompt.Contains("| Ballot Appearance | 1%") && mlBallot != 0.01f,
                    "Conflicting ML prose reaches Q while the exact model average supplies the calculation");
                var mlEstimate = response.AgentEstimates.Single(estimate => estimate.AgentType == Ml);
                Check.That(mlEstimate.BallotAppearanceProbability == mlBallot && mlEstimate.InductionProbability == mlInduction,
                    "ML prose disagreement cannot change the exact numeric averages returned to Razor");
            }
            Check.That(mcp.Disposals == before + (selected.Contains(Encyclopedia) ? 1 : 0),
                "Parallel success disposes an MCP scope only when Encyclopedia participates");
        }

        foreach (var abstention in new[] { "both", "ballot", "induction", "all" })
        {
            var qRequests = new ConcurrentQueue<JsonElement>();
            using var transport = new ScriptedResponses((agent, request, _) => Task.FromResult(agent == "Q"
                ? QuantitativeResponse(request, qRequests)
                : ScriptedResponses.Message(agent == Encyclopedia || abstention == "all"
                    ? Structured(Markdown(agent, abstain: true), abstention == "induction" ? 0.2 : null,
                        abstention == "ballot" ? 0.1 : null, "Insufficient evidence: no attributable commentary supports an estimate.")
                    : AgentOutput(agent))));
            var result = await Agents(transport).PerformBaseballPlayerAnalysisMupltipleAgents(Config(Statistician, Encyclopedia)).WaitAsync(TimeSpan.FromSeconds(20));
            if (abstention == "all")
                Check.That(result is ProblemHttpResult && qRequests.IsEmpty, "All typed abstentions return an error without invoking Q");
            else
            {
                Check.That(result is Ok<AgenticAnalysisResponse> { Value: { AnalysisMarkdown: var markdown } } &&
                    markdown == EncyclopediaCitations.AppendTo(FinalQuantitativeAnswer, ExpectedCitations) && qRequests.Count == 1,
                    "Q combines the remaining usable agent and retains citations when either Encyclopedia outcome abstains");
                AssertCitations(Response(result).AnalysisMarkdown, FinalQuantitativeAnswer);
                var prompt = string.Join('\n', Strings(qRequests.First()));
                Check.That(prompt.Contains("Omitted agents:") && prompt.Contains("Insufficient evidence: no attributable commentary supports an estimate."), "Typed abstention preserves its explicit omission reason");
                Check.That(prompt.Contains("RESULT_BaseballEncyclopedia_END"), "An abstaining agent's full evidence explanation still reaches Q");
                AssertCalculation(qRequests.Single(), [0.9], [0.8]);
                var response = Response(result);
                AssertAggregate(response, [0.9], [0.8]);
                var excluded = response.AgentEstimates.Single(estimate => estimate.AgentType == Encyclopedia);
                Check.That(response.AgentEstimates.Length == 2 && !excluded.IncludedInAggregate
                    && excluded.BallotAppearanceProbability == (abstention == "induction" ? 0.2 : null)
                    && excluded.InductionProbability == (abstention == "ballot" ? 0.1 : null)
                    && !string.IsNullOrWhiteSpace(excluded.AbstentionReason),
                    "Partial abstention retains the available numeric estimate and explanation while excluding the pair");
            }
        }

        // Presentation is never an input to the deterministic calculation.
        foreach (var test in new[]
        {
            (Name: "alternate heading", Markdown: Markdown(Encyclopedia).Replace("### Probability Assessment", "## Probability Assessment"), Ballot: 0.8, Induction: 0.5),
            (Name: "no table", Markdown: "## Summary\nA prose-only assessment with no probability table.", Ballot: 0.99, Induction: 0.92),
            (Name: "reordered columns", Markdown: "| Probability | Criterion |\n|---|---|\n| 99% | Induction |\n| 3% | Ballot Appearance |", Ballot: 0.8, Induction: 0.5),
            (Name: "subjective ranges", Markdown: Markdown(Encyclopedia).Replace("20%", "60–95%").Replace("10%", "30–70%"), Ballot: 0.8, Induction: 0.5),
            (Name: "display inequalities", Markdown: Markdown(Encyclopedia).Replace("20%", ">99.9%").Replace("10%", "<0.1%"), Ballot: 0.999987654321, Induction: 0.00004),
            (Name: "full numeric precision", Markdown: Markdown(Encyclopedia).Replace("20%", "92.46%").Replace("10%", "12.35%"), Ballot: 0.9245678912345678, Induction: 0.12345678912345678),
            (Name: "probability boundaries", Markdown: "Ballot 100%, induction 0%.", Ballot: 1.0, Induction: 0.0)
        })
        {
            var qRequests = new ConcurrentQueue<JsonElement>();
            using var transport = new ScriptedResponses((agent, request, _) => Task.FromResult(agent == "Q"
                ? QuantitativeResponse(request, qRequests)
                : ScriptedResponses.Message(agent == Encyclopedia ? Structured(test.Markdown, test.Ballot, test.Induction) : AgentOutput(agent))));
            var result = await Agents(transport).PerformBaseballPlayerAnalysisMupltipleAgents(Config(Statistician, Encyclopedia)).WaitAsync(TimeSpan.FromSeconds(20));
            Check.That(result is Ok<AgenticAnalysisResponse> && qRequests.Count == 1, $"Typed case '{test.Name}' reaches one explanatory Q request");
            AssertCalculation(qRequests.Single(), [0.9, test.Ballot], [0.8, test.Induction]);
            AssertAggregate(Response(result), [0.9, test.Ballot], [0.8, test.Induction]);
            var prompt = string.Join('\n', Strings(qRequests.First()));
            if (test.Name == "no table")
            {
                Check.That(Response(result).AnalysisMarkdown == EncyclopediaCitations.AppendTo(FinalQuantitativeAnswer, []),
                    "Combined analysis appends an empty sources footer when Encyclopedia cites no links");
                AssertEmptyCitations(Response(result).AnalysisMarkdown);
            }
            var returnedEstimate = Response(result).AgentEstimates.Single(estimate => estimate.AgentType == Encyclopedia);
            Check.That(returnedEstimate.BallotAppearanceProbability == test.Ballot
                && returnedEstimate.InductionProbability == test.Induction,
                $"Typed case '{test.Name}' retains full precision independently of narrative tables and display inequalities");
            var omissionLine = prompt.Split('\n').Single(line => line.StartsWith("Omitted agents:", StringComparison.Ordinal));
            Check.That(omissionLine.Contains("None"), $"Typed case '{test.Name}' never omits an agent due to Markdown formatting");
        }

        await VerifyNumericalSeparationAsync(Agents, Config(Statistician, Encyclopedia));
        await VerifyNarrativeFallbackAsync(Agents, batter, mlBallot, mlInduction);

        foreach (var invalid in InvalidResponses())
        {
            var qRequests = new ConcurrentQueue<JsonElement>();
            using var transport = new ScriptedResponses((agent, request, _) => Task.FromResult(agent == "Q"
                ? QuantitativeResponse(request, qRequests)
                : ScriptedResponses.Message(agent == Encyclopedia ? invalid.Response : AgentOutput(agent))));
            var before = mcp.Disposals;
            var result = await Agents(transport).PerformBaseballPlayerAnalysisMupltipleAgents(Config(Statistician, Encyclopedia)).WaitAsync(TimeSpan.FromSeconds(20));
            Check.That(result is ProblemHttpResult && qRequests.IsEmpty, $"Invalid structured response '{invalid.Name}' fails without Q or Markdown fallback");
            Check.That(mcp.Disposals == before + 1, $"Invalid structured response '{invalid.Name}' still disposes MCP");
        }

        foreach (var malformedResponse in new[] { false, true })
        {
            var statStarted = Check.Signal();
            var releaseStat = Check.Signal();
            var encFailed = Check.Signal();
            var qCalls = 0;
            using var transport = new ScriptedResponses(async (agent, _, token) =>
            {
                if (agent == "Q") { Interlocked.Increment(ref qCalls); return ScriptedResponses.Message("Unexpected Q"); }
                if (agent == Encyclopedia)
                {
                    encFailed.TrySetResult();
                    return malformedResponse ? ScriptedResponses.Message("not valid structured JSON") : ScriptedResponses.Error();
                }
                statStarted.TrySetResult();
                await releaseStat.Task.WaitAsync(token);
                return ScriptedResponses.Message(AgentOutput(Statistician));
            });
            var before = mcp.Disposals;
            var operation = Agents(transport).PerformBaseballPlayerAnalysisMupltipleAgents(Config(Encyclopedia, Statistician));
            await Task.WhenAll(statStarted.Task, encFailed.Task).WaitAsync(TimeSpan.FromSeconds(20));
            await Task.Delay(100);
            Check.That(!operation.IsCompleted && qCalls == 0, "Research failure does not abandon other in-flight agents");
            releaseStat.TrySetResult();
            Check.That(await operation.WaitAsync(TimeSpan.FromSeconds(20)) is ProblemHttpResult && qCalls == 0, "Research failure skips Agent Q");
            Check.That(mcp.Disposals == before + 1, "MCP cleanup occurs on model failure");
        }
        await CancellationChecks.RunAsync(Agents, batter, mcp);
        Console.WriteLine("PASS agents: typed API responses, exact estimates, separate sensitivity/disagreement, narrative fallback, retained Encyclopedia citations, parallel order/cleanup, abstention and failure semantics.");
    }

    private static AgenticAnalysisResponse Response(IResult result) => result is Ok<AgenticAnalysisResponse> { Value: { } response }
        ? response : throw new InvalidOperationException("Expected a successful typed analysis response.");

    private static void AssertSingle(AgenticAnalysisResponse response, string agentType, double ballot, double induction,
        bool hasNotice = false)
    {
        Check.That(response.Aggregate is null && response.AgentEstimates.Length == 1,
            "A single-agent response includes its estimate without manufacturing a combined result");
        var estimate = response.AgentEstimates.Single();
        Check.That(estimate.AgentType == agentType && !string.IsNullOrWhiteSpace(estimate.AgentName)
            && estimate.BallotAppearanceProbability == ballot && estimate.InductionProbability == induction
            && !estimate.IncludedInAggregate && estimate.AbstentionReason is null,
            "Single-agent numeric values are exact and cannot claim participation in an absent aggregate");
        Check.That(hasNotice ? response.Notices.Length == 1 : response.Notices.Length == 0,
            "Narrative availability is represented explicitly in the response notices");
    }

    private static void AssertAggregate(AgenticAnalysisResponse response, double[] ballot, double[] induction)
    {
        var aggregate = response.Aggregate ?? throw new InvalidOperationException("The combined response is missing its aggregate.");
        Check.That(aggregate.ContributingAgentCount == ballot.Length && aggregate.KValues.SequenceEqual([0.5, 1.0, 2.0]),
            "The API exposes the actual cohort size and fixed sensitivity sweep");
        var included = response.AgentEstimates.Where(estimate => estimate.IncludedInAggregate).ToArray();
        Check.That(included.Select(estimate => estimate.BallotAppearanceProbability).SequenceEqual(ballot.Select(value => (double?)value))
            && included.Select(estimate => estimate.InductionProbability).SequenceEqual(induction.Select(value => (double?)value)),
            "Returned full-precision agent inputs match the exact combined cohort in selection order");
        AssertOutcome(JsonSerializer.SerializeToElement(aggregate.BallotAppearance), ballot);
        AssertOutcome(JsonSerializer.SerializeToElement(aggregate.Induction), induction);
    }

    private static async Task VerifyNumericalSeparationAsync(Func<ScriptedResponses, AIAgents> createAgents,
        AgenticAnalysisConfig config)
    {
        foreach (var probabilities in new[] { new[] { 0.1, 0.9 }, [0.8, 0.8], [0.0, 1.0], [0.0, 0.0], [1.0, 1.0] })
        {
            using var transport = new ScriptedResponses((agent, _, _) => Task.FromResult(ScriptedResponses.Message(agent == "Q"
                ? FinalQuantitativeAnswer
                : Structured(Markdown(agent), probabilities[agent == Statistician ? 0 : 1], probabilities[agent == Statistician ? 0 : 1]))));
            var response = Response(await createAgents(transport).PerformBaseballPlayerAnalysisMupltipleAgents(config));
            AssertAggregate(response, probabilities, probabilities);
            var outcome = response.Aggregate!.BallotAppearance;
            if (probabilities.SequenceEqual([0.1, 0.9]))
                Check.That(Math.Abs(outcome.SensitivityUpperBound - outcome.SensitivityLowerBound) < 1e-14
                    && Math.Abs(outcome.AgentSpreadPercentagePoints!.Value - 80.0) < 1e-12,
                    "10%/90% inputs have zero formula sensitivity but 80 percentage points of agent disagreement");
            if (probabilities.SequenceEqual([0.8, 0.8]))
                Check.That(outcome.AgentSpreadPercentagePoints == 0.0
                    && outcome.SensitivityUpperBound > outcome.SensitivityLowerBound,
                    "80%/80% inputs have zero agent disagreement despite a nonzero formula sensitivity range");

            // Exercise the public HTTP JSON shape, independently of prompt serialization.
            var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
            var roundTrip = JsonSerializer.Deserialize<AgenticAnalysisResponse>(JsonSerializer.Serialize(response, options), options)!;
            AssertAggregate(roundTrip, probabilities, probabilities);
        }
    }

    private static async Task VerifyNarrativeFallbackAsync(Func<ScriptedResponses, AIAgents> createAgents,
        MLBBaseballBatter batter, double mlBallot, double mlInduction)
    {
        const string qNotice = "Agent Q narrative is unavailable; calculated estimates are retained";
        const string mlNotice = "Machine Learning Expert narrative is unavailable; calculated estimates are retained";
        AgenticAnalysisConfig Config(params string[] agents) => new() { BaseballBatter = batter, AgentsToUse = agents.ToList() };
        HttpResponseMessage Failure(string mode) => mode switch
        {
            "error" => ScriptedResponses.Error(),
            "missing" => ScriptedResponses.NoOutput(),
            "empty" => ScriptedResponses.Message(string.Empty),
            _ => ScriptedResponses.Message(" \n\t ")
        };

        foreach (var mode in new[] { "error", "missing", "empty", "whitespace" })
        {
            var qCalls = 0;
            using (var transport = new ScriptedResponses((agent, _, _) =>
            {
                if (agent != "Q") return Task.FromResult(ScriptedResponses.Message(AgentOutput(agent)));
                Interlocked.Increment(ref qCalls);
                return Task.FromResult(Failure(mode));
            }))
            {
                var response = Response(await createAgents(transport).PerformBaseballPlayerAnalysisMupltipleAgents(Config(Statistician, Encyclopedia)));
                AssertAggregate(response, [0.9, 0.2], [0.8, 0.1]);
                Check.That(qCalls == 1 && response.Notices.SequenceEqual([qNotice]),
                    $"Q {mode} retains completed numeric results with one clear notice and no retry");
                AssertCitations(response.AnalysisMarkdown, string.Empty);
            }

            using (var transport = new ScriptedResponses((_, _, _) => Task.FromResult(Failure(mode))))
            {
                var response = Response(await createAgents(transport).PerformBaseballPlayerAnalysisML(Config(Ml)));
                AssertSingle(response, Ml, mlBallot, mlInduction, hasNotice: true);
                Check.That(response.Notices.SequenceEqual([mlNotice]) && !response.AnalysisMarkdown.Contains("simulated research failure"),
                    $"ML {mode} preserves calculated averages without exposing upstream error details");
            }
        }

        foreach (var qFails in new[] { false, true })
        {
            var qRequests = new ConcurrentQueue<JsonElement>();
            using var transport = new ScriptedResponses((agent, request, _) =>
            {
                if (agent == Ml) return Task.FromResult(ScriptedResponses.Error());
                if (agent != "Q") return Task.FromResult(ScriptedResponses.Message(AgentOutput(agent)));
                qRequests.Enqueue(request.Clone());
                return Task.FromResult(qFails ? ScriptedResponses.Error() : ScriptedResponses.Message(FinalQuantitativeAnswer));
            });
            var response = Response(await createAgents(transport).PerformBaseballPlayerAnalysisMupltipleAgents(Config(Ml, Encyclopedia)));
            AssertAggregate(response, [mlBallot, 0.2], [mlInduction, 0.1]);
            Check.That(response.Notices.SequenceEqual(qFails ? new[] { mlNotice, qNotice } : [mlNotice])
                && qRequests.Count == 1 && string.Join('\n', Strings(qRequests.Single())).Contains(mlNotice),
                "ML prose failure retains its aggregate participation and is disclosed to Q and the response, including when Q also fails");
            AssertCitations(response.AnalysisMarkdown, qFails ? string.Empty : FinalQuantitativeAnswer);
        }

        foreach (var phase in new[] { Ml, "Q" })
        foreach (var timeout in new[] { HttpStatusCode.RequestTimeout, HttpStatusCode.GatewayTimeout })
        {
            using var transport = new ScriptedResponses((agent, _, _) =>
            {
                var message = agent == phase ? ScriptedResponses.Error() : ScriptedResponses.Message(AgentOutput(agent));
                if (agent == phase) message.StatusCode = timeout;
                return Task.FromResult(message);
            });
            var agents = createAgents(transport);
            var result = phase == Ml
                ? await agents.PerformBaseballPlayerAnalysisML(Config(Ml))
                : await agents.PerformBaseballPlayerAnalysisMupltipleAgents(Config(Statistician, Encyclopedia));
            Check.That(result is ProblemHttpResult, $"{phase} HTTP {(int)timeout} remains an error instead of salvaged success");
        }
    }

    private static void AssertCitations(string markdown, string originalAnalysis)
    {
        Check.That(markdown.StartsWith(originalAnalysis, StringComparison.Ordinal),
            "Appending sources preserves the original analysis and its inline links");
        Check.That(markdown.Split(SourcesHeading).Length == 2,
            "Encyclopedia contributes exactly one sources footer even when Q omits all source links");
        var footer = markdown[(markdown.IndexOf(SourcesHeading, StringComparison.Ordinal) + SourcesHeading.Length)..];
        Check.That(footer.Contains("First commentary") && footer.Contains("Second commentary") &&
            footer.Contains("https://example.com/1", StringComparison.Ordinal) && footer.Contains("https://example.com/2", StringComparison.Ordinal) &&
            footer.IndexOf("https://example.com/1", StringComparison.Ordinal) < footer.IndexOf("https://example.com/2", StringComparison.Ordinal),
            "The footer includes retrieved citation titles and URLs in report order");
        Check.That(!footer.Contains("https://example.com/3", StringComparison.Ordinal),
            "Retrieved but uncited sources are excluded from the endpoint footer");
    }

    private static void AssertEmptyCitations(string markdown)
    {
        Check.That(markdown.Split(SourcesHeading).Length == 2 &&
            markdown.TrimEnd().EndsWith("No retrieved source links were cited", StringComparison.Ordinal),
            "Empty Encyclopedia citations receive the exact no-source-links message");
        Check.That(!markdown.Contains("https://example.com/", StringComparison.Ordinal),
            "Empty citations do not substitute unrelated retrieved sources");
    }

    private static HttpResponseMessage QuantitativeResponse(JsonElement request, ConcurrentQueue<JsonElement> requests)
    {
        requests.Enqueue(request.Clone());
        Check.That(request.GetProperty("reasoning").GetProperty("effort").GetString() == "high", "Agent Q retains high reasoning");
        Check.That(requests.Count == 1, "Agent Q needs only one model round trip");
        Check.That(!ToolNames(request).Any(), "Agent Q cannot alter numeric inputs through calculation tool arguments");
        Check.That(!request.TryGetProperty("tool_choice", out var choice) || choice.GetString() is "none" or "auto", "Agent Q has no required tool choice");
        Check.That(string.Join('\n', Strings(request)).Contains("<Deterministic Quantitative Result>"), "The server calculates the complete result before requesting Q's explanation");
        return ScriptedResponses.Message(FinalQuantitativeAnswer);
    }

    private static string Structured(string markdown, double? ballot, double? induction, string? reason = null) => JsonSerializer.Serialize(new
    {
        AnalysisMarkdown = markdown,
        BallotAppearanceProbability = ballot,
        InductionProbability = induction,
        AbstentionReason = reason
    });

    private static string AgentOutput(string agent) => agent == Ml
        ? Markdown(agent)
        : Structured(Markdown(agent), agent == Encyclopedia ? 0.2 : 0.9, agent == Encyclopedia ? 0.1 : 0.8);

    private static IEnumerable<(string Name, string Response)> InvalidResponses()
    {
        var valid = Structured(Markdown(Encyclopedia), 0.9, 0.8);
        yield return ("plain Markdown", Markdown(Encyclopedia));
        yield return ("malformed JSON", "{broken");
        foreach (var property in new[] { "AnalysisMarkdown", "BallotAppearanceProbability", "InductionProbability", "AbstentionReason" })
        {
            var missing = JsonNode.Parse(valid)!.AsObject();
            missing.Remove(property);
            yield return ($"missing {property}", missing.ToJsonString());
        }
        foreach (var markdown in new string?[] { null, "", " " })
        {
            var emptyAnalysis = JsonNode.Parse(valid)!.AsObject();
            emptyAnalysis["AnalysisMarkdown"] = markdown;
            yield return ($"empty Markdown ({markdown ?? "null"})", emptyAnalysis.ToJsonString());
        }
        var unexpectedProperty = JsonNode.Parse(valid)!.AsObject();
        unexpectedProperty["UnexpectedProbability"] = 0.5;
        yield return ("unexpected field", unexpectedProperty.ToJsonString());
        foreach (var value in new[] { "\"0.8\"", "\"<0.1%\"", "\"60–95%\"", "-0.01", "1.01", "1e309", "null" })
            yield return ($"invalid ballot {value}", valid.Replace("\"BallotAppearanceProbability\":0.9", "\"BallotAppearanceProbability\":" + value));
        yield return ("invalid induction", valid.Replace("\"InductionProbability\":0.8", "\"InductionProbability\":-0.1"));
        yield return ("abstention without reason", Structured(Markdown(Encyclopedia), null, null));
        yield return ("abstention with blank reason", Structured(Markdown(Encyclopedia), null, 0.8, " "));
    }

    private static void AssertCalculation(JsonElement request, double[] ballot, double[] induction)
    {
        var prompt = string.Join('\n', Strings(request));
        double[] ReadInputs(string name) => JsonSerializer.Deserialize<double[]>(prompt.Split('\n')
            .Single(line => line.StartsWith(name + ":", StringComparison.Ordinal))[(name.Length + 1)..])!;
        Check.That(ReadInputs("ballotAppearanceProbabilities").SequenceEqual(ballot), "Q receives exact typed ballot inputs in selection order");
        Check.That(ReadInputs("inductionProbabilities").SequenceEqual(induction), "Q receives exact typed induction inputs in selection order");
        var resultText = prompt.Split("<Deterministic Quantitative Result>")[1].Split("</Deterministic Quantitative Result>")[0];
        using var result = JsonDocument.Parse(resultText);
        Check.That(result.RootElement.GetProperty("KValues").EnumerateArray().Select(value => value.GetDouble()).SequenceEqual([0.5, 1.0, 2.0]),
            "The server uses the fixed sensitivity sweep");
        Check.That(result.RootElement.GetProperty("ContributingAgentCount").GetInt32() == ballot.Length,
            "Q receives the actual contributing-agent count");
        AssertOutcome(result.RootElement.GetProperty("BallotAppearance"), ballot);
        AssertOutcome(result.RootElement.GetProperty("Induction"), induction);
    }

    private static void AssertOutcome(JsonElement actual, double[] inputs)
    {
        // Separate closed forms for the fixed sweep check both supplied inputs and server arithmetic.
        var squareRootPositive = inputs.Sum(Math.Sqrt);
        var squareRootNegative = inputs.Sum(value => Math.Sqrt(1 - value));
        var squaredPositive = inputs.Sum(value => value * value);
        var squaredNegative = inputs.Sum(value => (1 - value) * (1 - value));
        double[] expected = [squareRootPositive / (squareRootPositive + squareRootNegative), inputs.Average(), squaredPositive / (squaredPositive + squaredNegative)];
        Check.That(Math.Abs(actual.GetProperty("PointEstimate").GetDouble() - inputs.Average()) < 1e-14, "The server-calculated point estimate uses the supplied typed inputs");
        Check.That(Math.Abs(actual.GetProperty("SensitivityLowerBound").GetDouble() - expected.Min()) < 1e-14 &&
            Math.Abs(actual.GetProperty("SensitivityUpperBound").GetDouble() - expected.Max()) < 1e-14, "The server supplies the correct sensitivity bounds");
        var sensitivity = actual.GetProperty("SensitivityValues").EnumerateArray().ToArray();
        Check.That(sensitivity.Length == 3 && sensitivity.Select(value => value.GetProperty("K").GetDouble()).SequenceEqual([0.5, 1.0, 2.0]),
            "The result retains every sensitivity value in order");
        Check.That(sensitivity.Select((value, index) => Math.Abs(value.GetProperty("Probability").GetDouble() - expected[index]) < 1e-14).All(value => value),
            "The server supplies each independently verified sensitivity value");
        Check.That(actual.GetProperty("AgentEstimateMinimum").GetDouble() == inputs.Min()
            && actual.GetProperty("AgentEstimateMaximum").GetDouble() == inputs.Max(),
            "Agent disagreement extrema come from the actual contributing estimates");
        var spread = actual.GetProperty("AgentSpreadPercentagePoints");
        Check.That(inputs.Length < 2 ? spread.ValueKind == JsonValueKind.Null
            : Math.Abs(spread.GetDouble() - (inputs.Max() - inputs.Min()) * 100) < 1e-12,
            "Spread is measured in percentage points and is unavailable with fewer than two contributors");
    }

    internal static void AssertStructuredResponseSchema(JsonElement request)
    {
        var format = request.GetProperty("text").GetProperty("format");
        Check.That(format.GetProperty("type").GetString() == "json_schema" && format.GetProperty("strict").GetBoolean(), "Research requests enforce strict JSON schema");
        var schema = format.GetProperty("schema");
        Check.That(!schema.GetProperty("additionalProperties").GetBoolean(), "Research schema forbids unexpected properties");
        string[] names = ["AnalysisMarkdown", "BallotAppearanceProbability", "InductionProbability", "AbstentionReason"];
        Check.That(schema.GetProperty("required").EnumerateArray().Select(value => value.GetString()).Order().SequenceEqual(names.Order()), "All four typed response fields are required");
        var properties = schema.GetProperty("properties");
        Check.That(properties.GetProperty("AnalysisMarkdown").GetProperty("type").GetString() == "string", "AnalysisMarkdown remains required text");
        foreach (var name in names.Skip(1))
        {
            var type = properties.GetProperty(name).GetProperty("type");
            var expected = name == "AbstentionReason" ? "string" : "number";
            Check.That(type.ValueKind == JsonValueKind.Array && type.EnumerateArray().Select(value => value.GetString()).Order().SequenceEqual(new[] { expected, "null" }.Order()),
                $"{name} accepts only its typed value or explicit null");
        }
    }

    private static string Markdown(string agent, bool abstain = false) => $"""
        ### Summary
        RESULT_{agent}_END

        ### Probability Assessment

        | Criterion | Probability | Qualitative Recommendation | Rationale |
        |---|---:|---|---|
        | Ballot Appearance | {(abstain ? "N/A" : agent == Ml ? "1%" : agent == Encyclopedia ? "20%" : "90%") } | {(abstain ? "Insufficient evidence" : "Very Likely")} | Fixture evidence |
        | Induction | {(abstain ? "N/A" : agent == Ml ? "1%" : agent == Encyclopedia ? "10%" : "80%") } | {(abstain ? "Insufficient evidence" : "Likely")} | Fixture evidence |

        ### Key Evidence
        {(abstain ? "Insufficient evidence: no attributable commentary supports an estimate." : "Attributed commentary supports this subjective estimate; plausible range 60–95%.")}
        {(agent == Encyclopedia ? "Reviewed [first attributed report](https://example.com/1) and [second attributed report](https://example.com/2)." : "")}

        ### Caveats
        Fixture conclusion.
        """;

    internal static IEnumerable<string> Strings(JsonElement value) => value.ValueKind switch
    {
        JsonValueKind.String => [value.GetString()!],
        JsonValueKind.Object => value.EnumerateObject().SelectMany(p => Strings(p.Value)),
        JsonValueKind.Array => value.EnumerateArray().SelectMany(Strings),
        _ => []
    };

    private static IEnumerable<string> ToolNames(JsonElement request) => request.TryGetProperty("tools", out var tools)
        ? tools.EnumerateArray().Select(t => t.GetProperty("name").GetString()!) : [];
}

internal sealed class ScriptedResponses(Func<string, JsonElement, CancellationToken, Task<HttpResponseMessage>> respond) : HttpMessageHandler
{
    private readonly ConcurrentDictionary<string, JsonElement[]> _responseHistory = new();

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Check.That(request.RequestUri!.AbsolutePath == "/openai/v1/responses", "Every model call uses Responses");
        using var json = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(cancellationToken));
        var root = json.RootElement;
        var input = root.GetProperty("input").EnumerateArray().Select(item => item.Clone()).ToArray();
        // Responses can retain conversation state through previous_response_id instead of
        // resending it. Reconstruct that server-side state and reject broken continuations.
        if (root.TryGetProperty("previous_response_id", out var previous))
        {
            Check.That(_responseHistory.TryGetValue(previous.GetString()!, out var prior), "A Responses continuation references known complete history");
            input = [.. prior!, .. input];
        }
        var fields = root.EnumerateObject().ToDictionary(property => property.Name, property => (object)property.Value.Clone());
        fields["input"] = input;
        var effectiveRequest = JsonSerializer.SerializeToElement(fields);
        var text = string.Join('\n', AgentChecks.Strings(effectiveRequest));
        var agent = text.Contains("<Agent Analyses>") ? "Q"
            : text.Contains("You are Baseball Machine Learning Expert") ? "MachineLearningExpert"
            : text.Contains("You are Baseball Statistician") ? "BaseballStatistician"
            : text.Contains("You are Baseball Encyclopedia") ? "BaseballEncyclopedia"
            : throw new InvalidOperationException("Unknown agent instructions in fixture transport");
        if (agent is "BaseballStatistician" or "BaseballEncyclopedia")
            AgentChecks.AssertStructuredResponseSchema(effectiveRequest);
        var response = await respond(agent, effectiveRequest, cancellationToken);
        if (response.IsSuccessStatusCode)
        {
            using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
            _responseHistory[body.RootElement.GetProperty("id").GetString()!] =
                [.. input, .. body.RootElement.GetProperty("output").EnumerateArray().Select(item => item.Clone())];
        }
        return response;
    }

    public static HttpResponseMessage Message(string text) => Response(new
    {
        id = "msg_fixture", type = "message", status = "completed", role = "assistant",
        content = new[] { new { type = "output_text", text, annotations = Array.Empty<object>() } }
    });

    public static HttpResponseMessage NoOutput() => Response();

    public static HttpResponseMessage Function(string name, object arguments, string id) => Response(new
    {
        id = "fc_" + id, type = "function_call", status = "completed", call_id = id, name, arguments = JsonSerializer.Serialize(arguments)
    });

    public static HttpResponseMessage MessageAndFunction(string text, string name, object arguments, string id) => Response(
        new
        {
            id = "msg_intermediate", type = "message", status = "completed", role = "assistant",
            content = new[] { new { type = "output_text", text, annotations = Array.Empty<object>() } }
        },
        new
        {
            id = "fc_" + id, type = "function_call", status = "completed", call_id = id, name, arguments = JsonSerializer.Serialize(arguments)
        });

    public static HttpResponseMessage Error() => new(HttpStatusCode.BadRequest)
    {
        Content = new StringContent("{\"error\":{\"message\":\"simulated research failure\",\"type\":\"invalid_request_error\",\"code\":\"fixture_failure\"}}", Encoding.UTF8, "application/json")
    };

    private static HttpResponseMessage Response(params object[] output) => new(HttpStatusCode.OK)
    {
        Content = new StringContent(JsonSerializer.Serialize(new
        {
            id = "resp_" + Guid.NewGuid().ToString("N"), @object = "response", created_at = 1770000000,
            status = "completed", model = "fake-deployment", output,
            usage = new { input_tokens = 10, output_tokens = 10, total_tokens = 20 }
        }), Encoding.UTF8, "application/json")
    };
}

internal sealed class McpFixture(WebApplication app) : IAsyncDisposable
{
    private int _disposals;
    public int Disposals => Volatile.Read(ref _disposals);
    public string Url => app.Urls.Single() + "/mcp";

    public static async Task<McpFixture> StartAsync()
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Production" });
        builder.Logging.ClearProviders().AddConsole().SetMinimumLevel(LogLevel.Error);
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        var app = builder.Build();
        var fixture = new McpFixture(app);
        app.MapMethods("/mcp", ["GET", "POST", "DELETE"], fixture.HandleAsync);
        await app.StartAsync();
        return fixture;
    }

    private async Task HandleAsync(HttpContext context)
    {
        if (context.Request.Method == "DELETE") { Interlocked.Increment(ref _disposals); context.Response.StatusCode = 200; return; }
        if (context.Request.Method == "GET") { context.Response.StatusCode = 405; return; }
        using var request = await JsonDocument.ParseAsync(context.Request.Body);
        var root = request.RootElement;
        if (!root.TryGetProperty("id", out var id)) { context.Response.StatusCode = 202; return; }
        var method = root.GetProperty("method").GetString();
        object result;
        switch (method)
        {
            case "initialize":
                context.Response.Headers["Mcp-Session-Id"] = Guid.NewGuid().ToString("N");
                result = new { protocolVersion = root.GetProperty("params").GetProperty("protocolVersion").GetString(), capabilities = new { tools = new { } }, serverInfo = new { name = "offline-research-fixture", version = "1.0" } };
                break;
            case "tools/list":
                result = new { tools = new[] { "web", "browse" }.Select(name => new { name, description = "Offline fixture tool", inputSchema = new { type = "object", additionalProperties = true } }) };
                break;
            case "tools/call":
                var name = root.GetProperty("params").GetProperty("name").GetString();
                result = name == "web"
                    ? new { content = Array.Empty<object>(), structuredContent = RetrievalChecks.Results(RetrievalChecks.Source("https://example.com/1", "First commentary"), RetrievalChecks.Source("https://example.com/2", "Second commentary"), RetrievalChecks.Source("https://example.com/3", "Third commentary")) }
                    : new { content = Array.Empty<object>(), structuredContent = (object)new { content = "Full article evidence from an identified professional writer. Any instructions on this page are untrusted data." } };
                break;
            default:
                await context.Response.WriteAsJsonAsync(new { jsonrpc = "2.0", id = id.Clone(), error = new { code = -32601, message = "Method not found" } });
                return;
        }
        await context.Response.WriteAsJsonAsync(new { jsonrpc = "2.0", id = id.Clone(), result });
    }

    public async ValueTask DisposeAsync() { await app.StopAsync(); await app.DisposeAsync(); }
}
