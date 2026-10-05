# Analysis rendering regression checks

Run from the repository root with .NET 10:

```sh
dotnet run --project tests/BaseballAIWorkbench.WebChecks/BaseballAIWorkbench.WebChecks.csproj
```

This standalone executable uses the Web project's production `BaseballApiClient` and `AgenticAnalysisCard`, with no test framework or additional package references. AngleSharp is supplied transitively by the production HTML sanitizer. A failed assertion exits nonzero.

All analysis responses come from an in-memory HTTP handler returning the existing JSON-string response shape. The checks require no running application, browser, network calls, credentials, model execution, or paid services. The first build can restore ordinary NuGet dependencies.

Both single-agent and multiple-agent endpoints are checked for:

- Preserved headings, emphasis, lists, blockquotes, definition lists, literal code, pipe/grid tables, and HTTP/HTTPS/relative/fragment citation links.
- Existing fenced, indented, and malformed table normalization and exact report values.
- Final numbered Encyclopedia source footers with canonical clickable URLs, safely escaped titles, long titles, and URL fallbacks.
- Every surviving inline, reference, and automatic hyperlink opens a new tab with `target="_blank"` and `rel="noopener noreferrer"`; untrusted attribute-like text cannot override that behavior.
- Raw HTML displayed as text, with no active scripts, frames, forms, SVG, MathML, images, media, or other embedded resources.
- Removal of unsafe link destinations, including mixed-case, entity, and control-character obfuscations.
- Absence of event handlers, inline styles, custom classes/IDs, and data attributes in generated content; navigation attributes appear only on links.
- Safe DOM structure after the sanitized response passes through the real Blazor component's `MarkupString` boundary.
