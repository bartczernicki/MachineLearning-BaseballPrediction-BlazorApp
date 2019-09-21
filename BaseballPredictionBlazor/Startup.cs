using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.HttpsPolicy;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using BaseballPredictionBlazor.Service;
using BaseballPredictionBlazor.Shared;
using System.IO;
using Microsoft.Extensions.ML;
using Scalable.Model.Engine;

namespace BaseballPredictionBlazor
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        // For more information on how to configure your application, visit https://go.microsoft.com/fwlink/?LinkID=398940
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddRazorPages();
            services.AddServerSideBlazor();

            // Add .NET Core App Version service
            services.AddTransient<IAppVersionService, AppVersionService>();

            // Add data service (provides data to the application)
            services.AddSingleton<BaseballDataSampleService>();

            string modelPathInductedToHallOfFame = Path.Combine(Environment.CurrentDirectory, "Models", "InductedToHallOfFame.mlnet");
            string modelPathOnHallOfFameBallot = Path.Combine(Environment.CurrentDirectory, "Models", "OnHallOfFameBallot.mlnet");

            // OLD
            //services.AddSingleton<MLModelEngine<MLBBaseballBatter, MLBHOFPrediction>>((ctx) =>
            //{
            //    List<string> modelPathNames = new List<string> { modelPathInductedToHallOfFame, modelPathOnHallOfFameBallot };
            //    List<string> modelNames = new List<string> { "InductedToHallOfFame", "OnHallOfFameBallot" };
            //    return new MLModelEngine<MLBBaseballBatter, MLBHOFPrediction>(modelNames, modelPathNames);
            //});

            services.AddPredictionEnginePool<MLBBaseballBatter, MLBHOFPrediction>()
                .FromFile("InductedToHallOfFame", modelPathInductedToHallOfFame)
                .FromFile("OnHallOfFameBallot", modelPathOnHallOfFameBallot);
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }
            else
            {
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
