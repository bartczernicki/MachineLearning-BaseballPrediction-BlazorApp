**Scenario Overview**  
 Solutions where human judgement and decision-making is involved. Sufficiently important decisions require deeper analysis. If a quantitative approach (doing the math) is available, it is usually the one preferred as it will offer the best combination of an approach and outcome.

![Sports Decision Scenario](https://raw.githubusercontent.com/bartczernicki/MachineLearning-BaseballPrediction-BlazorApp/refs/heads/master/SportsDecisionScenario.png) 

**Baseball AI Workbench**   
is a distributed web application that showcases performing quantitative decision analysis (decision thresholding, what-if analysis, AI Agent retrieval with probability & sensitivity interval analysis) using in-memory Machine Learning models with historical baseball data.

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
* Visual Studio 2026 or VS Code, .NET 10.x, Server-Side Blazor, ML.NET v5.x 
* .NET Aspire 13.6 for Distributed Computed Hosting 
* Microsoft Agent Framework (Agents, Agent Orchestration & Retrieval) 
* Web IQ for Agentic Real-Time Intelligence Research 
* Microsoft AI Foundry (Model Hosting), Azure OpenAI (models)
* Azure SignalR (Scaling message communication)
* Azure Front Door (global web delivery scale)

See the [AppHost configuration and deployment decisions](src/BaseballAIWorkbench/BaseballAIWorkbench.AppHost/README.MD) for AI telemetry, Container Apps sizing and scaling, and Front Door configuration and validation.

**More Information of Key Components:**
* Decision Intelligence: https://www.decisionintelligencebook.ai/
* Historical Baseball Statistics Database (used as the model training and inference data set): http://www.seanlahman.com/baseball-archive/statistics/
* Microsoft Agent Framework: https://github.com/microsoft/agent-framework/
* MEAI: https://learn.microsoft.com/en-us/dotnet/ai/microsoft-extensions-ai/
* Aspire: https://aspire.dev/
* ML.NET: https://dotnet.microsoft.com/apps/machinelearning-ai/ml-dotnet
* Blazor: https://dotnet.microsoft.com/apps/aspnet/web-apps/blazor
* Web IQ: https://webiq.microsoft.ai/