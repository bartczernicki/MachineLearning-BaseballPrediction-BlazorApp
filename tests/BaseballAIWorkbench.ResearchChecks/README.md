# Research regression checks

Run from the repository root with .NET 10:

```sh
dotnet run --project tests/BaseballAIWorkbench.ResearchChecks/BaseballAIWorkbench.ResearchChecks.csproj
```

This standalone executable adds no NuGet dependencies or test framework. A failed assertion exits nonzero. The default run is offline: model requests use an in-memory Responses transport, MCP uses a temporary loopback server, and credentials are fixture strings. No Azure/Web IQ credentials, paid calls, application server, or dashboard are required. The existing bundled ML models are loaded without modification for the three-agent orchestration check.

The checks cover:

- Exact casing and presence of all six packaged model files, real predictions through the shared production registration from an empty working directory, and the Web application's two-model GAM selection.
- Actual ServiceDefaults OpenTelemetry collection: exact per-call input/output token histogram sums and counts, agent/chat span nesting, concurrent agents plus Agent Q, no duplicate agent-level token metrics, completed error/cancellation spans, and no invented usage when a response omits it. Sensitive message capture remains disabled even when the SDK's environment opt-in is enabled.
- Production HTTP resilience configuration: unsafe methods never retry transient responses or transport failures, GET retains retries, and client timeouts and circuit-breaker sampling allow the API's full processing budget.
- Both analysis handlers propagate request cancellation; short loopback endpoint timeouts return HTTP 504, cancel downstream model work, dispose MCP scopes, and skip Agent Q. Run without a debugger attached so ASP.NET Core request timeouts are active.
- Three concurrent searches, exact Web IQ arguments, local result/content limits, structured results and text-JSON compatibility, canonical URL deduplication and query provenance.
- Invalid/private URLs and arbitrary source IDs, two page-read attempts including failures, concurrent read caching, explicit partial failures, all-failed and malformed responses, successful empty results, and cancellation.
- Untrusted page instructions remaining evidence data, not an additional instruction channel. This tests data handling, not a guarantee that a model can never follow prompt injection.
- The real `AIAgents` pipeline: strict typed research responses, Encyclopedia medium reasoning, only `read_commentary_source`, two tool rounds followed by tool-free synthesis, and complete Responses continuation history, including intermediate assistant commentary before the final JSON.
- Encyclopedia source footers preserve original inline links and survive Agent Q omitting citations, including when Encyclopedia abstains; uncited retrieved sources are excluded, empty citations have an explicit message, and analyses without Encyclopedia have no source footer.
- Citation extraction handles inline/reference/autolinks and links inside tables or definitions, excludes code and image alternative text, matches the retrieval registry, and preserves first-citation order. Footer escaping keeps hostile titles literal and preserves query values and URL delimiters.
- Exact typed probabilities independent of Markdown headings, table layout, subjective ranges, rounded percentages, or inequality displays; full numeric precision and probability boundaries; ML inputs taken directly from model averages even when its generated prose disagrees.
- Display-ready input percentages with exactly two decimal places, including trailing zeros and inequality bounds, while the calculation retains the original numeric precision.
- Server-calculated Luce point estimates, bounds, and sensitivity values supplied to exactly one tool-free Agent Q request with high reasoning effort.
- Two-/three-agent overlap and selection ordering, wait-all failure behavior and MCP cleanup, explicit pair-level abstentions, rejection of malformed/missing/string/out-of-range probability fields without Markdown fallback, and unchanged single-/multiple-agent Markdown string responses.

Run only the model packaging checks without starting the loopback MCP fixture:

```sh
dotnet run --project tests/BaseballAIWorkbench.ResearchChecks/BaseballAIWorkbench.ResearchChecks.csproj -- --models-only
```

Run only the citation extraction and footer checks, using in-memory search results without model loading or loopback servers:

```sh
dotnet run --project tests/BaseballAIWorkbench.ResearchChecks/BaseballAIWorkbench.ResearchChecks.csproj -- --citations-only
```

To check a fresh publish output independently of the checkout, publish this executable to a new directory, then run its DLL from an unrelated working directory:

```sh
model_check_publish="$(mktemp -d)"
dotnet publish tests/BaseballAIWorkbench.ResearchChecks/BaseballAIWorkbench.ResearchChecks.csproj -c Release -o "$model_check_publish"
cd /tmp
dotnet "$model_check_publish/BaseballAIWorkbench.ResearchChecks.dll" --models-only
```

Model checks use `Models` beside the executable, including files copied from the API project reference. They require no source tree, application configuration, or credentials.

## Offline telemetry and optional dashboard export

Run only the telemetry checks:

```sh
dotnet run --project tests/BaseballAIWorkbench.ResearchChecks/BaseballAIWorkbench.ResearchChecks.csproj -- --telemetry-only
```

The checks use the production `AddServiceDefaults` configuration and attach in-memory SDK exporters. They explicitly disable the configured OTLP endpoint by default, including when the shell inherits Aspire settings. No cloud service or dashboard is contacted. Each successful scripted model response reports 10 input and 10 output tokens. The fixtures verify single-call totals of 10/10, a three-call research total of 30/30, and concurrent research plus Agent Q totals of 60/60. Failed, canceled, and missing-usage calls contribute no token observations; duration metrics still count these calls. Both structured and unstructured runners are tested on failure and cancellation. The token assertions run after the invocation's clients and telemetry wrappers have been disposed; a meter listener and completed spans verify resource cleanup, and a second invocation confirms the shared OpenAI client remains usable. Privacy assertions cover agent, chat, and tool spans.

To send these same synthetic fixtures to a running **local** Aspire dashboard, explicitly opt in and supply that dashboard's OTLP endpoint and protocol. For example, using an unauthenticated loopback gRPC receiver:

```sh
OTEL_EXPORTER_OTLP_ENDPOINT=http://127.0.0.1:19889 \
OTEL_EXPORTER_OTLP_PROTOCOL=grpc \
dotnet run --project tests/BaseballAIWorkbench.ResearchChecks/BaseballAIWorkbench.ResearchChecks.csproj -- --telemetry-export
```

Use the receiver port from your dashboard; its browser/UI port is different. If the receiver requires authentication, configure its OTLP headers in your environment. The export mode rejects non-loopback endpoints, including per-signal endpoint overrides, and still uses only fixture model responses and the local MCP server. It runs the telemetry suite alone and flushes telemetry before exiting.

In Aspire, select **BaseballAIWorkbench.TelemetryChecks**. Traces are grouped under `telemetry-fixture.*`, with `invoke_agent` spans from `BaseballAIWorkbench.Agents` and child `chat fake-deployment` spans from `BaseballAIWorkbench.AI`. Under Metrics, select `gen_ai.client.token.usage` from the chat meter and filter `gen_ai.token.type` to `input` or `output`. Aspire 13.6's graph shows P50/P90/P99 and a **Show count** option, rather than a total-token selector. In exported metrics, histogram **sum** is the token total; histogram **count** is the number of usage-bearing calls. The GenAI trace details also show tokens for each call or agent invocation. One fixture run produces 100 input and 100 output tokens across 10 usage-bearing calls. Duration metrics and traces can also cover calls that do not return usage. The agent meter is intentionally excluded so its aggregate usage cannot double-count the model calls.

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
