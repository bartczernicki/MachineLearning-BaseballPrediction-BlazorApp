**Baseball Machine Learning Workbench**
is a web application that showcases performing decision analysis (decision thresholding, what-if analysis) using in-memory Machine Learning models with baseball data.

**Live Demo Web Site:** https://aka.ms/BaseballMLWorkbench  
**DockerHub Container Location:** https://hub.docker.com/r/bartczernicki/baseballmachinelearningworkbench  
**Full Get Started Guide:** https://github.com/bartczernicki/MachineLearning-BaseballPrediction-BlazorApp/blob/master/GETSTARTED.md

![Baseball ML Workbench](https://github.com/bartczernicki/MachineLearning-BaseballPrediction-BlazorApp/blob/c45c8129aec7d88539687807fd614c17f719406a/BaseballMLWorkbenchDemo.gif)

**The application has the following features:**
* Three different decision analysis mechanisms that perform what-if analysis
* A simple "expert" rules engine to predict baseball hall of fame induction, contrasted with Machine Intelligence
* Single and multiple machine learning models working together to predict baseball hall of fame ballot and induction metrics
* Machine Learning models are surfaced via ML.NET in-memory for very quick inference (predictions)
* Surfaced via the Server-Side Blazor .NET Core web application framework using SignalR to deliver the predictions from the server to the web client at scale
* Self-contained application in a Docker container on DockerHub, allowing you to run it completely offline or locally

**Architecture - Cloud Deployment Diagram:**
![Baseball ML Workbench - Architecture Deployment Diagram](https://github.com/bartczernicki/MachineLearning-BaseballPrediction-BlazorApp/blob/master/BaseballMLWorkbench-Architecture-DeploymentDiagram.png)

**Project Structure (Tested):**
* Visual Studio 2019 v4.0 for Windows or Visual Studio 2019 for Mac (8.6), .NET Core 3.1.x, Server-Side Blazor, ML.NET v1.4, Azure SignalR (optional for massively scaling message communication for Azure deployments)

**More Information:**
* ML.NET: https://dotnet.microsoft.com/apps/machinelearning-ai/ml-dotnet
* Blazor: https://dotnet.microsoft.com/apps/aspnet/web-apps/blazor
* Historical Baseball Statistics Database (used as the model training and inference data set): http://www.seanlahman.com/baseball-archive/statistics/
* How to Measure Anything (Amazon book link): https://www.amazon.com/How-Measure-Anything-Intangibles-Business-ebook/dp/B00INUYS2U/ref=sr_1_1?dchild=1&keywords=how+to+measure+anything&qid=1588713606&sr=8-1
* Decision Management Systems (Amazon book link): https://www.amazon.com/Decision-Management-Systems-Practical-Predictive/dp/0132884380

