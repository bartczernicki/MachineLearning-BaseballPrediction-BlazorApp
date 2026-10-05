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
        "href", "title", "colspan", "rowspan", "start"
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
                    var html = multipleAgents
                        ? await client.GetBaseballPlayerAnalysisMultipleModels(config)
                        : await client.GetBaseballPlayerAnalysis(config);
                    That(handler.RequestCount == 1, "Exactly one fake POST serves the requested endpoint");
                    var body = parser.ParseDocument(html).Body!;
                    AssertSafeElements(body);
                    fixture.Verify(body);

                    // Exercise the real component's MarkupString boundary as well as the client fragment.
                    var rendered = await renderer.Dispatcher.InvokeAsync(async () =>
                    {
                        var component = await renderer.RenderComponentAsync<AgenticAnalysisCard>(
                            ParameterView.FromDictionary(new Dictionary<string, object?>
                            {
                                [nameof(AgenticAnalysisCard.IsVisible)] = true,
                                [nameof(AgenticAnalysisCard.AgenticAnalysis)] = html
                            }));
                        return component.ToHtmlString();
                    });
                    var renderedDocument = parser.ParseDocument(rendered);
                    var output = renderedDocument.QuerySelector(".agentic-analysis-output");
                    That(output is not null, "The real analysis output wrapper survives the supplied content");
                    That(renderedDocument.QuerySelectorAll(".agentic-analysis-output").Length == 1,
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

            ## Probability Assessment

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

        yield return new("pipe table and numeric report", """
            ### Probability Assessment

            | Criterion | Probability | Rationale |
            |---|---:|---|
            | Ballot Appearance | 92.59% | [Source](https://example.org/report) |
            | Induction | < 0.10% | **Limited evidence** |
            """, root =>
        {
            AssertTable(root, "Ballot Appearance", "92.59%", "Induction", "< 0.10%");
            That(root.QuerySelector("td strong")?.TextContent == "Limited evidence", "Table formatting survives");
            That(root.QuerySelector("td a")?.GetAttribute("href") == "https://example.org/report", "Table citation survives");
        });

        yield return new("grid table", """
            +---------+-------+
            | Agent   | Value |
            +=========+=======+
            | Expert  | 80%   |
            +---------+-------+
            """, root => AssertTable(root, "Expert", "80%"));

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
            AssertTable(root, "Induction", "80.00%");
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
            AssertTable(root, "Expert", "80%");
            That(root.TextContent.Contains("Additional caveats"), "Text after a repaired fenced table survives");
        });

        yield return new("indented and glued table normalization", """
            ### Probability Assessment

               | Criterion | Probability |
               |---|---|| Ballot Appearance | 90% |

               | Induction | 80% |
            """, root => AssertTable(root, "Ballot Appearance", "90%", "Induction", "80%"));

        yield return new("safe citation and navigation links", """
            [HTTPS](https://example.org/source?x=1&y=2 "Article title")
            [HTTP](http://example.org/archive)
            [Relative](/reports/player?q=1#evidence)
            [Child](research/player)
            [Fragment](#evidence)

            https://example.org/commentary
            """, root =>
        {
            var expected = new Dictionary<string, string>
            {
                ["HTTPS"] = "https://example.org/source?x=1&y=2",
                ["HTTP"] = "http://example.org/archive",
                ["Relative"] = "/reports/player?q=1#evidence",
                ["Child"] = "research/player",
                ["Fragment"] = "#evidence",
                ["https://example.org/commentary"] = "https://example.org/commentary"
            };
            foreach (var (text, href) in expected)
                That(root.QuerySelectorAll("a").Any(a => a.TextContent == text && a.GetAttribute("href") == href),
                    $"Safe link '{text}' remains clickable");
            That(root.QuerySelector("a")!.GetAttribute("title") == "Article title", "Safe citation title survives");
        });

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
            ### Heading {#hostile .hostile onclick="alert(1)" style="color:red"}

            [Citation](https://example.org/source){target="_blank" onclick="alert(1)" data-secret="value"}

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

    private static void AssertTable(IElement root, params string[] cellValues)
    {
        That(root.QuerySelectorAll("table").Length == 1, "Exactly one report table survives");
        var cells = root.QuerySelectorAll("th,td").Select(cell => cell.TextContent.Trim()).ToArray();
        foreach (var value in cellValues)
            That(cells.Contains(value), $"Report cell '{value}' survives");
    }

    private static void AssertSafeElements(IElement root)
    {
        foreach (var element in root.QuerySelectorAll("*"))
        {
            That(SafeElements.Contains(element.LocalName), $"Unexpected rendered element: {element.LocalName}");
            foreach (var attribute in element.Attributes)
                That(SafeAttributes.Contains(attribute.Name), $"Unexpected rendered attribute: {element.LocalName}[{attribute.Name}]");

            if (element.GetAttribute("href") is { } href)
            {
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
                Content = new StringContent(JsonSerializer.Serialize(markdown), Encoding.UTF8, "application/json")
            });
        }
    }
}
