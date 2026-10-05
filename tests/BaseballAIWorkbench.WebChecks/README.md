# Analysis rendering regression checks

Run from the repository root with .NET 10:

```sh
dotnet run --project tests/BaseballAIWorkbench.WebChecks/BaseballAIWorkbench.WebChecks.csproj
```

This standalone executable uses the production `BaseballApiClient` and `AgenticAnalysisCard`, with no test framework or additional package references. AngleSharp is supplied transitively by the production HTML sanitizer. A failed assertion exits nonzero.

Responses come from an in-memory HTTP handler using the typed analysis response contract. The checks need no running application, browser, credentials, model execution, or paid services. The first build can restore ordinary NuGet dependencies.

The checks cover:

- Application-owned probability, agent provenance, formula sensitivity, estimate range and disagreement displays, including omitted agents, single-agent partial/full abstention and one-contributor N/A spread.
- Recommendation thresholds calculated from original probabilities, with existing percentage rounding and clipping preserved.
- Deliberately incorrect narrative numbers and tables never supplying the application's numeric tables. Legacy Probability Assessment sections and all Markdown tables are removed, including fenced, indented, malformed and nested tables; later evidence, caveats and source footers survive.
- Encoded application notices, agent names, abstention reasons and loading/error status; initially collapsed formula details and individually scrollable numeric tables.
- Missing required numeric fields, explicit null aggregate structures and legacy string responses being rejected instead of becoming invented zero values.
- Preserved narrative headings, emphasis, lists, blockquotes, definitions, literal code and safe citation links.
- Final numbered Encyclopedia source footers with escaped titles, long titles and canonical URL fallbacks.
- Every surviving inline, reference and automatic hyperlink opening a new tab with `target="_blank"` and `rel="noopener noreferrer"`; untrusted attribute-like text cannot override that behavior.
- No active scripts, frames, forms, SVG, MathML, images, media, event handlers, inline styles, custom classes/IDs or data attributes in narrative content. Disallowed link destinations are removed, including entity and control-character obfuscations.
- Safe DOM structure after the sanitized narrative passes through the real Blazor component's `MarkupString` boundary, separately from its typed numeric sections.
