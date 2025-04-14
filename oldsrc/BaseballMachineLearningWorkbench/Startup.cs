using BaseballMachineLearningWorkbench.Services;
using BaseballMachineLearningWorkbench.MachineLearning;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.ML;
using System.IO;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Hosting.Internal;

namespace BaseballMachineLearningWorkbench
{
    public class Startup
    {

        public Startup(IConfiguration configuration, IWebHostEnvironment env)
        {
            Configuration = configuration;
            Environment = env;
        }

        public IConfiguration Configuration { get; }
        public IWebHostEnvironment Environment { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        // For more information on how to configure your application, visit https://go.microsoft.com/fwlink/?LinkID=398940
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddRazorPages();

            var isDevelopment = Environment.IsDevelopment();
            var isDeployedToAzureAppService = Configuration["IS_DEPLOYEDTO_AZUREAPPSERVICE"];

            // If running in Azure App Service, ensure sticky mode is configured
            // Publish Profile will set this environment variable to True
            if (isDeployedToAzureAppService == "True")
            {
                services.AddSignalR().AddAzureSignalR(options =>
                {
                    options.ServerStickyMode = Microsoft.Azure.SignalR.ServerStickyMode.Required;
                });
            }

            // Configure Server Side Blazor
            // Ref: https://docs.microsoft.com/en-us/aspnet/core/signalr/configuration?view=aspnetcore-3.1&tabs=dotnet
            services.AddServerSideBlazor(
                ).AddHubOptions(config =>
            {
                config.EnableDetailedErrors = true;
                config.KeepAliveInterval = new System.TimeSpan(0, 0, 0, 15, 0);
                //config.ClientTimeoutInterval = new System.TimeSpan(0, 0, 0, 50, 0);
                //config.HandshakeTimeout = new System.TimeSpan(0, 0, 0, 50, 0);
            }
            ).AddCircuitOptions(config =>
            {
                config.DetailedErrors = true;
            });


            /* CUSTOM OR OPTIONAL SERVICES */

            // Note: Shown below as an example
            // This can be added automatically, by the Azure Publish Profile
            // With the SignalR configuration & connectionstring as environment variables.
            // It can also be manually configured for local development
            // Ref: https://github.com/aspnet/AzureSignalR-samples/tree/master/samples/ServerSideBlazor
            // services.AddSignalR().AddAzureSignalR();

            // Add data service (provides historical Baseball data to the application)
            services.AddSingleton<BaseballDataSampleService>();

            // Add the ML.NET models and a prediction object pool to the service
            string modelPathInductedToHallOfFameGeneralizedAdditiveModel = Path.Combine(Environment.ContentRootPath, "Models", "InductedToHoF-GeneralizedAdditiveModels.mlnet");
            string modelPathOnHallOfFameBallotGeneralizedAdditiveModel = Path.Combine(Environment.ContentRootPath, "Models", "OnHoFBallot-GeneralizedAdditiveModels.mlnet");

            services.AddPredictionEnginePool<MLBBaseballBatter, MLBHOFPrediction>()
                .FromFile("InductedToHallOfFameGeneralizedAdditiveModel", modelPathInductedToHallOfFameGeneralizedAdditiveModel)
                .FromFile("OnHallOfFameBallotGeneralizedAdditiveModel", modelPathOnHallOfFameBallotGeneralizedAdditiveModel);
            services.AddApplicationInsightsTelemetry(Configuration["APPINSIGHTS_INSTRUMENTATIONKEY"]);
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env, ILogger<Startup> logger)
        {
            logger.LogInformation("Startup-Configure");

            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }
            else
            {
                logger.LogInformation("Startup-Configure: In Production");
                app.UseExceptionHandler("/Error");
                // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
                app.UseHsts();
            }

            app.UseHttpsRedirection();
            app.UseStaticFiles();

            app.UseRouting();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapBlazorHub();
                endpoints.MapFallbackToPage("/_Host");
            });
        }
    }
}
