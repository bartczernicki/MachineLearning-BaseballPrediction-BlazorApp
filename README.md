**Scenario Unlocked**
unlocks solutions where human judgement and decision-making is involved. Sufficiently important decisions require deeper analysis. If a quantitative approach is available, it is usually the one preferred as it will offer the best combination of an approach and outcome.

![Sports Decision Scenario](https://raw.githubusercontent.com/bartczernicki/MachineLearning-BaseballPrediction-BlazorApp/refs/heads/master/SportsDecisionScenario.png) 

**Baseball AI Workbench**
is a web application that showcases performing quantitative decision analysis (decision thresholding, what-if analysis, AI Agents with probability & confidence interval analysis) using in-memory Machine Learning models with historical baseball data.

![Baseball ML Workbench](https://raw.githubusercontent.com/bartczernicki/MachineLearning-BaseballPrediction-BlazorApp/refs/heads/master/BaseballAIWorkbench.png)





**The application has the following features:**
* Historical position player (batters) up to the end of the 2024 season 
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
* Visual Studio 2022, .NET 9, Server-Side Blazor, ML.NET v4.02, Semantic Kernel, Azure AI Foundy, Azure OpenAI Azure SignalR (optional for massively scaling message communication for Azure deployments)

**AI telemetry in Aspire:**

After running an analysis with `aspire run`, open **Metrics → apiservice → BaseballAIWorkbench.AI** in the dashboard. `gen_ai.client.token.usage` records provider-reported input/output tokens; filter by `gen_ai.token.type`. Aspire 13.6's graph shows percentiles and an optional observation count. In the exported histogram, **sum** measures tokens consumed, while **count** measures observations. `gen_ai.client.operation.duration` measures model-call time.

Under **Traces**, each named agent span contains its model-call spans. Agent traces use `BaseballAIWorkbench.Agents`; only the model-call meter is collected, so aggregate agent usage is not counted again. AI telemetry excludes prompt/response bodies and tool arguments/results.

See the [offline telemetry checks and dashboard export instructions](tests/BaseballAIWorkbench.ResearchChecks/README.md) to verify this without paid model calls. Production uses the same instrumentation when an OTLP endpoint is configured.

**More Information:**
* ML.NET: https://dotnet.microsoft.com/apps/machinelearning-ai/ml-dotnet
* Blazor: https://dotnet.microsoft.com/apps/aspnet/web-apps/blazor
* Historical Baseball Statistics Database (used as the model training and inference data set): http://www.seanlahman.com/baseball-archive/statistics/
* How to Measure Anything (Amazon book link): https://www.amazon.com/How-Measure-Anything-Intangibles-Business-ebook/dp/B00INUYS2U/ref=sr_1_1?dchild=1&keywords=how+to+measure+anything&qid=1588713606&sr=8-1
* Decision Management Systems (Amazon book link): https://www.amazon.com/Decision-Management-Systems-Practical-Predictive/dp/0132884380
