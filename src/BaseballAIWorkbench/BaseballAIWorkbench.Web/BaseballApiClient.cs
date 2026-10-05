
using BaseballAIWorkbench.Common.Agents;
using Ganss.Xss;
using Markdig;
using Markdig.Renderers.Html;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;
using Newtonsoft.Json;
using System.Net.Http.Json;
using System.Text.RegularExpressions;

namespace BaseballAIWorkbench.Web
{
    public class BaseballApiClient(HttpClient httpClient)
    {
        private static readonly MarkdownPipeline MarkdownPipeline = new MarkdownPipelineBuilder()
            .UsePipeTables()
            .UseGridTables()
            .UseAutoLinks()
            .UseEmphasisExtras()
            .UseDefinitionLists()
            .DisableHtml()
            .Build();

        // Configure once and keep immutable: Sanitize supports concurrent calls.
        private static readonly HtmlSanitizer AgentHtmlSanitizer = CreateAgentHtmlSanitizer();

        private static HtmlSanitizer CreateAgentHtmlSanitizer()
        {
            var sanitizer = new HtmlSanitizer();
            sanitizer.AllowedTags.Clear();
            sanitizer.AllowedTags.UnionWith([
                "p", "br", "hr", "h1", "h2", "h3", "h4", "h5", "h6",
                "strong", "em", "b", "i", "del", "s", "ins", "sub", "sup", "mark",
                "blockquote", "pre", "code", "ul", "ol", "li", "dl", "dt", "dd",
                "table", "thead", "tbody", "tfoot", "tr", "th", "td", "caption", "a"
            ]);
            sanitizer.AllowedAttributes.Clear();
            sanitizer.AllowedAttributes.UnionWith(["href", "title", "target", "rel", "colspan", "rowspan", "start"]);
            sanitizer.AllowedSchemes.Clear();
            sanitizer.AllowedSchemes.UnionWith(["http", "https"]);
            // Allowed URI attributes must also be screened against the scheme allowlist.
            sanitizer.UriAttributes.Clear();
            sanitizer.UriAttributes.Add("href");
            sanitizer.AllowedCssProperties.Clear();
            sanitizer.AllowedAtRules.Clear();
            sanitizer.AllowDataAttributes = false;
            return sanitizer;
        }

        public async Task<int> GetBaseballPlayerCountAsync(CancellationToken cancellationToken = default)
        {
            var playerCount = await httpClient.GetFromJsonAsync<int>("/Players", cancellationToken);
            return playerCount;
        }

        public async Task<string> GetBaseballPlayerAnalysis(AgenticAnalysisConfig agenticAnalysisConfig, CancellationToken cancellationToken = default)
        {
            var playerAnalysis = await httpClient.PostAsJsonAsync<AgenticAnalysisConfig>("/BaseballPlayerAnalysisML", agenticAnalysisConfig, cancellationToken);

            playerAnalysis.EnsureSuccessStatusCode();

            var playerAnalysisString = await playerAnalysis.Content.ReadAsStringAsync(cancellationToken);

            string bingSourcePattern = @"【[^】]*】";        // match an opening 【, then any chars except 】, then a closing 】
            string cleanedAnalysis = Regex.Replace(playerAnalysisString, bingSourcePattern, "");

            return ConvertAgentMarkdownToHtml(cleanedAnalysis);
        }

        public async Task<string> GetBaseballPlayerAnalysisMultipleModels(AgenticAnalysisConfig agenticAnalysisConfig, CancellationToken cancellationToken = default)
        {
            var playerAnalysis = await httpClient.PostAsJsonAsync<AgenticAnalysisConfig>("/BaseballPlayerAnalysisMultipleAgents", agenticAnalysisConfig, cancellationToken);

            playerAnalysis.EnsureSuccessStatusCode();

            var playerAnalysisString = await playerAnalysis.Content.ReadAsStringAsync(cancellationToken);

            string bingSourcePattern = @"【[^】]*】";        // match an opening 【, then any chars except 】, then a closing 】
            string cleanedAnalysis = Regex.Replace(playerAnalysisString, bingSourcePattern, "");

            return ConvertAgentMarkdownToHtml(cleanedAnalysis);
        }

        private static string ConvertAgentMarkdownToHtml(string jsonString)
        {
            var markdown = NormalizeAgentMarkdown(JsonConvert.DeserializeObject<string>(jsonString) ?? string.Empty);
            var document = Markdown.Parse(markdown, MarkdownPipeline);
            foreach (var inline in document.Descendants<Inline>())
            {
                if (inline is LinkInline { IsImage: false } or AutolinkInline)
                {
                    // Apply trusted navigation attributes before the final sanitization step.
                    var attributes = inline.GetAttributes();
                    attributes.AddProperty("target", "_blank");
                    attributes.AddProperty("rel", "noopener noreferrer");
                }
            }
            var html = document.ToHtml(MarkdownPipeline);
            // Keep sanitization last: generated HTML is rendered through Blazor's MarkupString.
            return AgentHtmlSanitizer.Sanitize(html);
        }

        private static string NormalizeAgentMarkdown(string markdown)
        {
            markdown = markdown.Replace("\r\n", "\n").Replace('\r', '\n').Trim();

            markdown = Regex.Replace(
                markdown,
                @"\A\s*```(?:markdown|md)?\s*\n(?<content>[\s\S]*?)\n```\s*\z",
                "${content}",
                RegexOptions.IgnoreCase);

            markdown = UnwrapFencedMarkdownTables(markdown);
            markdown = NormalizeHeadingLevels(markdown);
            markdown = RepairMalformedPipeTables(markdown);

            return markdown.Trim();
        }

        private static string NormalizeHeadingLevels(string markdown)
        {
            return Regex.Replace(
                markdown,
                @"(?m)^(#{1,2})([ \t]+)(.+)$",
                "###$2$3");
        }

        private static string RepairMalformedPipeTables(string markdown)
        {
            var repairedLines = new List<string>();

            foreach (var line in markdown.Split('\n'))
            {
                foreach (var splitLine in SplitMalformedTableLine(TrimLeadingTableIndent(line)))
                {
                    repairedLines.Add(splitLine);
                }
            }

            return RemoveBlankLinesInsideTables(repairedLines);
        }

        private static string TrimLeadingTableIndent(string line)
        {
            var trimmed = line.TrimStart(' ', '\t');
            var leadingWhitespaceCount = line.Length - trimmed.Length;

            return leadingWhitespaceCount is > 0 and <= 3 && trimmed.StartsWith('|')
                ? trimmed
                : line;
        }

        private static IEnumerable<string> SplitMalformedTableLine(string line)
        {
            var trimmed = line.Trim();
            var gluedRowIndex = trimmed.IndexOf("||", StringComparison.Ordinal);

            if (gluedRowIndex > 0)
            {
                var separatorCandidate = trimmed[..(gluedRowIndex + 1)];
                var rowCandidate = $"|{trimmed[(gluedRowIndex + 2)..].TrimStart()}";

                if (IsMarkdownTableSeparator(separatorCandidate) && IsMarkdownTableRow(rowCandidate))
                {
                    yield return separatorCandidate;
                    yield return rowCandidate;
                    yield break;
                }
            }

            yield return line;
        }

        private static string RemoveBlankLinesInsideTables(IReadOnlyList<string> lines)
        {
            var cleanedLines = new List<string>();

            for (var i = 0; i < lines.Count; i++)
            {
                if (string.IsNullOrWhiteSpace(lines[i])
                    && HasAdjacentTableLine(lines, i, -1)
                    && HasAdjacentTableLine(lines, i, 1))
                {
                    continue;
                }

                cleanedLines.Add(lines[i]);
            }

            return string.Join('\n', cleanedLines);
        }

        private static bool HasAdjacentTableLine(IReadOnlyList<string> lines, int index, int direction)
        {
            for (var i = index + direction; i >= 0 && i < lines.Count; i += direction)
            {
                if (string.IsNullOrWhiteSpace(lines[i]))
                {
                    continue;
                }

                return IsMarkdownTableRow(lines[i].Trim());
            }

            return false;
        }

        private static string UnwrapFencedMarkdownTables(string markdown)
        {
            return Regex.Replace(
                markdown,
                @"(?ms)(^|\n)```(?:markdown|md)?[ \t]*\n(?<content>[\s\S]*?)\n```(?=\n|$)",
                match =>
                {
                    var content = match.Groups["content"].Value.Trim('\n');
                    return LooksLikeMarkdownTable(content)
                        ? $"{match.Groups[1].Value}{content}"
                        : match.Value;
                },
                RegexOptions.IgnoreCase);
        }

        private static bool LooksLikeMarkdownTable(string markdown)
        {
            var lines = markdown
                .Split('\n')
                .Select(line => line.Trim())
                .Where(line => line.Length > 0)
                .ToArray();

            for (var i = 0; i < lines.Length - 1; i++)
            {
                if (IsMarkdownTableRow(lines[i]) && IsMarkdownTableSeparator(lines[i + 1]))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsMarkdownTableRow(string line)
        {
            return line.StartsWith('|') && line.EndsWith('|') && line.Count(character => character == '|') >= 2;
        }

        private static bool IsMarkdownTableSeparator(string line)
        {
            return IsMarkdownTableRow(line) && Regex.IsMatch(line, @"^\|[ \t:\-|\u2013\u2014]+\|$");
        }
    }
}
