## **Get Started with the Baseball Machine Learning Workbench**  
There are multiple ways to get started with the Baseball Machine Learning Workbench, from non-technical users to professional software developers.

### 1. Live Demo Site  
The easiest way without performing any technical tasks is to simply use the live demo application located here:  
**Live Demo Web Site:** https://aka.ms/BaseballMLWorkbench  
A getting started guide is provided inside the workbench.

### 2. Run the Docker Container locally in your own environment
The complete Baseball Machine Learning Workbench is containerized using Docker.  This allows you to run the entire web application locally by using Docker.  Linux containers are used.  To get started:  
* Ensure you have Docker Engine installed on your Windows, macOS or Linux system.  More information located here: https://docs.docker.com/engine/install/  
* Using a command terminal, pull down the Baseball Machine Learning Workbench Docker Container using this command (this will download the docker container locally):  
<code>docker pull bartczernicki/baseballmachinelearningworkbench</code>  
* Verify the Docker image has been pulled down locally, by running this command:  
<code>docker images bartczernicki/baseballmachinelearningworkbench</code>  
* Using a command terminal, run the the Baseball Machine Learning Workbench Docker Container using this command:  
<code>docker run -it --rm -p 8080:80 --name baseballmachinelearningworkbench bartczernicki/baseballmachinelearningworkbench:latest</code>  
* Once you have verified your Docker container is running, using your browser navigate to:  http://localhost:8080

