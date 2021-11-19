**Baseball Machine Learning Workbench**
is a web application that showcases performing decision analysis (decision thresholding, what-if analysis) using in-memory Machine Learning models with baseball data.

**Live Demo Web Site:** https://aka.ms/BaseballMLWorkbench  
**AI Architecture Details:** https://docs.microsoft.com/en-us/azure/architecture/data-guide/big-data/baseball-ml-workload  
**DockerHub Container Location:** https://hub.docker.com/r/bartczernicki/baseballmachinelearningworkbench  
**Live Demo (Docker container hosted on Azure Container Instances):** http://baseballmachinelearningworkbench.eastus2.azurecontainer.io  
**Full Get Started Guide:** https://github.com/bartczernicki/MachineLearning-BaseballPrediction-BlazorApp/blob/master/GETSTARTED.md  

![Baseball ML Workbench](https://github.com/bartczernicki/MachineLearning-BaseballPrediction-BlazorApp/blob/master/BaseballMLWorkbenchDemo.gif)

**The application has the following features:**
* Three different decision analysis mechanisms to perform what-if analysis
* A simple "expert" rules engine to predict baseball hall of fame induction, contrasted with a Machine Intelligence solution
* Single and multiple machine learning models working together to predict baseball hall of fame ballot and induction probabilities
* Machine Learning models are surfaced via ML.NET in-memory for rapid inference (predictions)
* Surfaced via the Server-Side Blazor .NET Core web application framework using SignalR to deliver the predictions from the server to the web client at scale
* Self-contained application in a Docker container on DockerHub, allowing you to run it completely offline or locally

**Architecture - Cloud Deployment Diagram:**
![Baseball ML Workbench - Architecture Deployment Diagram](https://github.com/bartczernicki/MachineLearning-BaseballPrediction-BlazorApp/blob/master/BaseballMLWorkbench-Architecture-DeploymentDiagram.png)

**Project Structure (Verified):**
* Visual Studio 2019 v4.0 for Windows/Mac - Visual Studio 2022, .NET Core 3.x - .NET 6, Server-Side Blazor, ML.NET v1.5 - v1.7, Azure SignalR (optional for massively scaling message communication for Azure deployments)
* Note: Updated Azure service versions or NuGet package references could work

**More Information:**
* ML.NET: https://dotnet.microsoft.com/apps/machinelearning-ai/ml-dotnet
* Blazor: https://dotnet.microsoft.com/apps/aspnet/web-apps/blazor
* Historical Baseball Statistics Database (used as the model training and inference data set): http://www.seanlahman.com/baseball-archive/statistics/
* How to Measure Anything (Amazon book link): https://www.amazon.com/How-Measure-Anything-Intangibles-Business-ebook/dp/B00INUYS2U/ref=sr_1_1?dchild=1&keywords=how+to+measure+anything&qid=1588713606&sr=8-1
* Decision Management Systems (Amazon book link): https://www.amazon.com/Decision-Management-Systems-Practical-Predictive/dp/0132884380

