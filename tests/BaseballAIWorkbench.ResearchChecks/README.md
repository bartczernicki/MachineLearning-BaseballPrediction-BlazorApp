# Research regression checks

Run from the repository root with .NET 10:

```sh
dotnet run --project tests/BaseballAIWorkbench.ResearchChecks/BaseballAIWorkbench.ResearchChecks.csproj
```

This standalone executable adds no NuGet dependencies or test framework. A failed assertion exits nonzero. The default run is offline: model requests use an in-memory Responses transport, MCP uses a temporary loopback server, and credentials are fixture strings. No Azure/Web IQ credentials, paid calls, application server, or dashboard are required. The existing bundled ML models are loaded without modification for the three-agent orchestration check.

The checks cover:

- Three concurrent searches, exact Web IQ arguments, local result/content limits, structured results and text-JSON compatibility, canonical URL deduplication and query provenance.
- Invalid/private URLs and arbitrary source IDs, two page-read attempts including failures, concurrent read caching, explicit partial failures, all-failed and malformed responses, successful empty results, and cancellation.
- Untrusted page instructions remaining evidence data, not an additional instruction channel. This tests data handling, not a guarantee that a model can never follow prompt injection.
- The real `AIAgents` pipeline: Encyclopedia medium reasoning, only `read_commentary_source`, two tool rounds followed by tool-free synthesis, and complete Responses continuation history.
- Two-/three-agent overlap and selection ordering, wait-all failure behavior and MCP cleanup, N/A omissions, and Agent Q's unchanged required calculation tool and high reasoning effort.

## Optional fixed-evidence prompt comparison

Validate the seven synthetic evidence fixtures and pinned baseline prompt extraction without credentials, file output, or model calls:

```sh
dotnet run --project tests/BaseballAIWorkbench.ResearchChecks/BaseballAIWorkbench.ResearchChecks.csproj -- --prompt-eval --dry-run --results-dir /private/tmp/baseball-prompt-evaluation
```

An explicitly requested live comparison can use the existing AppHost user-secrets and deployment. It makes up to 14 billed Responses completions (baseline and revised prompts for each case), with no Web IQ searches. Supply a fresh output directory **outside the checkout**:

```sh
dotnet run --project tests/BaseballAIWorkbench.ResearchChecks/BaseballAIWorkbench.ResearchChecks.csproj -- --prompt-eval --results-dir /private/tmp/baseball-prompt-evaluation
```

The baseline is pinned to commit `b2c4f73daf515e72eb4151f66223d5ba11b91c8f`, so that commit must be available locally. The synthetic players, writers, publications, and evidence are test data, not baseball claims or live sources. Saved answers and `summary.json` remain outside the repository. Review attribution, hypothetical-outcome handling, scenario applicability, unsupported certainty, uncertainty ranges, and abstentions in addition to the automatic structure checks. These cases do **not** establish empirical probability calibration.
