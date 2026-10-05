using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
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

internal static class MarkdownRenderingChecks
{
    private static readonly HashSet<string> SafeElements = new(StringComparer.OrdinalIgnoreCase)
    {
        "p", "br", "hr", "h1", "h2", "h3", "h4", "h5", "h6", "strong", "em", "b", "i", "del", "s",
        "ins", "blockquote", "pre", "code", "ul", "ol", "li", "dl", "dt", "dd", "table", "thead", "tbody", "tfoot",
        "tr", "th", "td", "caption", "a", "sub", "sup", "mark"
    };

    private static readonly HashSet<string> SafeAttributes = new(StringComparer.OrdinalIgnoreCase)
    {
        "href", "title", "colspan", "rowspan", "start", "target", "rel"
    };

    internal static async Task RunAsync()
    {
        using var services = new ServiceCollection().AddLogging().BuildServiceProvider();
        await using var renderer = new HtmlRenderer(services, services.GetRequiredService<ILoggerFactory>());
        var parser = new HtmlParser();
        var checks = 0;

        foreach (var fixture in Fixtures())
        {
            foreach (var multipleAgents in new[] { false, true })
            {
                var endpoint = multipleAgents ? "/BaseballPlayerAnalysisMultipleAgents" : "/BaseballPlayerAnalysisML";
                using var handler = new MarkdownResponseHandler(fixture.Markdown, endpoint);
                using var http = new HttpClient(handler) { BaseAddress = new Uri("https://api.example.invalid") };
                var client = new BaseballApiClient(http);
                var config = new AgenticAnalysisConfig
                {
                    BaseballBatter = new MLBBaseballBatter { FullPlayerName = "Rendering Fixture" },
                    AgentsToUse = multipleAgents
                        ? ["BaseballStatistician", "MachineLearningExpert"]
                        : ["BaseballStatistician"]
                };

                try
                {
                    var analysis = multipleAgents
                        ? await client.GetBaseballPlayerAnalysisMultipleModels(config)
                        : await client.GetBaseballPlayerAnalysis(config);
                    That(handler.RequestCount == 1, "Exactly one fake POST serves the requested endpoint");
                    var body = parser.ParseDocument(analysis.NarrativeHtml).Body!;
                    That(analysis.Response.AgentEstimates[0].BallotAppearanceProbability == 0.9,
                        "Typed probabilities are preserved independently of the narrative fixture");
                    AssertSafeElements(body);
                    fixture.Verify(body);

                    // Exercise the real component's MarkupString boundary as well as the client fragment.
                    var rendered = await renderer.Dispatcher.InvokeAsync(async () =>
                    {
                        var component = await renderer.RenderComponentAsync<AgenticAnalysisCard>(
                            ParameterView.FromDictionary(new Dictionary<string, object?>
                            {
                                [nameof(AgenticAnalysisCard.IsVisible)] = true,
                                [nameof(AgenticAnalysisCard.Analysis)] = analysis
                            }));
                        return component.ToHtmlString();
                    });
                    var renderedDocument = parser.ParseDocument(rendered);
                    var output = renderedDocument.QuerySelector(".agentic-analysis-narrative");
                    That(output is not null, "The real analysis output wrapper survives the supplied content");
                    That(renderedDocument.QuerySelectorAll(".agentic-analysis-narrative").Length == 1,
                        "The supplied content cannot duplicate the analysis output wrapper");
                    AssertSafeElements(output!);
                    fixture.Verify(output!);
                    That(renderedDocument.QuerySelectorAll("script,iframe,frame,frameset,object,embed,img,svg,math,form,input,button,style,link,meta,audio,video,source").Length == 0,
                        "No active or embedded-resource element appears elsewhere in the rendered component");
                    checks++;
                }
                catch (Exception ex)
                {
                    throw new InvalidOperationException($"Rendering fixture '{fixture.Name}' failed for {endpoint}: {ex.Message}", ex);
                }
            }

            Console.WriteLine($"PASS: {fixture.Name} (both endpoints and real component)");
        }

        Console.WriteLine($"PASS: {checks} endpoint/fixture combinations with parsed HTML safety and content checks.");
    }

    private static IEnumerable<Fixture> Fixtures()
    {
        yield return new("headings and inline formatting", """
            # Summary

            ## Background

            ### Key Evidence

            #### Details

            ##### Sources

            ###### Caveats

            **strong evidence**, *qualified opinion*, ~~removed~~, ++inserted++, ~subscript~, ^superscript^, ==highlighted==, and `literal code`.
            """, root =>
        {
            That(root.QuerySelectorAll("h3").Length == 3 && root.QuerySelector("h1,h2") is null,
                "Top-level headings retain existing h3 normalization");
            foreach (var tag in new[] { "h4", "h5", "h6", "strong", "em", "ins", "del", "sub", "sup", "mark", "code" })
                That(root.QuerySelector(tag) is not null, $"Benign {tag} formatting survives");
        });

        yield return new("lists, blockquotes, definitions and separators", """
            > Attributed professional commentary

            - First source
            - Second source

            3. Third case
            4. Fourth case

            Ballot
            :   Appearance on a ballot

            ---

            First line\
            Second line
            """, root =>
        {
            foreach (var tag in new[] { "blockquote", "ul", "ol", "li", "dl", "dt", "dd", "hr", "br" })
                That(root.QuerySelector(tag) is not null, $"Benign {tag} structure survives");
            That(root.QuerySelector("ol")!.GetAttribute("start") == "3", "Ordered-list starting number survives");
        });

        yield return new("legacy probability section is excluded from narrative", """
            ### Probability Assessment

            | Criterion | Probability | Rationale |
            |---|---:|---|
            | Ballot Appearance | 92.59% | [Source](https://example.org/report) |
            | Induction | < 0.10% | **Limited evidence** |
            """, root =>
        {
            AssertNoNarrativeTable(root, "92.59%", "< 0.10%");
            That(!root.TextContent.Contains("Probability Assessment"), "The competing model-generated assessment heading is removed");
        });

        yield return new("grid table is excluded from narrative", """
            +---------+-------+
            | Agent   | Value |
            +=========+=======+
            | Expert  | 80%   |
            +---------+-------+
            """, root => AssertNoNarrativeTable(root, "Expert", "80%"));

        yield return new("whole Markdown fence normalization", """
            ```markdown
            # Summary

            | Criterion | Probability |
            |---|---|
            | Induction | 80.00% |
            ```
            """, root =>
        {
            That(root.QuerySelector("h3")?.TextContent == "Summary", "The whole-document Markdown fence is removed");
            AssertNoNarrativeTable(root, "Induction", "80.00%");
        });

        yield return new("embedded table fence normalization", """
            ### Key Evidence

            ```md
            | Agent | Estimate |
            |---|---|
            | Expert | 80% |
            ```

            Additional caveats
            """, root =>
        {
            AssertNoNarrativeTable(root, "Expert", "80%");
            That(root.TextContent.Contains("Additional caveats"), "Text after a repaired fenced table survives");
        });

        yield return new("indented and glued table normalization", """
            ### Probability Assessment

               | Criterion | Probability |
               |---|---|| Ballot Appearance | 90% |

               | Induction | 80% |
            """, root => AssertNoNarrativeTable(root, "Ballot Appearance", "90%", "Induction", "80%"));

        yield return new("legacy section hierarchy preserves following evidence and sources", """
            ## Summary

            A useful opening paragraph.

            ## **Probability Assessment:**

            Incorrect outcome estimate 99.99%.

            ### Model Details

            Subordinate explanation that belongs to the old assessment.

            ## Key Evidence

            Retained evidence paragraph.

            ## Caveats

            Retained caveat.

            ### Encyclopedia Sources

            1. [Source](https://example.org/commentary)
            """, root =>
        {
            That(!root.TextContent.Contains("99.99%") && !root.TextContent.Contains("Subordinate explanation"),
                "The entire legacy assessment section is removed, including subordinate content");
            That(root.TextContent.Contains("Retained evidence paragraph") && root.TextContent.Contains("Retained caveat"),
                "Following peer sections remain readable");
            AssertSourceFooter(root, ("Source", "https://example.org/commentary"));
        });

        yield return new("a deeper source footer survives a legacy top-level assessment", """
            # Probability Assessment

            Incorrect assessment 99.99%.

            ### Encyclopedia Sources

            1. [Original commentary](https://example.org/original)
            """, root =>
        {
            That(!root.TextContent.Contains("99.99%"), "The old assessment is excluded");
            AssertSourceFooter(root, ("Original commentary", "https://example.org/original"));
        });

        yield return new("nested narrative tables are removed without discarding prose", """
            > Attributed context remains.
            >
            > | Criterion | Estimate |
            > |---|---|
            > | Invented model row | 99.99% |

            Following commentary remains.
            """, root =>
        {
            AssertNoNarrativeTable(root, "99.99%", "Invented model row");
            That(root.TextContent.Contains("Attributed context remains") && root.TextContent.Contains("Following commentary remains"),
                "Prose surrounding a nested table survives");
        });

        yield return new("safe citation and navigation links", """
            [HTTPS](https://example.org/source?x=1&y=2 "Article title")
            [HTTP](http://example.org/archive)
            [Relative](/reports/player?q=1#evidence)
            [Child](research/player)
            [Fragment](#evidence)
            [Reference][source-reference]

            [source-reference]: https://example.org/referenced-source

            https://example.org/commentary

            <https://example.org/angle-autolink>
            """, root =>
        {
            var expected = new Dictionary<string, string>
            {
                ["HTTPS"] = "https://example.org/source?x=1&y=2",
                ["HTTP"] = "http://example.org/archive",
                ["Relative"] = "/reports/player?q=1#evidence",
                ["Child"] = "research/player",
                ["Fragment"] = "#evidence",
                ["Reference"] = "https://example.org/referenced-source",
                ["https://example.org/commentary"] = "https://example.org/commentary",
                ["https://example.org/angle-autolink"] = "https://example.org/angle-autolink"
            };
            foreach (var (text, href) in expected)
                That(root.QuerySelectorAll("a").Any(a => a.TextContent == text && a.GetAttribute("href") == href),
                    $"Safe link '{text}' remains clickable");
            That(root.QuerySelector("a")!.GetAttribute("title") == "Article title", "Safe citation title survives");
        });

        yield return new("numbered Encyclopedia sources stay below the report", """
            ### Summary

            A professional commentary assessment with a [source cited in context](https://example.org/context).

            ### Caveats

            This is a judgment based on the retrieved commentary.

            ### Encyclopedia Sources

            1. [A writer's case for induction](<https://example.org/case?season=2025&view=full>)
            2. [The strongest objections](<https://example.org/archive/case-(part-two)>)
            """, root => AssertSourceFooter(root,
                ("A writer's case for induction", "https://example.org/case?season=2025&view=full"),
                ("The strongest objections", "https://example.org/archive/case-(part-two)")));

        yield return new("escaped Encyclopedia source titles remain safe and readable", """
            ### Caveats

            Source titles are untrusted text.

            ### Encyclopedia Sources

            1. [A \[bracketed\] \*Hall\* case &amp; &lt;img src=x onerror=alert(1)&gt;](<https://example.org/safe-title>)
            2. [\[False link\]\(javascript:alert\(1\)\) \# commentary](<https://example.org/escaped-markdown>)
            """, root =>
        {
            AssertSourceFooter(root,
                ("A [bracketed] *Hall* case & <img src=x onerror=alert(1)>", "https://example.org/safe-title"),
                ("[False link](javascript:alert(1)) # commentary", "https://example.org/escaped-markdown"));
            That(root.QuerySelectorAll("a").Length == 2, "Title markup cannot introduce additional links");
        });

        var longTitle = "A detailed commentary title " + new string('A', 240);
        var fallbackUrl = "https://example.org/commentary/" + new string('b', 450) + "?season=2025&view=full";
        yield return new("long source titles and canonical URL fallback survive", $"""
            ### Caveats

            The next references include a long title and a source without a usable title.

            ### Encyclopedia Sources

            1. [{longTitle}](<https://example.org/long-title>)
            2. [{fallbackUrl}](<{fallbackUrl}>)
            """, root => AssertSourceFooter(root,
                (longTitle, "https://example.org/long-title"), (fallbackUrl, fallbackUrl)));

        yield return new("raw executable HTML remains literal text", """
            <script>alert('script-marker')</script>

            <img src="https://attacker.invalid/pixel" onerror="alert('image-marker')">

            <a href="javascript:alert(1)" onclick="alert(2)" target="_blank" class="hostile" id="hostile" style="position:fixed" data-payload="hostile">raw-link</a>

            <style>body { background: url(https://attacker.invalid/style); }</style>
            """, root =>
        {
            foreach (var literal in new[] { "<script>", "<img ", "<a href=", "<style>" })
                That(root.TextContent.Contains(literal), $"Raw {literal} is displayed as text");
            That(!root.QuerySelectorAll("a").Any(a => a.TextContent == "raw-link"), "The raw HTML anchor never becomes an active element");
        });

        yield return new("frames, forms and embedded resources remain inert", """
            </div><iframe srcdoc="<script>alert(1)</script>"></iframe><div>

            <form action="https://attacker.invalid/submit"><input name="secret"><button>Submit</button></form>

            <object data="https://attacker.invalid/object"></object><embed src="https://attacker.invalid/embed">

            <audio src="https://attacker.invalid/audio"></audio><video src="https://attacker.invalid/video"><source src="https://attacker.invalid/source"></video>

            <link rel="stylesheet" href="https://attacker.invalid/css"><meta http-equiv="refresh" content="0;url=https://attacker.invalid">
            """, root =>
        {
            That(root.TextContent.Contains("<iframe") && root.TextContent.Contains("<form"), "Blocked HTML is visible as literal text");
            That(root.QuerySelector("div") is null, "Untrusted closing and opening wrappers cannot alter the DOM structure");
        });

        yield return new("SVG and MathML remain inert", """
            <svg onload="alert(1)"><a xlink:href="javascript:alert(2)">SVG payload</a><foreignObject><iframe src="https://attacker.invalid"></iframe></foreignObject></svg>

            <math><mtext><img src=x onerror=alert(3)></mtext></math>
            """, root => That(root.TextContent.Contains("<svg") && root.TextContent.Contains("<math"),
                "Foreign-namespace payloads remain literal text"));

        yield return new("Markdown images cannot load external resources", """
            Before image

            ![remote tracking image](https://attacker.invalid/pixel.png "tracking")
            ![inline SVG](data:image/svg+xml;base64,PHN2ZyBvbmxvYWQ9YWxlcnQoMSk+)

            After image
            """, root =>
        {
            That(root.QuerySelector("img") is null, "Markdown image elements are removed");
            That(root.TextContent.Contains("Before image") && root.TextContent.Contains("After image"), "Surrounding prose survives removed images");
        });

        yield return new("Markdown extensions cannot add attributes or controls", """
            ### Heading {#hostile .hostile target="_self" rel="opener" onclick="alert(1)" style="color:red"}

            [Citation](https://example.org/source){target="_self" rel="opener" onclick="alert(1)" data-secret="value"}

            - [ ] Task control

            ```html
            <img src=x onerror=alert(1)>
            ```
            """, root =>
        {
            That(root.QuerySelector("input") is null, "Task-list text does not introduce form controls");
            That(root.QuerySelector("code")?.TextContent.Contains("<img src=x onerror=alert(1)>") == true,
                "Code retains the literal payload without language or custom classes");
            That(root.QuerySelector("a")?.GetAttribute("href") == "https://example.org/source", "Attribute-like text does not destroy the safe link");
        });

        foreach (var (name, destination) in new[]
        {
            ("javascript scheme", "javascript:alert%281%29"),
            ("mixed-case javascript scheme", "JaVaScRiPt:alert%281%29"),
            ("HTML-entity javascript scheme", "jav&#x61;script:alert%281%29"),
            ("entity-colon javascript scheme", "javascript&#58;alert%281%29"),
            ("tab-obfuscated javascript scheme", "java&#x09;script:alert%281%29"),
            ("newline-obfuscated javascript scheme", "java&#x0A;script:alert%281%29"),
            ("literal-control javascript scheme", "java\tscript:alert%281%29"),
            ("data scheme", "data:text/html;base64,PHNjcmlwdD5hbGVydCgxKTwvc2NyaXB0Pg=="),
            ("vbscript scheme", "vbscript:msgbox%281%29"),
            ("file scheme", "file:///etc/passwd"),
            ("non-HTTP mail scheme", "mailto:reader@example.org")
        })
        {
            yield return new(name, $"[Blocked destination](<{destination}>)", root =>
            {
                That(root.TextContent.Contains("Blocked destination"), "The blocked link's readable label survives");
                That(root.QuerySelector("a[href]") is null, "A disallowed or obfuscated destination cannot remain clickable");
            });
        }

        yield return new("escaped HTML and code stay literal", """
            &lt;script&gt;escaped-marker&lt;/script&gt;

            `<img src=x onerror=alert(1)>`

            ```html
            <script>fenced-marker</script>
            <a href="javascript:alert(1)">literal-link</a>
            ```
            """, root =>
        {
            That(root.TextContent.Contains("<script>escaped-marker</script>"), "Escaped prose is text after HTML parsing");
            That(root.QuerySelectorAll("code").Length == 2, "Inline and fenced code survive");
            That(root.QuerySelector("pre code")?.TextContent.Contains("<script>fenced-marker</script>") == true,
                "Fenced executable text remains literal code");
        });
    }

    private static void AssertSourceFooter(IElement root, params (string Title, string Url)[] sources)
    {
        var footer = root.Children.LastOrDefault();
        That(footer?.LocalName == "ol", "The numbered source list is the final report block");
        var heading = footer!.PreviousElementSibling;
        That(heading?.LocalName == "h3" && heading.TextContent == "Encyclopedia Sources",
            "The footer has the Encyclopedia Sources heading");
        That(root.QuerySelectorAll("h3").Count(h => h.TextContent == "Encyclopedia Sources") == 1,
            "The source heading appears once");
        var links = footer.QuerySelectorAll("li > a");
        That(footer.Children.Length == sources.Length && links.Length == sources.Length,
            "Each numbered source has exactly one clickable title");
        for (var index = 0; index < sources.Length; index++)
        {
            That(links[index].TextContent == sources[index].Title, "Source titles and URL fallbacks retain their readable text");
            That(links[index].GetAttribute("href") == sources[index].Url, "Canonical source destinations remain clickable and unchanged");
        }
    }

    private static void AssertNoNarrativeTable(IElement root, params string[] cellValues)
    {
        That(root.QuerySelector("table") is null, "The narrative cannot supply a competing table");
        foreach (var value in cellValues)
            That(!root.TextContent.Contains(value), $"Model-generated table value '{value}' is removed");
    }

    private static void AssertSafeElements(IElement root)
    {
        foreach (var element in root.QuerySelectorAll("*"))
        {
            That(SafeElements.Contains(element.LocalName), $"Unexpected rendered element: {element.LocalName}");
            foreach (var attribute in element.Attributes)
            {
                That(SafeAttributes.Contains(attribute.Name), $"Unexpected rendered attribute: {element.LocalName}[{attribute.Name}]");
                if (attribute.Name is "target" or "rel")
                    That(element.LocalName == "a", $"Link navigation attributes cannot appear on {element.LocalName}");
            }

            if (element.GetAttribute("href") is { } href)
            {
                That(element.LocalName == "a", "Only anchors can carry a rendered hyperlink destination");
                That(element.GetAttribute("target") == "_blank", "Every rendered hyperlink opens in a new tab");
                var relationshipTokens = new HashSet<string>(
                    (element.GetAttribute("rel") ?? string.Empty).Split(' ', StringSplitOptions.RemoveEmptyEntries),
                    StringComparer.OrdinalIgnoreCase);
                That(relationshipTokens.SetEquals(["noopener", "noreferrer"]),
                    "Every rendered hyperlink isolates the opener and suppresses referrer information");

                // HTML parsing decodes entities; browsers also ignore ASCII controls in URL schemes.
                var normalized = Regex.Replace(href, "[\\x00-\\x20\\x7f]", string.Empty);
                That(Uri.TryCreate(new Uri("https://app.example.invalid/"), normalized, out var uri)
                    && uri.Scheme is "http" or "https", $"Unsafe rendered link destination: {href}");
            }
        }
    }

    private static void That(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }

    private sealed record Fixture(string Name, string Markdown, Action<IElement> Verify);

    private sealed class MarkdownResponseHandler(string markdown, string expectedEndpoint) : HttpMessageHandler
    {
        internal int RequestCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            That(request.Method == HttpMethod.Post && request.RequestUri?.AbsolutePath == expectedEndpoint,
                "The client uses the expected analysis POST route");
            RequestCount++;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(JsonSerializer.Serialize(new AgenticAnalysisResponse
                {
                    AnalysisMarkdown = markdown,
                    AgentEstimates = [new("BaseballStatistician", "Baseball Statistician", 0.9, 0.8, false, null)]
                }), Encoding.UTF8, "application/json")
            });
        }
    }
}
