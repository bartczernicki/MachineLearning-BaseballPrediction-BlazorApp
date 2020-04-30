## **Get Started with the Baseball Machine Learning Workbench**  
There are multiple ways to get started with the Baseball Machine Learning Workbench, from non-technical users to professional software developers.

[Live Demo Site](#LiveDemoSite)
[Run Docker Locally In your environment](#RunDockerLocally)
[Publish Docker Container to the Azure Cloud](#PublishDockerContainerToAzure)



### 1. <a name="LiveDemoSite"></a>Live Demo Site
The easiest way without performing any technical tasks is to simply use the live demo application located here:  
**Live Demo Web Site Address:** https://aka.ms/BaseballMLWorkbench  
A getting started guide is provided inside the workbench.

### 2. <a name="RunDockerLocally"></a>Run the Docker Container locally in your own environment
The complete Baseball Machine Learning Workbench is containerized using Docker.  This allows you to run the entire web application locally by using Docker.  Linux containers are used.  To get started:  
* Ensure you have Docker Engine installed on your Windows, macOS or Linux system.  More information located here: https://docs.docker.com/engine/install/  
* Using a command terminal, pull down the Baseball Machine Learning Workbench Docker Container using this command (this will download the docker container locally):  
<code>docker pull bartczernicki/baseballmachinelearningworkbench</code>  
* Verify the Docker image has been pulled down locally, by running this command:  
<code>docker images bartczernicki/baseballmachinelearningworkbench</code>  
* Using a command terminal, run the the Baseball Machine Learning Workbench Docker Container using this command:  
<code>docker run -it --rm -p 8080:80 --name baseballmachinelearningworkbench bartczernicki/baseballmachinelearningworkbench:latest</code>  
* Once you have verified your Docker container is running, using your browser navigate to:  http://localhost:8080

### 3. <a name="PublishDockerContainerToAzure"></a>Publish Docker Container to the Azure Cloud using Azure Container Instances  
Since the Baseball Machine Learning Workbench is containerized and published on a public DockerHub repository, you can easily use run the container on Azure.  Azuer Container Instances (ACI) allow you to run Docker containers in your own Azure environment.  You can do sophisticated deployments using SSL, Application Gateways, VNETs etc.  Below is a simple way to get started to publish the Baseball Machine Learning Workbench using ACI  
* Navigate to Azure Portal:  https://portal.azure.com  
* Select **Create a Resource** from the top navigation menu  
* Type in **Azure Container Instance** inside Azure Marketplace  
* Select the **Container Instances** Marketplace offer; and select **Create** on the next screen  
* Fill out the **Resource group** information.  You can name the **Container name** anything you like; select a **Region**
* Select the **Image Source: DockerHub or other registry** option.  The **Image** name is **bartczernicki/baseballmachinelearningworkbench:latest**.  The **OS Type** is **Linux**
* You can change the size of the image if you like.   Below is a screenshot of how this should look like filled out.
<p align="center">
  <img width=500 height=250 src="https://github.com/bartczernicki/MachineLearning-BaseballPrediction-BlazorApp/blob/master/Images/CreateAzureContainerInstance.png">
</p>  

* Select __Next: Networking__ to fill out the Network options.
* Provide a __DNS name label__ name.  This will allow you to access the workbench via a FQDN rather than an IP address.  Note:  This is a public DNS name scoped to the deployment region, so your FQDN will have to be unique.  The screenshot below should show an example of how this will look like. 
<p align="center">
  <img width=500 height=250 src="https://github.com/bartczernicki/MachineLearning-BaseballPrediction-BlazorApp/blob/master/Images/AzureContainerInstanceNetworking.png">
</p>
  
* There are more advanced options that could be set, however for now the deployment is ready.  Select __Review + Create__.  Your deployment will be validated, if it has been filled out correctly click **Create**.  It will take a few seconds to deploy the Baseball Machine Learning Workbench container to Azure.
* After your deployment succeeds, you can select __Go to resource__, where you will have the IP address and the FQDN (DNS) name where you can access the web application. Using your browser, access the application using either the IP address of FQDN. The screenshot below shows where you can find the IP & FQDN in your ACI resource deployment:  
<p align="center">
  <img width=700 height=150 src="https://github.com/bartczernicki/MachineLearning-BaseballPrediction-BlazorApp/blob/master/Images/ACIDeployment.png">
</p>
