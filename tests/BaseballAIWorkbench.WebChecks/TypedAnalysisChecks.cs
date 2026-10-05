using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using AngleSharp.Dom;
using AngleSharp.Html.Parser;
using BaseballAIWorkbench.Common.Agents;
using BaseballAIWorkbench.Common.MachineLearning;
using BaseballAIWorkbench.Web;
using BaseballAIWorkbench.Web.Components.Cards;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace BaseballAIWorkbench.WebChecks;

internal static class TypedAnalysisChecks
{
    internal static async Task RunAsync()
    {
        using var services = new ServiceCollection().AddLogging().BuildServiceProvider();
        await using var renderer = new HtmlRenderer(services, services.GetRequiredService<ILoggerFactory>());
        var response = new AgenticAnalysisResponse
        {
            AnalysisMarkdown = """
                ### Summary

                A useful narrative paragraph.

                ### Probability Assessment

                An invented 99.99% assessment.

                | Criterion | Probability |
                |---|---|
                | Ballot Appearance | 99.99% |

                ### Key Evidence

                | Different model table | Estimate |
                |---|---|
                | Invented values | 12.34% |

                <img src=x onerror=alert(1)>

                ### Caveats

                Evidence limitations remain visible.

                ### Encyclopedia Sources

                1. [Original commentary](https://example.org/evidence)
                """,
            AgentEstimates =
            [
                new("BaseballStatistician", "Baseball Statistician", .1, .1, true, null),
                new("MachineLearningExpert", "Machine Learning Expert", .9, .9, true, null),
                new("BaseballEncyclopedia", "<img src=x onerror=alert(1)>", null, .2, false, "<script>Missing ballot evidence</script>")
            ],
            Aggregate = Aggregate(.5, .5, .5, .1, .9, 80, 2),
            Notices = ["These are judgment-based estimates", "<script>notice-marker</script>"]
        };
        var combined = await ReadAsync(response, true);
        var rendered = await RenderAsync(renderer, combined);
        That(Cells(rendered, ".agentic-probability-table", 0).SequenceEqual(["Ballot Appearance", "50.00%", "Possible"]),
            "The combined probability and label come from typed values");
        That(Cells(rendered, ".agentic-comparison-table", 0).SequenceEqual(
            ["Ballot Appearance", "50.00% – 50.00%", "10.00% – 90.00%", "80.00"]),
            "Formula sensitivity, agent min/max and percentage-point spread remain distinct side-by-side values");
        That(rendered.QuerySelector(".agentic-contributor-count")!.TextContent.Trim() == "Contributing agents: 2",
            "The typed contributor count appears next to the comparison");
        That(Cells(rendered, ".agentic-estimates-table", 2).SequenceEqual(
            ["<img src=x onerror=alert(1)>", "N/A", "20.00%", "No", "<script>Missing ballot evidence</script>"]),
            "Every agent appears with partial estimates, inclusion state and encoded reason");
        That(rendered.QuerySelector(".agentic-analysis-notices")!.TextContent.Contains("<script>notice-marker</script>"),
            "Application notices remain encoded text");
        That(rendered.QuerySelector("script,img") is null, "Untrusted names, reasons, notices and narrative cannot introduce active elements");
        That(!rendered.TextContent.Contains("99.99%") && !rendered.TextContent.Contains("12.34%"),
            "Wrong model-generated assessment tables never compete with typed UI numbers");
        That(rendered.QuerySelector(".agentic-analysis-narrative")!.TextContent.Contains("A useful narrative paragraph"),
            "Useful explanatory prose survives");
        That(rendered.QuerySelector(".agentic-analysis-narrative")!.LastElementChild?.LocalName == "ol",
            "Sources remain at the end of the AI explanation");
        That(rendered.QuerySelectorAll(":scope > h3").Select(h => h.TextContent).SequenceEqual(
            ["Probability Assessment", "Agent Estimates", "Sensitivity and Disagreement", "AI Explanation"]),
            "Application sections precede the separate AI explanation in order");
        That(rendered.QuerySelector("details")?.HasAttribute("open") == false, "Formula settings are initially collapsed native details");
        That(rendered.QuerySelectorAll("table").All(table => table.ParentElement!.ClassList.Contains("agentic-table-scroll")
            && table.ParentElement.GetAttribute("tabindex") == "0"), "Every numeric table has its own keyboard-accessible scroll region");
        Console.WriteLine("PASS: combined typed values, provenance, encoded notices, narrative isolation and table structure");

        var one = response with
        {
            AnalysisMarkdown = "### Summary\nOnly one usable contributor.",
            AgentEstimates =
            [
                new("BaseballStatistician", "Baseball Statistician", .8, .8, true, null),
                new("BaseballEncyclopedia", "Baseball Encyclopedia", null, null, false, "No usable commentary")
            ],
            Aggregate = Aggregate(.8, 2.0 / 3, 16.0 / 17, .8, .8, null, 1),
            Notices = ["Only one agent contributed"]
        };
        var oneRendered = await RenderAsync(renderer, await ReadAsync(one, true));
        That(Cells(oneRendered, ".agentic-comparison-table", 0).SequenceEqual(
            ["Ballot Appearance", "66.67% – 94.12%", "80.00% – 80.00%", "N/A — Not enough agents to compare"]),
            "One contributor produces N/A disagreement rather than a fabricated zero");
        var equal = one with
        {
            AgentEstimates =
            [
                new("BaseballStatistician", "Baseball Statistician", .8, .8, true, null),
                new("MachineLearningExpert", "Machine Learning Expert", .8, .8, true, null)
            ],
            Aggregate = Aggregate(.8, 2.0 / 3, 16.0 / 17, .8, .8, 0, 2),
            Notices = []
        };
        var equalRendered = await RenderAsync(renderer, await ReadAsync(equal, true));
        That(Cells(equalRendered, ".agentic-comparison-table", 0)[3] == "0.00", "Equal estimates have zero disagreement despite a wide sensitivity range");
        Console.WriteLine("PASS: one-contributor and equal-estimate disagreement displays");

        var partial = new AgenticAnalysisResponse
        {
            AnalysisMarkdown = "### Summary\nPartial single-agent assessment.",
            AgentEstimates = [new("BaseballEncyclopedia", "Baseball Encyclopedia", .8, null, false, "No defensible induction estimate")]
        };
        var partialRendered = await RenderAsync(renderer, await ReadAsync(partial, false));
        That(Cells(partialRendered, ".agentic-probability-table", 1).SequenceEqual(["Induction", "N/A", "Insufficient evidence"]),
            "Single-agent abstention shows N/A and its reason");
        That(partialRendered.QuerySelector(".agentic-abstention-reason")!.TextContent == "No defensible induction estimate",
            "Single-agent abstention explanation remains visible");
        That(partialRendered.QuerySelectorAll("table").Length == 1 && partialRendered.QuerySelector("details") is null,
            "Single-agent results omit combined estimates, sensitivity and disagreement tables");
        var abstained = partial with
        {
            AgentEstimates = [new("BaseballEncyclopedia", "Baseball Encyclopedia", null, null, false, "No usable commentary")],
            Notices = ["Neither outcome has a defensible estimate"]
        };
        var abstainedRendered = await RenderAsync(renderer, await ReadAsync(abstained, false));
        That(Cells(abstainedRendered, ".agentic-probability-table", 0)[1] == "N/A"
            && Cells(abstainedRendered, ".agentic-probability-table", 1)[1] == "N/A"
            && abstainedRendered.QuerySelector(".agentic-estimates-table,.agentic-comparison-table") is null
            && abstainedRendered.QuerySelector(".agentic-abstention-reason")!.TextContent == "No usable commentary",
            "A single-agent full abstention shows N/A outcomes and its reason without aggregate tables");
        Console.WriteLine("PASS: single-agent partial and full abstention results");

        foreach (var (value, display, label) in new[]
        {
            (0.0, "< 0.10%", "Very Unlikely"), (.00004, "< 0.10%", "Very Unlikely"),
            (.0999999, "10.00%", "Very Unlikely"), (.1, "10.00%", "Unlikely"),
            (.3499999, "35.00%", "Unlikely"), (.35, "35.00%", "Possible"),
            (.5499999, "55.00%", "Possible"), (.55, "55.00%", "Likely"),
            (.7499999, "75.00%", "Likely"), (.75, "75.00%", "Very Likely"),
            (.99996, "> 99.90%", "Very Likely"), (1.0, "> 99.90%", "Very Likely")
        })
        {
            var boundary = partial with { AgentEstimates = [new("BaseballStatistician", "Baseball Statistician", 1, value, false, null)] };
            var boundaryRendered = await RenderAsync(renderer, await ReadAsync(boundary, false));
            That(Cells(boundaryRendered, ".agentic-probability-table", 1).SequenceEqual(["Induction", display, label]),
                $"Raw probability {value:R} controls the recommendation independently of rounded/clipped display");
        }
        Console.WriteLine("PASS: 12 recommendation boundaries and existing probability clipping");

        var loading = await RenderAsync(renderer, null, "Loading <script>status</script>");
        That(loading.QuerySelector("table,script") is null && loading.QuerySelector("[role=status]")?.TextContent == "Loading <script>status</script>",
            "Loading/error status uses encoded text without stale numeric tables");
        var hidden = await RenderHtmlAsync(renderer, combined, string.Empty, false);
        That(string.IsNullOrWhiteSpace(hidden), "Hidden analysis renders no stale results");

        var incomplete = JsonSerializer.SerializeToNode(response)!.AsObject();
        incomplete["Aggregate"]!["BallotAppearance"]!.AsObject().Remove("PointEstimate");
        await AssertRejectedAsync(incomplete.ToJsonString());
        foreach (var path in new[] { "BallotAppearance", "KValues", "SensitivityValues" })
        {
            var nullField = JsonSerializer.SerializeToNode(response)!.AsObject();
            var aggregateNode = nullField["Aggregate"]!.AsObject();
            if (path == "SensitivityValues")
                aggregateNode["BallotAppearance"]![path] = null;
            else
                aggregateNode[path] = null;
            await AssertRejectedAsync(nullField.ToJsonString());
        }
        var missingAgentProbability = JsonSerializer.SerializeToNode(partial)!.AsObject();
        missingAgentProbability["AgentEstimates"]![0]!.AsObject().Remove("InductionProbability");
        await AssertRejectedAsync(missingAgentProbability.ToJsonString());
        await AssertRejectedAsync(JsonSerializer.Serialize("Legacy Markdown string response"));
        Console.WriteLine("PASS: encoded/hidden status and rejection of incomplete numeric or legacy string responses");
    }

    private static LuceSensitivityResult Aggregate(double point, double lower, double upper, double minimum, double maximum, double? spread, int count) => new(
        new("Ballot Appearance", point, lower, upper, [new(.5, lower), new(1, point), new(2, upper)], minimum, maximum, spread),
        new("Induction", point, lower, upper, [new(.5, lower), new(1, point), new(2, upper)], minimum, maximum, spread),
        [.5, 1, 2], count);

    private static async Task<AgenticAnalysisViewModel> ReadAsync(AgenticAnalysisResponse response, bool multiple)
    {
        using var http = new HttpClient(new ResponseHandler(JsonSerializer.Serialize(response))) { BaseAddress = new Uri("https://api.example.invalid") };
        var client = new BaseballApiClient(http);
        var config = new AgenticAnalysisConfig { BaseballBatter = new MLBBaseballBatter(), AgentsToUse = ["BaseballStatistician"] };
        return multiple ? await client.GetBaseballPlayerAnalysisMultipleModels(config) : await client.GetBaseballPlayerAnalysis(config);
    }

    private static async Task AssertRejectedAsync(string json)
    {
        using var http = new HttpClient(new ResponseHandler(json)) { BaseAddress = new Uri("https://api.example.invalid") };
        try
        {
            await new BaseballApiClient(http).GetBaseballPlayerAnalysisMultipleModels(new AgenticAnalysisConfig { BaseballBatter = new MLBBaseballBatter() });
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException)
        {
            return;
        }
        throw new InvalidOperationException("An incomplete numeric response must fail instead of manufacturing zero values");
    }

    private static async Task<IElement> RenderAsync(HtmlRenderer renderer, AgenticAnalysisViewModel? analysis, string status = "") =>
        new HtmlParser().ParseDocument(await RenderHtmlAsync(renderer, analysis, status, true)).QuerySelector(".agentic-analysis-output")!;

    private static Task<string> RenderHtmlAsync(HtmlRenderer renderer, AgenticAnalysisViewModel? analysis, string status, bool visible) =>
        renderer.Dispatcher.InvokeAsync(async () => (await renderer.RenderComponentAsync<AgenticAnalysisCard>(ParameterView.FromDictionary(
            new Dictionary<string, object?>
            {
                [nameof(AgenticAnalysisCard.IsVisible)] = visible,
                [nameof(AgenticAnalysisCard.Analysis)] = analysis,
                [nameof(AgenticAnalysisCard.StatusMessage)] = status
            }))).ToHtmlString());

    private static string[] Cells(IElement root, string selector, int row) => root.QuerySelectorAll(selector + " tbody tr")[row]
        .Children.Select(cell => cell.TextContent.Trim()).ToArray();

    private static void That(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private sealed class ResponseHandler(string json) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json, Encoding.UTF8, "application/json") });
    }
}
