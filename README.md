**Scenario Unlocked**
unlocks solutions where human judgement and decision-making is involved. Sufficiently important decisions require deeper analysis. If a quantitative approach is available, it is usually the one preferred as it will offer the best combination of an approach and outcome.

![Sports Decision Scenario](https://raw.githubusercontent.com/bartczernicki/MachineLearning-BaseballPrediction-BlazorApp/refs/heads/master/SportsDecisionScenario.png) 

**Baseball AI Workbench**
is a web application that showcases performing quantitative decision analysis (decision thresholding, what-if analysis, AI Agents with probability & confidence interval analysis) using in-memory Machine Learning models with historical baseball data.

![Baseball ML Workbench](https://raw.githubusercontent.com/bartczernicki/MachineLearning-BaseballPrediction-BlazorApp/refs/heads/master/BaseballAIWorkbench.png)





**The application has the following features:**
* Historical position player (batters) up to the end of the 2025 season 
* Three different decision analysis mechanisms to perform what-if analysis
* Agentic AI integrations with Agents performing research & quantitative analysis 
* A simple "expert" rules engine to predict baseball hall of fame induction, contrasted with a Machine Intelligence solution
* Single and multiple machine learning models working together to predict baseball hall of fame ballot and induction probabilities
* Machine Learning models are surfaced via ML.NET in-memory for rapid inference (predictions)
* Surfaced via the Aspire.NET integration with a Blazor application framework using SignalR to deliver the predictions from the server to the web client at scale
* Self-contained application with Docker, allowing you to run locally

**Architecture - Cloud Deployment Diagram:**
![Baseball ML Workbench - Architecture Deployment Diagram](https://github.com/bartczernicki/MachineLearning-BaseballPrediction-BlazorApp/blob/master/BaseballMLWorkbench-Architecture-DeploymentDiagram.png)

**Project Structure (Verified):**
* Visual Studio 2026, .NET 10, Server-Side Blazor, ML.NET v5.x, Microsoft Agent Frameworks, Microsoft AI Foundry, Azure OpenAI, Azure SignalR (optional for massively scaling message communication for Azure deployments)

**AI telemetry in Aspire:**

After running an analysis with `aspire run`, open **Metrics → apiservice → BaseballAIWorkbench.AI** in the dashboard. `gen_ai.client.token.usage` records provider-reported input/output tokens; filter by `gen_ai.token.type`. Aspire 13.6's graph shows percentiles and an optional observation count. In the exported histogram, **sum** measures tokens consumed, while **count** measures observations. `gen_ai.client.operation.duration` measures model-call time.

Under **Traces**, each named agent span contains its model-call spans. Agent traces use `BaseballAIWorkbench.Agents`; only the model-call meter is collected, so aggregate agent usage is not counted again. AI telemetry excludes prompt/response bodies and tool arguments/results.

See the [offline telemetry checks and dashboard export instructions](tests/BaseballAIWorkbench.ResearchChecks/README.md) to verify this without paid model calls. Production uses the same instrumentation when an OTLP endpoint is configured.

**Azure Front Door deployments:**

The AppHost adds Azure Front Door Standard in publish mode using `Aspire.Hosting.Azure.FrontDoor` **13.6.0-preview.1.26479.8**. One origin serves `webfrontend` through its public Azure Container Apps endpoint; the Web app continues calling the API directly. Local `aspire run` does not provision Front Door. After the next `aspire deploy`, use the generated HTTPS `*.azurefd.net` URL labeled **Web frontend (Front Door)** in the deployment summary. A custom domain, WAF policy, or Private Link requires separate configuration.

HTTP redirects to HTTPS, and Front Door forwards to the origin over HTTPS with a **240-second origin response timeout**. The default route has caching disabled. Two rules enable caching only for GET requests matching the static path and file-extension allowlists: framework/package assets, libraries, images, and application CSS/favicon files. The extension allowlist is split into ten script/style/image extensions and two font extensions because Azure permits at most **10 match values per condition**. Both rules preserve query strings in the cache key, enable compression, honor origin cache lifetimes, and use a one-hour lifetime only when the origin omits one. `no-cache`, `private`, and `no-store` responses remain uncached. Rendered pages, API/data responses, health endpoints, and Blazor/SignalR negotiation remain uncached. See [Front Door caching behavior](https://learn.microsoft.com/en-us/azure/frontdoor/front-door-caching).

Static references use ASP.NET Core's fingerprinted asset URLs where available, so updated files receive new URLs on deployment. The existing Blazored CSS and JavaScript assets retain their origin `no-cache` behavior. Use the URLs emitted by the page when checking caching; an unversioned asset URL can intentionally remain uncached.

Both Container Apps retain **minimum replicas 0** and a **3,600-second cooldown** before the final replica can scale to zero; actual timing depends on the scaler. Front Door health probes are disabled because there is only one origin: they provide no failover target and can keep an idle app running. See [single-origin probe guidance](https://learn.microsoft.com/en-us/azure/frontdoor/best-practices#disable-health-probes-when-theres-only-one-origin-in-an-origin-group). Cache misses and interactive requests can still incur cold starts. Scaling down loses in-memory Blazor circuit state, so returning users may need to reload. After negotiation, browsers connect directly to Azure SignalR; edge caching does not preserve Blazor sessions. Regional failover requires an additional application deployment and its dependencies in another region.

As of October 1, 2026, the Standard base list price is **USD $35/month, billed hourly**, plus request and data-transfer charges. The Front Door base fee continues while the profile exists, including when the Container Apps have scaled to zero. Check [current Azure Front Door pricing](https://azure.microsoft.com/en-us/pricing/details/frontdoor/) before deploying.

Before deploying, generate and compile the ARM template from the repository root. Inspect the generated Front Door rules to confirm each condition has at most 10 match values; Bicep compilation alone does not enforce this service limit:

```bash
aspire publish --apphost src/BaseballAIWorkbench/BaseballAIWorkbench.AppHost/BaseballAIWorkbench.AppHost.csproj --output-path /tmp/baseball-frontdoor-check --non-interactive
az bicep build --file /tmp/baseball-frontdoor-check/main.bicep
```

After a future deployment, validate and monitor the endpoint:

1. Open the generated Front Door URL, navigate all scenarios, and exercise a what-if slider. In browser Network tools, confirm uncached page/negotiation responses and the direct Azure SignalR connection. This check does not require running paid AI analysis.
2. Copy a fingerprinted static asset path from the page and repeat a **GET** request, for example `curl --compressed -sS -D - -o /dev/null 'https://<front-door-host>/<fingerprinted-asset-path>'`. Inspect `Cache-Control`, `X-Cache`, and `Age` when present. Eligible assets should produce `TCP_HIT` or `TCP_REMOTE_HIT` after warming; individual edge locations have separate caches. Check the homepage and Blazored asset separately to confirm they do not become cache hits. A HEAD request does not validate GET caching.
3. Use Azure Monitor to follow Front Door cache hit ratio, origin latency, request/error rates, and Container Apps replica counts. [Front Door access logs](https://learn.microsoft.com/en-us/azure/frontdoor/monitor-front-door?pivots=front-door-standard-premium) can explain cache misses and origin timeouts when diagnostic logging is enabled. After stopping test traffic, observe replica metrics through the idle cooldown, then verify that reopening the app recovers from a cold start. Repeated requests to the application during this check can keep it active.

**More Information:**
* ML.NET: https://dotnet.microsoft.com/apps/machinelearning-ai/ml-dotnet
* Blazor: https://dotnet.microsoft.com/apps/aspnet/web-apps/blazor
* Historical Baseball Statistics Database (used as the model training and inference data set): http://www.seanlahman.com/baseball-archive/statistics/
* How to Measure Anything (Amazon book link): https://www.amazon.com/How-Measure-Anything-Intangibles-Business-ebook/dp/B00INUYS2U/ref=sr_1_1?dchild=1&keywords=how+to+measure+anything&qid=1588713606&sr=8-1
* Decision Management Systems (Amazon book link): https://www.amazon.com/Decision-Management-Systems-Practical-Predictive/dp/0132884380
