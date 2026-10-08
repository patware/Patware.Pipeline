using AspireApp1.ApiService.Pipelines;

using Pipeline.AspNetCore;
using Pipeline.Core.Pipelines;
using Pipeline.Persistence.EntityFrameworkCore;
using Pipeline.Runtime;

var builder = WebApplication.CreateBuilder(args);

// Add service defaults & Aspire client integrations.
builder.AddServiceDefaults();

// Add services to the container.
builder.Services.AddProblemDetails();

// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

var pipelineConnection = builder.Configuration.GetConnectionString("Pipeline");

var requireSharedStorage = builder.Configuration.GetValue<bool>("Pipeline:RequireSharedStorage");

if (requireSharedStorage && string.IsNullOrWhiteSpace(pipelineConnection))
{
    throw new InvalidOperationException("ConnectionStrings:Pipeline is required when shared storage is enabled.");
}

builder.Services.AddPipeline(options =>
{
    if (!string.IsNullOrWhiteSpace(pipelineConnection))
    {
        options.UseSqlServer(pipelineConnection);
    }

    options.RunLogFormattingDemoOnStartup = builder.Configuration.GetValue("Pipeline:RunLogFormattingDemoOnStartup", true);
});

if (!string.IsNullOrWhiteSpace(pipelineConnection))
{
    builder.Services.AddHealthChecks()
        .AddDbContextCheck<PipelineDbContext>("pipeline-database");
}

builder.Services.AddTransient<KubernetesProbeStep>();
builder.Services.AddTransient<KubernetesProbePipeline>();
builder.Services.AddTransient<IPipelineDefinitionRegistration, KubernetesProbeRegistration>();

var app = builder.Build();

app.Use(async (context, next) =>
{
    context.Response.Headers["X-Instance-Id"] = Environment.MachineName;

    await next(context);
});

// Configure the HTTP request pipeline.
app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

string[] summaries = ["Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"];

app.MapGet("/", () => "API service is running. Navigate to /weatherforecast to see sample data.");

app.MapGet("/weatherforecast", () =>
{
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

app.MapPipelineEndpoints();

if (app.Configuration.GetValue<bool>("Pipeline:EnableDemoEndpoints"))
{
    app.MapPost(
        "/demo/probe",
        async (
            KubernetesProbePipeline pipeline,
            IPipelineRuntime runtime,
            CancellationToken cancellationToken) =>
        {
            var run = await runtime.EnqueueAsync(
                pipeline.Build(),
                cancellationToken);

            return Results.Accepted(
                $"/api/pipeline/runs/{run.Id:D}/execution",
                new { run.Id });
        });

    app.MapPost(
        "/demo/log-formatting",
        async (
            LogFormattingPipeline pipeline,
            IPipelineRuntime runtime,
            CancellationToken cancellationToken) =>
        {
            var run = await runtime.EnqueueAsync(
                pipeline.Build("Kubernetes demonstration"),
                cancellationToken);

            return Results.Accepted(
                $"/api/pipeline/runs/{run.Id:D}/execution",
                new { run.Id });
        });
}

app.Run();

record WeatherForecast(DateOnly Date, int TemperatureC, string? Summary)
{
    public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);
}
