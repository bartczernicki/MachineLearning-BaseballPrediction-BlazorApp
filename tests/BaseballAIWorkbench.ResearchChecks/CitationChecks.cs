using System.Xml.Linq;
using BaseballAIWorkbench.ApiService.Services;
using Markdig;
using Microsoft.Extensions.Logging.Abstractions;

namespace BaseballAIWorkbench.ResearchChecks;

internal static class CitationChecks
{
    internal static async Task RunAsync()
    {
        await LinkedSourcesAsync();
        await NonCitationContentAsync();
        await CanonicalAndUnregisteredUrlsAsync();
        FooterRendering();
        EmptyAndInvalidSources();
        Console.WriteLine("PASS citations: Markdown links, registry matching, first-citation order, excluded code/images, and safe source footers.");
    }

    private static async Task LinkedSourcesAsync()
    {
        var research = await ResearchAsync(
            RetrievalChecks.Source("https://example.org/first?utm_source=fixture#original", "First registry title"),
            RetrievalChecks.Source("https://example.org/second", "Second registry title"),
            RetrievalChecks.Source("https://example.org/angle", "Angle-link title"),
            RetrievalChecks.Source("https://example.org/bare", "Bare-link title"),
            RetrievalChecks.Source("https://example.org/report_(2025)?a=1&b=2", "Definition title"));
        const string markdown = """
            ### Key Evidence
            [Model-written label][second]

            | Evidence | Source |
            |---|---|
            | First | [Model-written title](https://EXAMPLE.ORG/first?utm_campaign=cited#section) |

            <https://example.org/angle>

            https://example.org/bare

            Commentary
            : [A definition](<https://example.org/report_(2025)?a=1&amp;b=2>)

            [Duplicate](https://example.org/second)

            [second]: https://example.org/second "Reference label"
            """;

        var citations = research.GetCitations(markdown);
        Check.That(citations.Select(citation => citation.Url).SequenceEqual(new[]
        {
            "https://example.org/second", "https://example.org/first", "https://example.org/angle",
            "https://example.org/bare", "https://example.org/report_(2025)?a=1&b=2"
        }), "Inline, reference, angle, bare, table and definition links match registered URLs in first-citation order");
        Check.That(citations[0].Title == "Second registry title" && citations[1].Title == "First registry title",
            "Source labels come from retrieval metadata, never the model's link labels");
        Check.That(research.GetCitations("An uncited summary.").Count == 0,
            "Retrieved sources alone do not become citations");
    }

    private static async Task NonCitationContentAsync()
    {
        var research = await ResearchAsync(
            RetrievalChecks.Source("https://example.org/inline-code", "Inline code"),
            RetrievalChecks.Source("https://example.org/fenced-code", "Fenced code"),
            RetrievalChecks.Source("https://example.org/image", "Image"),
            RetrievalChecks.Source("https://example.org/alt-link", "Image alternative text"),
            RetrievalChecks.Source("https://example.org/unused", "Unused reference"));
        const string markdown = """
            `[Code](https://example.org/inline-code)`

            ```markdown
            [Fenced link](https://example.org/fenced-code)
            ```

            ![Image](https://example.org/image)

            ![Alternative [nested link](https://example.org/alt-link)](https://example.org/image)

            [Not retrieved](https://example.org/invented)
            [Not HTTP](javascript:alert(1))
            [Relative](/unused)

            [unused]: https://example.org/unused
            """;

        Check.That(research.GetCitations(markdown).Count == 0,
            "Code, images and their descendants, unused references, unregistered URLs, and unsafe/relative links are not citations");
    }

    private static async Task CanonicalAndUnregisteredUrlsAsync()
    {
        var research = await ResearchAsync(
            RetrievalChecks.Source("https://example.org/commentary?utm_source=search#section", "Original title"),
            RetrievalChecks.Source("https://example.org/commentary", "Duplicate title"),
            RetrievalChecks.Source("javascript:alert(1)", "Unsafe source"),
            RetrievalChecks.Source("https://127.0.0.1/private", "Private source"));
        var citations = research.GetCitations("""
            [Cited](https://EXAMPLE.ORG/commentary?gclid=tracking#paragraph)
            [Duplicate](https://example.org/commentary)
            [Invented destination](https://example.org/commentary?article=other)
            [Unsafe](javascript:alert(1))
            [Private](https://127.0.0.1/private)
            """);
        Check.That(citations.Count == 1 && citations[0] == new EncyclopediaCitation("Original title", "https://example.org/commentary"),
            "Canonical URL matching deduplicates tracking/fragment variants without accepting different articles or unsafe sources");
    }

    private static void FooterRendering()
    {
        const string title = "Writer ](javascript:alert(1))\n# Forged <img src=x onerror=alert(2)> [other](https://attacker.invalid) &copy; \\ **bold**";
        const string url = "https://example.org/report_(2025)?x=&copy;&y=&amp;&encoded=%3Csource%3E";
        var footer = EncyclopediaCitations.AppendTo("### Summary\nOriginal analysis", [
            new EncyclopediaCitation(title, url),
            new EncyclopediaCitation("Duplicate title", url + "&utm_source=duplicate#section"),
            new EncyclopediaCitation("   ", "https://example.org/second")
        ]);

        // Parse actual generated HTML with raw HTML enabled: escaping must stand on its own,
        // independently of the web client's additional raw-HTML disabling and sanitizer.
        var root = XElement.Parse("<root>" + Markdown.ToHtml(footer) + "</root>");
        var links = root.Descendants("a").ToArray();
        Check.That(links.Length == 2 && (string?)links[0].Attribute("href") == url,
            "Footer destinations retain parentheses, encoded angles, and literal entity-looking query values");
        Check.That(links[0].Value == title.Replace('\n', ' '),
            "Untrusted source titles round-trip as literal text without adding markup or links");
        Check.That(links[1].Value == "https://example.org/second",
            "A missing source title falls back to its canonical URL");
        Check.That(root.Elements("h3").Select(heading => heading.Value).SequenceEqual(new[] { "Summary", "Encyclopedia Sources" })
            && root.Element("p")?.Value == "Original analysis" && root.Element("ol")?.Elements("li").Count() == 2,
            "Exactly one numbered footer follows the unchanged analysis");
        Check.That(root.Descendants().All(element => new[] { "h3", "p", "ol", "li", "a" }.Contains(element.Name.LocalName))
            && root.Descendants().SelectMany(element => element.Attributes()).All(attribute => attribute.Name == "href"),
            "Malicious title punctuation cannot create active elements, attributes, or extra links");
    }

    private static void EmptyAndInvalidSources()
    {
        var empty = EncyclopediaCitations.AppendTo("An abstained analysis", []);
        Check.That(empty == "An abstained analysis\n\n### Encyclopedia Sources\n\nNo retrieved source links were cited",
            "An empty citation set is disclosed explicitly without invented links");
        var invalid = EncyclopediaCitations.AppendTo("An abstained analysis", [
            new EncyclopediaCitation("Script", "javascript:alert(1)"),
            new EncyclopediaCitation("Data", "data:text/html,malicious"),
            new EncyclopediaCitation("Credentials", "https://user:password@example.org/private"),
            new EncyclopediaCitation("Local", "https://localhost/private")
        ]);
        Check.That(invalid == empty, "The footer independently rejects unsafe or non-public destinations");
    }

    private static async Task<EncyclopediaResearch> ResearchAsync(params object[] sources)
    {
        var searchIndex = 0;
        var research = new EncyclopediaResearch((tool, _, _) =>
        {
            Check.That(tool == "web", "Citation collection needs no additional page reads or model calls");
            var start = (Interlocked.Increment(ref searchIndex) - 1) * 5;
            return Task.FromResult(RetrievalChecks.Structured(RetrievalChecks.Results(sources.Skip(start).Take(5).ToArray())));
        }, CancellationToken.None, NullLogger.Instance);
        await research.SearchAsync("Citation Fixture");
        return research;
    }
}
