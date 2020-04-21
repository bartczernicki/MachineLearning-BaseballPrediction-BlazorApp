**Baseball Machine Learning Workbench**
is a web application that showcases performing decision analysis (decision thresholding, what-if analysis) using in-memory Machine Learning models with baseball data.

**Live Demo Web Site:** https://aka.ms/BaseballMLWorkbench  
**DockerHub Container Location:** https://hub.docker.com/r/bartczernicki/baseballmlworkbench

![Baseball ML Workbench](https://github.com/bartczernicki/MachineLearning-BaseballPrediction-BlazorApp/blob/c45c8129aec7d88539687807fd614c17f719406a/BaseballMLWorkbenchDemo.gif)

**The application has the following features:**
* Three different decision analysis mechanisms performing what-if analysis
* A simple rules engine to predict baseball hall of fame induction, contrasted with Machine Intelligence
* Single and multiple machine learning models working together to predict baseball hall of fame ballot and induction
* Machine Learning models are surfaced via ML.NET in-memory for very quick inference (predictions)
* Surfaced via the Server-Side Blazor .NET Core web application framework using SignalR to deliver the predictions from the server to the web client at scale

**Architecture - Cloud Deployment Diagram:**
![Baseball ML Workbench - Architecture Deployment Diagram](https://github.com/bartczernicki/MachineLearning-BaseballPrediction-BlazorApp/blob/master/BaseballMLWorkbench-Architecture-DeploymentDiagram.png)

**Project Structure (Tested):**
* Visual Studio 2019 v4.0 for Windows or Visual Studio 2019 for Mac (8.6), .NET Core 3.1.x, Server-Side Blazor, ML.NET v1.4, Azure SignalR (optional for massively scaling message communication for Azure deployments)

**More Information:**
* ML.NET: https://dotnet.microsoft.com/apps/machinelearning-ai/ml-dotnet
* Blazor: https://dotnet.microsoft.com/apps/aspnet/web-apps/blazor
* Historical Baseball Statistics Database: http://www.seanlahman.com/baseball-archive/statistics/
* Decision Management Systems (Amazon book): https://www.amazon.com/Decision-Management-Systems-Practical-Predictive/dp/0132884380

