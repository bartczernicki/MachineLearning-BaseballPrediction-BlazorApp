using Azure.Identity;
using Azure.Security.KeyVault.Secrets;
using System;

var builder = WebApplication.CreateBuilder(args);


// Add service defaults & Aspire client integrations.
builder.AddServiceDefaults();

builder.Configuration.AddAzureKeyVaultSecrets(connectionName: "AOAIEastUS2Gpt41");

// Add services to the container.
builder.Services.AddProblemDetails();

// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

var app = builder.Build();

// Configure the HTTP request pipeline.
app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

string[] summaries = ["Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"];

app.MapGet("/weatherforecast", () =>
{
    var client = new SecretClient(kvUri, new DefaultAzureCredential());
    var secret = client.GetSecret("AOAIEastUS2Gpt41");
    var test = app.Configuration["AOAIEastUS2Gpt41"];
    Console.WriteLine($"Secret: {test}");
    var forecast = Enumerable.Range(1, 5).Select(index =>
        new WeatherForecast
        (
            DateOnly.FromDateTime(DateTime.Now.AddDays(index)),
            Random.Shared.Next(-20, 55),
            summaries[Random.Shared.Next(summaries.Length)]
        ))
        .ToArray();
    return forecast;
})
.WithName("GetWeatherForecast");

app.MapDefaultEndpoints();

app.Run();

record WeatherForecast(DateOnly Date, int TemperatureC, string? Summary)
{
    public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);
}
