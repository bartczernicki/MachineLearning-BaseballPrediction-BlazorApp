using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using BaseballAIWorkbench.Common.Agents;
using BaseballAIWorkbench.Common.MachineLearning;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using OpenAI;
using System.ClientModel;
using System.ClientModel.Primitives;

namespace BaseballAIWorkbench.ResearchChecks;

// Opt-in live evaluation only. All players, authors, and sources in these cases are fictional.
// Artifacts are written outside the repository so synthetic model answers cannot become app content.
internal static class PromptEvaluation
{
    private const string BaselineCommit = "b2c4f73daf515e72eb4151f66223d5ba11b91c8f";
    private const string PromptsPath = "src/BaseballAIWorkbench/BaseballAIWorkbench.Common/Agents/Agents.cs";
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true, PropertyNameCaseInsensitive = true };

    public static async Task<int> RunAsync(string[] args)
    {
        var root = FindRepositoryRoot();
        var resultsIndex = Array.IndexOf(args, "--results-dir");
        if (resultsIndex < 0 || resultsIndex + 1 >= args.Length)
            throw new ArgumentException("Live prompt evaluation requires --results-dir with an output directory outside the repository.");

        var resultsDirectory = Path.GetFullPath(args[resultsIndex + 1]);
        var relativePath = Path.GetRelativePath(root, resultsDirectory);
        if (relativePath == "." || (!relativePath.StartsWith(".." + Path.DirectorySeparatorChar) && relativePath != ".." && !Path.IsPathRooted(relativePath)))
            throw new ArgumentException("Synthetic response artifacts must be written outside the repository.");

        var cases = JsonSerializer.Deserialize<EvidenceCase[]>(
            await File.ReadAllTextAsync(Path.Combine(root, "tests/BaseballAIWorkbench.ResearchChecks/PromptEvidenceCases.json")), JsonOptions)!;
        Check.That(cases.Length == 7, "The fixed-evidence suite must contain seven cases (14 completions). ");
        var baselineSource = await ReadBaselineSourceAsync(root);
        var baselineSystem = ExpandBaselineRules(ExtractCase(baselineSource, "GetAgentInstructions", "BaseballEncyclopedia"), baselineSource);
        var baselineTaskTemplate = ExpandBaselineRules(ExtractMethodPrompt(baselineSource, "GetInternetResearchAgentDecisionPrompt"), baselineSource);
        Check.That(baselineSystem.Contains("Baseball Encyclopedia") && baselineSystem.Contains("### Probability Assessment") &&
            baselineTaskTemplate.Contains("{baseballBatter.FullPlayerName}"), "Pinned baseline prompt extraction.");
        Check.That(cases.Select(evidenceCase => evidenceCase.Id).Distinct().Count() == cases.Length &&
            cases.All(evidenceCase => evidenceCase.Searches.Length == 3 &&
                evidenceCase.Sources.All(source => new Uri(source.Url).Host == "example.org")), "Synthetic fixture identities, search counts, and reserved source domain.");
        if (args.Contains("--dry-run"))
        {
            Console.WriteLine("PASS: seven fixed-evidence cases and exact pinned baseline prompts validated; no secrets read or model calls made.");
            return 0;
        }

        // Read credentials through the same standard user-secrets provider as AppHost. Never log them.
        var appHostProject = XDocument.Load(Path.Combine(root, "src/BaseballAIWorkbench/BaseballAIWorkbench.AppHost/BaseballAIWorkbench.AppHost.csproj"));
        var secretsId = appHostProject.Descendants("UserSecretsId").Single().Value;
        var configuration = new ConfigurationBuilder().AddUserSecrets(secretsId).Build();
        var endpoint = RequiredConfiguration(configuration, "ConnectionStrings:AOAIEndpoint");
        var apiKey = RequiredConfiguration(configuration, "ConnectionStrings:AOAIAPIKey");
        var deployment = RequiredConfiguration(configuration, "ConnectionStrings:AOAIModelDeploymentName");
        var client = new OpenAIClient(new ApiKeyCredential(apiKey), new OpenAIClientOptions
        {
            Endpoint = new Uri(new Uri(endpoint), "/openai/v1/"),
            // Keep the opt-in suite at at most 14 requests rather than retrying a billed completion.
            RetryPolicy = new ClientRetryPolicy(0)
        });
#pragma warning disable OPENAI001 // Evaluation uses the same experimental Responses adapter as the app.
        using var chatClient = client.GetResponsesClient().AsIChatClient(deployment);
#pragma warning restore OPENAI001
        Directory.CreateDirectory(resultsDirectory);

        var outcomes = new List<EvaluationOutcome>();
        // Two concurrent requests at most; each case compares the same fixed evidence, with no tools.
        foreach (var evidenceCase in cases)
        {
            var player = new MLBBaseballBatter
            {
                FullPlayerName = evidenceCase.PlayerName,
                ID = "synthetic-evaluation-only",
                YearsPlayed = evidenceCase.SelectedSeasons,
                LastYearPlayed = evidenceCase.LastYear
            };
            var dossier = FormatDossier(evidenceCase);
            var baselineTask = baselineTaskTemplate
                .Replace("{baseballBatter.FullPlayerName}", player.FullPlayerName)
                .Replace("{baseballBatter.ID}", player.ID)
                .Replace("{baseballBatter.LastYearPlayed}", player.LastYearPlayed.ToString(CultureInfo.InvariantCulture));
            var pair = await Task.WhenAll(
                EvaluateAsync(chatClient, evidenceCase, "baseline", baselineSystem, baselineTask + dossier, resultsDirectory),
                EvaluateAsync(chatClient, evidenceCase, "revised", Agents.GetAgentInstructions("BaseballEncyclopedia"),
                    Agents.GetInternetResearchAgentDecisionPrompt(player) + dossier, resultsDirectory));
            outcomes.AddRange(pair);
            foreach (var outcome in pair)
                Console.WriteLine($"PROMPT EVAL {outcome.CaseId}/{outcome.Variant}: " +
                    (outcome.Success ? $"{outcome.Checks.Count(check => check.Value)}/{outcome.Checks.Count} automatic checks; {outcome.ElapsedSeconds:F1}s" : "request failed (see sanitized artifact)"));
        }

        await File.WriteAllTextAsync(Path.Combine(resultsDirectory, "summary.json"), JsonSerializer.Serialize(new
        {
            BaselineCommit,
            SyntheticEvidenceOnly = true,
            ModelCalls = outcomes.Count,
            ManualReviewRequired = new[]
            {
                "Claims and author/voter roles are supported by supplied extracts, with no invented attribution.",
                "Known actual outcomes are not used as proof of the hypothetical assessment.",
                "The Encyclopedia does not independently extrapolate statistics or manufacture scenario-specific opinions.",
                "Sparse attention is an explicitly qualified inference only with adequate successful coverage.",
                "Disagreement, archives, scenario mismatch, and subjective uncertainty are accurately explained.",
                "Compare fixed-case baseline and revised responses; these checks do not establish empirical calibration."
            },
            Outcomes = outcomes
        }, JsonOptions));
        Console.WriteLine("Synthetic prompt comparison artifacts saved outside the repository. Qualitative review remains required.");
        return outcomes.Where(outcome => outcome.Variant == "revised").All(outcome => outcome.Success && outcome.Checks.All(check => check.Value)) ? 0 : 1;
    }

    private static async Task<EvaluationOutcome> EvaluateAsync(IChatClient client, EvidenceCase evidenceCase,
        string variant, string systemPrompt, string userPrompt, string resultsDirectory)
    {
        var stopwatch = Stopwatch.StartNew();
        EvaluationOutcome outcome;
        try
        {
            using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(120));
            var response = await client.GetResponseAsync(
                [new ChatMessage(ChatRole.System, systemPrompt), new ChatMessage(ChatRole.User, userPrompt)],
                new ChatOptions
                {
                    Reasoning = new ReasoningOptions { Effort = ReasoningEffort.Medium },
                    MaxOutputTokens = 4000
                }, deadline.Token);
            var answer = response.Text;
            outcome = new EvaluationOutcome(evidenceCase.Id, variant, true, stopwatch.Elapsed.TotalSeconds,
                answer, EvaluateStructure(evidenceCase, answer), null);
        }
        catch (Exception error)
        {
            // SDK errors can embed endpoint/request details. Persist only the exception type.
            outcome = new EvaluationOutcome(evidenceCase.Id, variant, false, stopwatch.Elapsed.TotalSeconds,
                "", [], error.GetType().Name);
        }
        await File.WriteAllTextAsync(Path.Combine(resultsDirectory, $"{evidenceCase.Id}-{variant}.json"), JsonSerializer.Serialize(outcome, JsonOptions));
        return outcome;
    }

    private static Dictionary<string, bool> EvaluateStructure(EvidenceCase evidenceCase, string answer)
    {
        var headings = Regex.Matches(answer, @"(?m)^### (.+?)\s*$").Select(match => match.Groups[1].Value.Trim()).ToArray();
        var rows = Regex.Matches(answer, @"(?mi)^\|\s*(Ballot Appearance|Induction)\s*\|\s*([^|]+)\|\s*([^|]+)\|\s*([^|]+)\|").Cast<Match>().ToArray();
        var probabilities = rows.Select(row => row.Groups[2].Value.Trim()).ToArray();
        var links = Regex.Matches(answer, @"\]\((https?://[^\s)]+)\)").Select(match => match.Groups[1].Value).ToArray();
        var allowedUrls = evidenceCase.Sources.Select(source => source.Url).ToHashSet(StringComparer.Ordinal);
        var keyEvidence = Regex.Match(answer, @"(?s)### Key Evidence\s*(.*?)### Caveats").Groups[1].Value;
        var numericValues = probabilities.Any(value => Regex.IsMatch(value, @"^\d{1,3}%$"));
        return new Dictionary<string, bool>
        {
            ["four_sections_in_order"] = headings.SequenceEqual(new[] { "Summary", "Probability Assessment", "Key Evidence", "Caveats" }),
            ["unchanged_probability_table_columns"] = Regex.IsMatch(answer, @"\|\s*Criterion\s*\|\s*Probability\s*\|\s*Qualitative Recommendation\s*\|\s*Rationale\s*\|"),
            ["two_parseable_probability_rows"] = rows.Length == 2 && rows.Select(row => row.Groups[1].Value).Distinct(StringComparer.OrdinalIgnoreCase).Count() == 2 &&
                probabilities.All(value => value.Equals("N/A", StringComparison.OrdinalIgnoreCase) || Regex.IsMatch(value, @"^\d{1,3}(?:\.\d+)?%$")),
            ["whole_percentage_points_or_abstention"] = probabilities.Length == 2 && probabilities.All(value => value.Equals("N/A", StringComparison.OrdinalIgnoreCase) || Regex.IsMatch(value, @"^\d{1,3}%$")),
            ["expected_abstention_policy"] = evidenceCase.ExpectedAssessment switch
            {
                "abstain" => probabilities.Length == 2 && probabilities.All(value => value.Equals("N/A", StringComparison.OrdinalIgnoreCase)),
                "numeric" => probabilities.Length == 2 && probabilities.All(value => Regex.IsMatch(value, @"^\d{1,3}%$")),
                _ => probabilities.Length == 2
            },
            ["citation_urls_from_evidence_only"] = links.All(allowedUrls.Contains),
            ["cites_sources_when_assessing_evidence"] = !numericValues || links.Length > 0,
            ["subjective_range_in_key_evidence"] = !numericValues || Regex.IsMatch(keyEvidence, @"(?i)(plausible|subjective).*(range|\d)|range.*(plausible|subjective)"),
            ["confidence_in_key_evidence"] = !numericValues || Regex.IsMatch(keyEvidence, @"(?i)confidence.*(low|medium|high)|(low|medium|high).*confidence"),
            // Flag obvious affirmative claims; the negative statement 'not statistically calibrated' is allowed.
            ["no_asserted_statistical_calibration"] = !Regex.IsMatch(answer, @"(?i)\b(is|are)\s+(?:a\s+)?statistically calibrated (?:probabilit|confidence)|\b95%\s+(?:statistical\s+)?confidence interval")
        };
    }

    private static string FormatDossier(EvidenceCase evidenceCase) => "\n\n" + JsonSerializer.Serialize(new
    {
        Notice = "SYNTHETIC FIXED-EVIDENCE EVALUATION. All people, publications, URLs, and statements below are fictional test fixtures. Do not use outside this test. Both prompt variants receive the same evidence. No additional research or page-read tools are available; assess only these extracts.",
        SelectedSeasons = evidenceCase.SelectedSeasons,
        Context = evidenceCase.Context,
        Searches = evidenceCase.Searches,
        Sources = evidenceCase.Sources
    }, JsonOptions);

    private static string RequiredConfiguration(IConfiguration configuration, string key) =>
        !string.IsNullOrWhiteSpace(configuration[key]) ? configuration[key]! : throw new InvalidOperationException($"Required existing configuration is missing: {key}");

    private static string FindRepositoryRoot()
    {
        foreach (var start in new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory })
            for (var directory = new DirectoryInfo(start); directory is not null; directory = directory.Parent)
                if (Directory.Exists(Path.Combine(directory.FullName, ".git")) || File.Exists(Path.Combine(directory.FullName, ".git"))) return directory.FullName;
        throw new InvalidOperationException("Run the evaluator from the existing repository.");
    }

    private static async Task<string> ReadBaselineSourceAsync(string root)
    {
        var start = new ProcessStartInfo("git") { WorkingDirectory = root, RedirectStandardOutput = true, RedirectStandardError = true };
        start.ArgumentList.Add("show");
        start.ArgumentList.Add($"{BaselineCommit}:{PromptsPath}");
        using var process = Process.Start(start)!;
        var source = await process.StandardOutput.ReadToEndAsync();
        await process.WaitForExitAsync();
        if (process.ExitCode != 0) throw new InvalidOperationException("The pinned pre-change prompt source is unavailable in git history.");
        return source;
    }

    private static string ExtractCase(string source, string method, string agentType)
    {
        var methodStart = source.IndexOf("public static string " + method + "(", StringComparison.Ordinal);
        var match = Regex.Match(source[methodStart..], "\\\"" + agentType + "\\\"\\s*=>\\s*\\$?\\\"\\\"\\\"(?<prompt>.*?)\\\"\\\"\\\"", RegexOptions.Singleline);
        if (!match.Success) throw new InvalidOperationException("The pinned baseline role prompt could not be extracted.");
        return Unindent(match.Groups["prompt"].Value);
    }

    private static string ExtractMethodPrompt(string source, string method)
    {
        var methodStart = source.IndexOf("public static string " + method + "(", StringComparison.Ordinal);
        var match = Regex.Match(source[methodStart..], "\\$?\\\"\\\"\\\"(?<prompt>.*?)\\\"\\\"\\\"", RegexOptions.Singleline);
        if (!match.Success) throw new InvalidOperationException("The pinned baseline task prompt could not be extracted.");
        return Unindent(match.Groups["prompt"].Value);
    }

    private static string ExpandBaselineRules(string template, string source)
    {
        foreach (var name in new[] { "AgentMarkdownOutputRules", "ProbabilityAssessmentRules" })
        {
            var match = Regex.Match(source, name + "\\s*=\\s*\\\"\\\"\\\"(?<prompt>.*?)\\\"\\\"\\\"", RegexOptions.Singleline);
            if (!match.Success) throw new InvalidOperationException("The pinned baseline output rules could not be extracted.");
            template = template.Replace("{" + name + "}", Unindent(match.Groups["prompt"].Value));
        }
        return template;
    }

    private static string Unindent(string raw)
    {
        var lines = raw.Trim('\r', '\n').Split('\n');
        var indent = lines.Where(line => !string.IsNullOrWhiteSpace(line)).Min(line => line.Length - line.TrimStart().Length);
        return string.Join("\n", lines.Select(line => line.Length >= indent ? line[indent..].TrimEnd('\r') : "")).TrimEnd();
    }

    private sealed record EvidenceCase(string Id, string PlayerName, int SelectedSeasons, int LastYear,
        string ExpectedAssessment, string Context, SearchFixture[] Searches, SourceFixture[] Sources);
    private sealed record SearchFixture(string Purpose, string Status, string Note);
    private sealed record SourceFixture(string SourceId, string Title, string Url, string PublicationDate, string Author,
        string DocumentedRole, string Extract);
    private sealed record EvaluationOutcome(string CaseId, string Variant, bool Success, double ElapsedSeconds,
        string Response, Dictionary<string, bool> Checks, string? ErrorType);
}
