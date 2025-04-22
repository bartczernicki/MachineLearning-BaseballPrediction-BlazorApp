**Baseball AI Workbench**
is a web application that showcases performing quantitative decision analysis (decision thresholding, what-if analysis, AI Agents with probability & confidence interval analysis) using in-memory Machine Learning models with historical baseball data.

**Live Demo Web Site:** https://baseballmlworkbench.azurefd.net/  
**AI Architecture Details:** https://docs.microsoft.com/en-us/azure/architecture/data-guide/big-data/baseball-ml-workload  
**DockerHub Container Location:** https://hub.docker.com/r/bartczernicki/baseballmachinelearningworkbench   
**Full Get Started Guide:** https://github.com/bartczernicki/MachineLearning-BaseballPrediction-BlazorApp/blob/master/GETSTARTED.md  

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

**More Information:**
* ML.NET: https://dotnet.microsoft.com/apps/machinelearning-ai/ml-dotnet
* Blazor: https://dotnet.microsoft.com/apps/aspnet/web-apps/blazor
* Historical Baseball Statistics Database (used as the model training and inference data set): http://www.seanlahman.com/baseball-archive/statistics/
* How to Measure Anything (Amazon book link): https://www.amazon.com/How-Measure-Anything-Intangibles-Business-ebook/dp/B00INUYS2U/ref=sr_1_1?dchild=1&keywords=how+to+measure+anything&qid=1588713606&sr=8-1
* Decision Management Systems (Amazon book link): https://www.amazon.com/Decision-Management-Systems-Practical-Predictive/dp/0132884380

