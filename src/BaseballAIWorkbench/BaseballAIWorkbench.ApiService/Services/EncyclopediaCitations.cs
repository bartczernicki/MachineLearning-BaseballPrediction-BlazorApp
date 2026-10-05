using System.Text;
using Markdig;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;

namespace BaseballAIWorkbench.ApiService.Services;

internal sealed record EncyclopediaCitation(string Title, string Url);

internal static class EncyclopediaCitations
{
    // Match the web renderer's link-containing extensions, including tables and definitions.
    private static readonly MarkdownPipeline CitationPipeline = new MarkdownPipelineBuilder()
        .UsePipeTables()
        .UseGridTables()
        .UseAutoLinks()
        .UseEmphasisExtras()
        .UseDefinitionLists()
        .DisableHtml()
        .Build();

    internal static IEnumerable<string> GetLinkedUrls(string markdown)
    {
        var document = Markdown.Parse(markdown, CitationPipeline);
        foreach (var inline in document.Descendants<Inline>())
        {
            // An image's alternative text can contain parsed links, but no such link is clickable.
            if (HasImageAncestor(inline))
                continue;

            var url = inline switch
            {
                LinkInline { IsImage: false } link => link.Url,
                AutolinkInline { IsEmail: false } link => link.Url,
                _ => null
            };
            if (!string.IsNullOrWhiteSpace(url))
                yield return url;
        }
    }

    internal static string AppendTo(string markdown, IEnumerable<EncyclopediaCitation> citations)
    {
        var result = new StringBuilder(markdown.TrimEnd());
        result.Append("\n\n### Encyclopedia Sources\n\n");
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var count = 0;
        foreach (var citation in citations)
        {
            var url = EncyclopediaResearch.CanonicalPublicUrl(citation.Url);
            if (url is null || !seen.Add(url))
                continue;

            var title = string.IsNullOrWhiteSpace(citation.Title) ? url : citation.Title;
            // Angle-delimited destinations preserve parentheses; escaping ampersands prevents
            // entity-looking query values from being decoded into a different destination.
            result.Append(++count).Append(". [").Append(EscapeLabel(title)).Append("](<")
                .Append(url.Replace("&", "&amp;", StringComparison.Ordinal)).Append(">)\n");
        }

        if (count == 0)
            result.Append("No retrieved source links were cited\n");

        return result.ToString().TrimEnd();
    }

    private static bool HasImageAncestor(Inline inline)
    {
        for (var parent = inline.Parent; parent is not null; parent = parent.Parent)
        {
            if (parent is LinkInline { IsImage: true })
                return true;
        }

        return false;
    }

    private static string EscapeLabel(string value)
    {
        var escaped = new StringBuilder(value.Length);
        foreach (var character in value)
        {
            if (char.IsWhiteSpace(character) || char.IsControl(character))
            {
                escaped.Append(' ');
                continue;
            }

            // CommonMark allows every ASCII punctuation character to be backslash-escaped.
            if ("!\"#$%&'()*+,-./:;<=>?@[\\]^_`{|}~".Contains(character))
                escaped.Append('\\');
            escaped.Append(character);
        }

        return escaped.ToString();
    }
}
