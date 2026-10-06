using Hangfire;

using Pipeline.Hangfire;
using Pipeline.Persistence.EntityFrameworkCore;
using Pipeline.Runtime;
using Pipeline.Web.Components;
using Pipeline.Web.Pipelines;
using Pipeline.Web.Services;
using Pipeline.Web.Services.Fakes;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddHttpContextAccessor();

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();


// Option 1: Defaults: PipelineRuntime + in-memory persistence.
// builder.Services.AddPipeline();

// Option 2: Hangfire processor + in-memory persistence.
//builder.Services.AddPipeline(options =>
//{
//    options.UseHangfire();
//});


var connectionString = builder.Configuration.GetConnectionString("Pipeline");

if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException("Connection string 'Pipeline' is missing.");
}

// Option 3: Built-in processor + SQL Server persistence.
//builder.Services.AddPipeline(options =>
//{
//    options
//        .UseSqlServer(connectionString)
//});

// Option 4: Hangfire processor + SQL Server persistence.
// Option 4: Hangfire processor + SQL Server persistence.
builder.Services.AddPipeline(options =>
{
    options
        .UseSqlServer(connectionString)
        .UseHangfire();
});

// Formatting sample used to verify log rendering.
builder.Services.AddTransient<Pipeline.Core.Steps.LogFormattingStep>();
builder.Services.AddTransient<Pipeline.Core.Pipelines.LogFormattingPipeline>();

builder.Services.AddTransient<IPipelineDefinitionRegistration, Pipeline.Runtime.Pipelines.LogFormattingRegistration>();

// ============ My stuff ==========================

builder.Services.AddSingleton<DirectorySimulator>();
builder.Services.AddSingleton<IActiveDirectory>(services => services.GetRequiredService<DirectorySimulator>());
builder.Services.AddSingleton<IMsGraph>(services => services.GetRequiredService<DirectorySimulator>());
builder.Services.AddSingleton<IMsTeams>(services => services.GetRequiredService<DirectorySimulator>());

builder.Services.AddScoped<Pipeline.Web.Services.IMyPipelines, Pipeline.Web.Services.MyPipelines>();

builder.Services.AddTransient<AssignLineEmployeeDefinition>();

builder.Services.AddTransient<Pipeline.Runtime.IPipelineDefinitionRegistration, AssignLineEmployeeRegistration>();

builder.Services.AddTransient<Pipeline.Web.Pipelines.Actions.PrepareEmployee>();

builder.Services.AddTransient<Pipeline.Web.Pipelines.Actions.CheckPhoneSystemLicense>();

builder.Services.AddTransient<Pipeline.Web.Pipelines.Actions.AssignPhoneNumber>();

builder.Services.AddTransient<Pipeline.Web.Pipelines.Actions.AssignCallingPolicy>();

builder.Services.AddTransient<Pipeline.Web.Pipelines.Actions.CheckEnterpriseVoiceEnabled>();

builder.Services.AddSingleton<Pipeline.Web.Repository.IRepo, Pipeline.Web.Repository.Fakes.RepoInMemory>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();


app.UseHangfireDashboard("/hangfire", new DashboardOptions
{
    AppPath = "/simulator"
});

app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddAdditionalAssemblies(typeof(global::Pipeline.Blazor.Pages.LivePipelinePage).Assembly)
    .AddInteractiveServerRenderMode();

app.Run();
