# AppHost configuration and deployment decisions

Core hosting, telemetry, and Azure deployment decisions for Baseball AI Workbench. The AppHost configuration is defined in [Program.cs](Program.cs), with Front Door customization in [FrontDoorConfiguration.cs](FrontDoorConfiguration.cs). Run the commands below from the repository root.

## AI telemetry in Aspire

After running an analysis with `aspire run`, open **Metrics → apiservice → BaseballAIWorkbench.AI** in the dashboard. `gen_ai.client.token.usage` records provider-reported input/output tokens; filter by `gen_ai.token.type`. Aspire 13.6's graph shows percentiles and an optional observation count. In the exported histogram, **sum** measures tokens consumed, while **count** measures observations. `gen_ai.client.operation.duration` measures model-call time.

Under **Traces**, each named agent span contains its model-call spans. Agent traces use `BaseballAIWorkbench.Agents`; only the model-call meter is collected, so aggregate agent usage is not counted again. AI telemetry excludes prompt/response bodies and tool arguments/results.

See the [offline telemetry checks and dashboard export instructions](../../../tests/BaseballAIWorkbench.ResearchChecks/README.md) to verify this without paid model calls. Production uses the same instrumentation when an OTLP endpoint is configured.

## Azure Container Apps sizing

The API and web frontend each use **1 vCPU and 2 GiB RAM per running replica** on the Consumption workload profile, which requires this CPU/memory pairing. Autoscaling changes the replica count, with **minimum replicas 0**, the existing maximum of **10**, and a **3,600-second (one-hour) cooldown** before the final replica scales to zero; actual timing depends on the scaler. These settings take effect on the next `aspire deploy --non-interactive`; local `aspire run` is unchanged.

## Azure Front Door deployments

The AppHost adds Azure Front Door Standard in publish mode using `Aspire.Hosting.Azure.FrontDoor` **13.6.0-preview.1.26479.8**. One origin serves `webfrontend` through its public Azure Container Apps endpoint; the Web app continues calling the API directly. Local `aspire run` does not provision Front Door. After the next `aspire deploy`, use the generated HTTPS `*.azurefd.net` URL labeled **Web frontend (Front Door)** in the deployment summary. A custom domain, WAF policy, or Private Link requires separate configuration.

The endpoint resource name is fixed to **`BaseballAIWorkBench`**, giving a URL shaped like `https://BaseballAIWorkBench-<hash>.<zone>.azurefd.net`. Azure assigns the hash and zone suffix; the deployment summary reads the actual returned hostname rather than constructing it. See [Front Door endpoint naming](https://learn.microsoft.com/en-us/azure/frontdoor/how-to-configure-endpoints).

Changing the endpoint name creates a new endpoint and route on the next deployment. Aspire uses incremental deployment, so the existing `webfrontendEndpoint-3yxogsx7cloqo` endpoint and its route remain available as a fallback, sharing the existing origin group and cache rules. The old URL continues serving the application without redirecting to the new URL. The old endpoint and route remain outside the updated template; no deletion is included in this change. See [incremental deployment behavior](https://learn.microsoft.com/en-us/azure/azure-resource-manager/templates/deployment-modes#incremental-mode).

HTTP redirects to HTTPS, and Front Door forwards to the origin over HTTPS with a **240-second origin response timeout**. The default route has caching disabled. Two rules enable caching only for GET requests matching the static path and file-extension allowlists: framework/package assets, libraries, images, and application CSS/favicon files. The extension allowlist is split into ten script/style/image extensions and two font extensions because Azure permits at most **10 match values per condition**. Both rules preserve query strings in the cache key, enable compression, honor origin cache lifetimes, and use a one-hour lifetime only when the origin omits one. `no-cache`, `private`, and `no-store` responses remain uncached. Rendered pages, API/data responses, health endpoints, and Blazor/SignalR negotiation remain uncached. See [Front Door caching behavior](https://learn.microsoft.com/en-us/azure/frontdoor/front-door-caching).

Static references use ASP.NET Core's fingerprinted asset URLs where available, so updated files receive new URLs on deployment. The existing Blazored CSS and JavaScript assets retain their origin `no-cache` behavior. Use the URLs emitted by the page when checking caching; an unversioned asset URL can intentionally remain uncached.

Both Container Apps retain **minimum replicas 0** and a **3,600-second cooldown** before the final replica can scale to zero; actual timing depends on the scaler. Front Door health probes are disabled because there is only one origin: they provide no failover target and can keep an idle app running. See [single-origin probe guidance](https://learn.microsoft.com/en-us/azure/frontdoor/best-practices#disable-health-probes-when-theres-only-one-origin-in-an-origin-group). Cache misses and interactive requests can still incur cold starts. Scaling down loses in-memory Blazor circuit state, so returning users may need to reload. After negotiation, browsers connect directly to Azure SignalR; edge caching does not preserve Blazor sessions. Regional failover requires an additional application deployment and its dependencies in another region.

Before deploying, generate the deployment artifacts and compile the environment and individual app templates from the repository root. Inspect the generated Front Door rules to confirm each condition has at most 10 match values; Bicep compilation alone does not enforce this service limit:

```bash
aspire publish --apphost src/BaseballAIWorkbench/BaseballAIWorkbench.AppHost/BaseballAIWorkbench.AppHost.csproj --output-path /tmp/baseball-frontdoor-check --non-interactive
az bicep build --file /tmp/baseball-frontdoor-check/main.bicep
az bicep build --file /tmp/baseball-frontdoor-check/apiservice/apiservice.bicep
az bicep build --file /tmp/baseball-frontdoor-check/webfrontend/webfrontend.bicep
```

Publishing and compiling these artifacts does not deploy resources. The next `aspire deploy --non-interactive` applies the configuration to Azure.

After a future deployment, validate and monitor the endpoint:

1. After deployment and propagation, confirm the summary shows the new `BaseballAIWorkBench-...azurefd.net` URL and that both it and the existing fallback URL work. Navigate all scenarios and exercise a what-if slider. In browser Network tools, confirm uncached page/negotiation responses and the direct Azure SignalR connection. This check does not require running paid AI analysis.
2. Copy a fingerprinted static asset path from the page and repeat a **GET** request, for example `curl --compressed -sS -D - -o /dev/null 'https://<front-door-host>/<fingerprinted-asset-path>'`. Inspect `Cache-Control`, `X-Cache`, and `Age` when present. Eligible assets should produce `TCP_HIT` or `TCP_REMOTE_HIT` after warming; individual edge locations have separate caches. Check the homepage and Blazored asset separately to confirm they do not become cache hits. A HEAD request does not validate GET caching.
3. Use Azure Monitor to follow Front Door cache hit ratio, origin latency, request/error rates, and Container Apps replica counts. [Front Door access logs](https://learn.microsoft.com/en-us/azure/frontdoor/monitor-front-door?pivots=front-door-standard-premium) can explain cache misses and origin timeouts when diagnostic logging is enabled. After stopping test traffic, observe replica metrics through the idle cooldown, then verify that reopening the app recovers from a cold start. Repeated requests to the application during this check can keep it active.
