using System.ClientModel;
using System.ClientModel.Primitives;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.Metrics;
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
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace BaseballAIWorkbench.ResearchChecks;

internal static class TelemetryChecks
{
    private const string ServiceName = "BaseballAIWorkbench.TelemetryChecks";
    private const string TokenMetric = "gen_ai.client.token.usage";
    private const string CaptureEnvironmentVariable = "OTEL_INSTRUMENTATION_GENAI_CAPTURE_MESSAGE_CONTENT";
    private const string SensitiveMarker = "PRIVATE_TELEMETRY_FIXTURE_RESPONSE";
    private static readonly TimeSpan GuardTimeout = TimeSpan.FromSeconds(20);

    public static async Task RunAsync(bool exportToDashboard = false)
    {
        // Prove the application's explicit opt-out wins over the SDK's environment opt-in.
        var previousCapture = Environment.GetEnvironmentVariable(CaptureEnvironmentVariable);
        Environment.SetEnvironmentVariable(CaptureEnvironmentVariable, "true");
        try
        {
            await RunCoreAsync(exportToDashboard);
        }
        finally
        {
            Environment.SetEnvironmentVariable(CaptureEnvironmentVariable, previousCapture);
        }
    }

    private static async Task RunCoreAsync(bool exportToDashboard)
    {
        var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
        {
            ApplicationName = ServiceName,
            EnvironmentName = "Production"
        });
        builder.Logging.ClearProviders();
        if (exportToDashboard)
        {
            Check.That(!string.IsNullOrWhiteSpace(builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"]),
                "Telemetry export requires an explicitly configured OTEL_EXPORTER_OTLP_ENDPOINT");
            foreach (var key in new[] { "OTEL_EXPORTER_OTLP_ENDPOINT", "OTEL_EXPORTER_OTLP_TRACES_ENDPOINT",
                "OTEL_EXPORTER_OTLP_METRICS_ENDPOINT", "OTEL_EXPORTER_OTLP_LOGS_ENDPOINT" })
            {
                var value = builder.Configuration[key];
                if (string.IsNullOrWhiteSpace(value)) continue;
                Check.That(Uri.TryCreate(value, UriKind.Absolute, out var endpoint)
                    && endpoint.IsLoopback && endpoint.Scheme is "http" or "https",
                    $"{key} must target a loopback HTTP(S) receiver in telemetry export mode");
            }
        }
        else
        {
            // Ambient Aspire/developer settings must never turn the default offline suite into an exporter.
            builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"] = string.Empty;
        }

        builder.AddServiceDefaults();
        var metrics = new CapturedMetrics();
        var traces = new CapturedTraces();
        builder.Services.AddOpenTelemetry()
            .ConfigureResource(resource => resource.AddService(ServiceName))
            .WithMetrics(provider => provider.AddReader(new PeriodicExportingMetricReader(metrics, 60_000)
            {
                TemporalityPreference = MetricReaderTemporalityPreference.Delta
            }))
            .WithTracing(provider => provider.SetSampler(new AlwaysOnSampler())
                .AddProcessor(new SimpleActivityExportProcessor(traces)));
        builder.Services.AddBaseballPredictionModels();

        using var host = builder.Build();
        await host.StartAsync();
        try
        {
            var meterProvider = host.Services.GetRequiredService<MeterProvider>();
            var tracerProvider = host.Services.GetRequiredService<TracerProvider>();
            using var source = new ActivitySource(ServiceName);
            var activeInstruments = new ConcurrentDictionary<Instrument, byte>();
            using var lifetimeListener = new MeterListener
            {
                InstrumentPublished = (instrument, listener) =>
                {
                    if (instrument.Meter.Name != AiTelemetry.ChatSourceName && instrument.Meter.Name != AiTelemetry.AgentSourceName) return;
                    activeInstruments.TryAdd(instrument, 0);
                    listener.EnableMeasurementEvents(instrument);
                },
                MeasurementsCompleted = (instrument, state) => activeInstruments.TryRemove(instrument, out _)
            };
            lifetimeListener.Start();
            var batter = File.ReadLines(Path.Combine(AppContext.BaseDirectory, "Data/MLBBaseballBattersPositionPlayers.csv"))
                .Skip(1).Select(MLBBaseballBatter.FromCsv).Single(b => b.FullPlayerName == "Mike Trout");
            var pool = host.Services.GetRequiredService<PredictionEnginePool<MLBBaseballBatter, MLBHOFPrediction>>();
            await using var mcp = await McpFixture.StartAsync();
            var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["MAIGrounding-MCP:url"] = mcp.Url,
                ["ConnectionStrings:WebIQMcpApiKey"] = "offline-fixture-key"
            }).Build();
            var toolProvider = new WebIqMcpToolProvider(config, NullLoggerFactory.Instance);

            AIAgents Agents(ScriptedResponses transport) => new(pool,
                new OpenAIClient(new ApiKeyCredential("offline-fixture-key"), new OpenAIClientOptions
                {
                    Endpoint = new Uri("http://fixture.invalid/openai/v1/"),
                    Transport = new HttpClientPipelineTransport(new HttpClient(transport)),
                    RetryPolicy = new ClientRetryPolicy(0)
                }), new AzureOpenAIModelOptions("fake-deployment"), toolProvider, NullLoggerFactory.Instance,
                new BaseballDataService());
            AgenticAnalysisConfig Config(params string[] agents) => new() { AgentsToUse = agents.ToList(), BaseballBatter = batter };

            async Task VerifyAsync(string name, int completedCalls, int attemptedCalls, string[] agentNames,
                Func<Task> run, bool expectError = false)
            {
                Check.That(meterProvider.ForceFlush(), "Metrics flush before fixture succeeds");
                var before = metrics.Totals();
                var durationCountBefore = metrics.DurationCounts.Sum();
                ActivityTraceId traceId;
                ActivitySpanId rootSpanId;
                using (var activity = source.StartActivity("telemetry-fixture." + name))
                {
                    Check.That(activity is not null, "ServiceDefaults subscribes to the application's parent ActivitySource");
                    traceId = activity!.TraceId;
                    rootSpanId = activity.SpanId;
                    await run().WaitAsync(GuardTimeout);
                }

                Check.That(tracerProvider.ForceFlush() && meterProvider.ForceFlush(), "Telemetry flush after fixture succeeds");
                var after = metrics.Totals();
                Check.That(after.Input - before.Input == completedCalls * 10 && after.Output - before.Output == completedCalls * 10,
                    $"{name}: exported token sums are exactly {completedCalls * 10} input/{completedCalls * 10} output");
                Check.That(after.InputCount - before.InputCount == completedCalls && after.OutputCount - before.OutputCount == completedCalls,
                    $"{name}: exactly one input/output histogram observation per usage-bearing model call");
                Check.That(metrics.DurationCounts.Sum() - durationCountBefore == attemptedCalls,
                    $"{name}: one duration observation per attempted model call, including calls without usage");
                Check.That(metrics.TokenPoints.All(point => point.Meter == AiTelemetry.ChatSourceName),
                    "ServiceDefaults exports the MEAI token meter, never the aggregate MAF token meter");

                var spans = traces.Spans.Where(span => span.TraceId == traceId).ToArray();
                var agents = spans.Where(span => span.Source.Name == AiTelemetry.AgentSourceName && Tag(span, "gen_ai.operation.name") == "invoke_agent").ToArray();
                var chats = spans.Where(span => span.Source.Name == AiTelemetry.ChatSourceName && Tag(span, "gen_ai.operation.name") == "chat").ToArray();
                Check.That(agents.Select(span => Tag(span, "gen_ai.agent.name")).Order().SequenceEqual(agentNames.Order()),
                    $"{name}: every expected agent has exactly one completed MAF invocation span");
                Check.That(agents.All(span => span.ParentSpanId == rootSpanId), $"{name}: agent invocations share the fixture parent");
                Check.That(chats.Length == attemptedCalls, $"{name}: every attempted model call has one completed MEAI chat span");
                Check.That(chats.All(chat => agents.Any(agent => agent.SpanId == chat.ParentSpanId)),
                    $"{name}: each chat span is a direct child of its agent invocation");
                Check.That(agents.Concat(chats).All(span => span.Duration > TimeSpan.Zero && span.IsStopped),
                    $"{name}: all AI spans finish before the handler completes");
                Check.That(activeInstruments.IsEmpty && agents.Concat(chats).All(span => !span.Source.HasListeners()),
                    $"{name}: invocation-owned meters and activity sources are disposed while SDK collectors remain active");
                if (expectError)
                    Check.That(agents.Concat(chats).All(span => span.Status == ActivityStatusCode.Error),
                        $"{name}: failures and cancellations mark both instrumentation layers as errors");
                AssertNoSensitiveContent(spans.Where(span => span.Source.Name == AiTelemetry.ChatSourceName
                    || span.Source.Name == AiTelemetry.AgentSourceName));
                Console.WriteLine($"PASS telemetry {name}: {completedCalls * 10} input, {completedCalls * 10} output; {agents.Length} agent spans, {chats.Length} chat spans.");
            }

            var reusedClientCalls = 0;
            using var reusedTransport = new ScriptedResponses(async (_, _, _) =>
            {
                var response = ScriptedResponses.Message(SensitiveMarker);
                if (Interlocked.Increment(ref reusedClientCalls) == 1) return response;
                using (response)
                {
                    var body = JsonNode.Parse(await response.Content.ReadAsStringAsync())!.AsObject();
                    body.Remove("usage");
                    return new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json")
                    };
                }
            });
            var reusedAgents = Agents(reusedTransport);
            await VerifyAsync("single", 1, 1, ["MachineLearningExpert"], async () =>
                Check.That(await reusedAgents.PerformBaseballPlayerAnalysisML(Config("MachineLearningExpert")) is Ok<string>,
                    "Single telemetry fixture completes successfully"));

            var researchCalls = 0;
            using (var transport = new ScriptedResponses((_, _, _) => Task.FromResult(ResearchResponse(Interlocked.Increment(ref researchCalls)))))
                await VerifyAsync("research", 3, 3, ["BaseballEncyclopedia"], async () =>
                    Check.That(await Agents(transport).PerformBaseballPlayerAnalysisML(Config("BaseballEncyclopedia")) is Ok<string> && researchCalls == 3,
                        "Research telemetry fixture preserves two tool rounds plus synthesis"));

            string[] selected = ["MachineLearningExpert", "BaseballStatistician", "BaseballEncyclopedia"];
            var started = selected.ToDictionary(name => name, _ => Check.Signal());
            var parallelResearchCalls = 0;
            using (var transport = new ScriptedResponses(async (agent, _, token) =>
            {
                if (agent == "Q") return ScriptedResponses.Message(SensitiveMarker);
                started[agent].TrySetResult();
                await Task.WhenAll(started.Values.Select(signal => signal.Task)).WaitAsync(token);
                return agent == "BaseballEncyclopedia"
                    ? ResearchResponse(Interlocked.Increment(ref parallelResearchCalls))
                    : ScriptedResponses.Message(agent == "BaseballStatistician" ? StructuredAnswer() : SensitiveMarker);
            }))
                await VerifyAsync("parallel", 6, 6, [.. selected, "QuantitativeAnalysis"], async () =>
                    Check.That(await Agents(transport).PerformBaseballPlayerAnalysisMupltipleAgents(Config(selected)) is Ok<string>,
                        "Parallel telemetry fixture completes all three agents and Q"));

            foreach (var selectedAgent in new[] { "MachineLearningExpert", "BaseballStatistician" })
            {
                var prefix = selectedAgent == "BaseballStatistician" ? "structured-" : string.Empty;
                using (var transport = new ScriptedResponses((_, _, _) => Task.FromResult(ScriptedResponses.Error())))
                    await VerifyAsync(prefix + "failure", 0, 1, [selectedAgent], async () =>
                        Check.That(await Agents(transport).PerformBaseballPlayerAnalysisML(Config(selectedAgent)) is ProblemHttpResult,
                            "Failed model call preserves the handler's error contract"), expectError: true);

                using (var cancellation = new CancellationTokenSource())
                {
                    var callStarted = Check.Signal();
                    using var transport = new ScriptedResponses(async (_, _, token) =>
                    {
                        callStarted.TrySetResult();
                        await Task.Delay(Timeout.InfiniteTimeSpan, token);
                        throw new InvalidOperationException("Canceled model call unexpectedly continued");
                    });
                    await VerifyAsync(prefix + "cancellation", 0, 1, [selectedAgent], async () =>
                    {
                        var operation = Agents(transport).PerformBaseballPlayerAnalysisML(Config(selectedAgent), cancellation.Token);
                        try
                        {
                            await callStarted.Task.WaitAsync(GuardTimeout);
                            cancellation.Cancel();
                            try
                            {
                                await operation;
                                throw new InvalidOperationException("Cancellation was not propagated");
                            }
                            catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { }
                        }
                        finally { cancellation.Cancel(); }
                    }, expectError: true);
                }
            }

            await VerifyAsync("missing-usage", 0, 1, ["MachineLearningExpert"], async () =>
                Check.That(await reusedAgents.PerformBaseballPlayerAnalysisML(Config("MachineLearningExpert")) is Ok<string>
                    && reusedClientCalls == 2,
                    "The shared OpenAIClient survives invocation cleanup; its next successful response without usage invents no counts"));

            Check.That(metrics.TokenPoints.All(point => point.Unit == "{token}"
                && point.Tags.GetValueOrDefault("gen_ai.operation.name") == "chat"
                && point.Tags.GetValueOrDefault("gen_ai.request.model") == "fake-deployment"
                && point.Tags.GetValueOrDefault("gen_ai.response.model") == "fake-deployment"
                && point.Tags.GetValueOrDefault("gen_ai.provider.name") == "openai"),
                "Token metrics preserve the SDK's unit, operation, request/response model, and provider tags");
            Console.WriteLine($"PASS telemetry: actual ServiceDefaults SDK collection; sensitive capture disabled despite environment opt-in; {(exportToDashboard ? "loopback OTLP export enabled" : "external exporters disabled")}.");
        }
        finally
        {
            await host.StopAsync();
        }
    }

    private static string StructuredAnswer() => JsonSerializer.Serialize(new
    {
        AnalysisMarkdown = SensitiveMarker,
        BallotAppearanceProbability = 0.8,
        InductionProbability = 0.6,
        AbstentionReason = (string?)null
    });

    private static HttpResponseMessage ResearchResponse(int call) => call <= 2
        ? ScriptedResponses.Function("read_commentary_source", new { sourceId = "S" + call }, "telemetry_read_" + call)
        : ScriptedResponses.Message(StructuredAnswer());

    private static string? Tag(Activity span, string name) => span.GetTagItem(name)?.ToString();

    private static void AssertNoSensitiveContent(IEnumerable<Activity> spans)
    {
        string[] sensitiveTags = ["gen_ai.input.messages", "gen_ai.output.messages", "gen_ai.system_instructions",
            "gen_ai.prompt", "gen_ai.completion", "gen_ai.tool.call.arguments", "gen_ai.tool.call.result"];
        foreach (var span in spans)
        {
            Check.That(span.TagObjects.All(tag => !sensitiveTags.Contains(tag.Key)),
                "AI traces omit prompt, response, instruction, and tool argument/result content");
            var data = string.Join('\n', span.TagObjects.Select(tag => $"{tag.Key}={tag.Value}")) +
                string.Join('\n', span.Events.SelectMany(evt => evt.Tags.Select(tag => $"{tag.Key}={tag.Value}")));
            Check.That(!data.Contains(SensitiveMarker) && !data.Contains("You are Baseball")
                && !data.Contains("Full article evidence"), "No fixture prompt, answer, or retrieved article leaks into AI traces");
            Check.That(!span.Events.Any(evt => evt.Name is "gen_ai.user.message" or "gen_ai.assistant.message" or "gen_ai.system.message"),
                "AI traces omit sensitive message events");
        }
    }

    private sealed class CapturedTraces : BaseExporter<Activity>
    {
        public ConcurrentQueue<Activity> Spans { get; } = new();
        public override ExportResult Export(in Batch<Activity> batch)
        {
            foreach (var activity in batch) Spans.Enqueue(activity);
            return ExportResult.Success;
        }
    }

    private sealed record TokenPoint(string Meter, string? Unit, double Sum, long Count, Dictionary<string, string?> Tags);

    private sealed class CapturedMetrics : BaseExporter<Metric>
    {
        public ConcurrentQueue<TokenPoint> TokenPoints { get; } = new();
        public ConcurrentQueue<long> DurationCounts { get; } = new();
        public override ExportResult Export(in Batch<Metric> batch)
        {
            foreach (var metric in batch)
            {
                if (metric.Name == "gen_ai.client.operation.duration" && metric.MeterName == AiTelemetry.ChatSourceName)
                {
                    foreach (ref readonly var point in metric.GetMetricPoints()) DurationCounts.Enqueue(point.GetHistogramCount());
                }
                if (metric.Name != TokenMetric) continue;
                foreach (ref readonly var point in metric.GetMetricPoints())
                {
                    var tags = new Dictionary<string, string?>();
                    foreach (var tag in point.Tags) tags[tag.Key] = tag.Value?.ToString();
                    TokenPoints.Enqueue(new TokenPoint(metric.MeterName, metric.Unit,
                        point.GetHistogramSum(), point.GetHistogramCount(), tags));
                }
            }
            return ExportResult.Success;
        }

        public (double Input, double Output, long InputCount, long OutputCount) Totals()
        {
            var points = TokenPoints.ToArray();
            var input = points.Where(point => point.Tags.GetValueOrDefault("gen_ai.token.type") == "input").ToArray();
            var output = points.Where(point => point.Tags.GetValueOrDefault("gen_ai.token.type") == "output").ToArray();
            return (input.Sum(point => point.Sum), output.Sum(point => point.Sum),
                input.Sum(point => point.Count), output.Sum(point => point.Count));
        }
    }
}
