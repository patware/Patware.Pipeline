using AspireApp1.Web;
using AspireApp1.Web.Components;

using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.AspNetCore.DataProtection.StackExchangeRedis;

using Pipeline.Blazor;
using Pipeline.HttpClient;

using StackExchange.Redis;

var builder = WebApplication.CreateBuilder(args);

// Add service defaults & Aspire client integrations.
builder.AddServiceDefaults();
builder.AddRedisOutputCache("cache");

builder.AddRedisClient("cache");

builder.Services
    .AddDataProtection()
    .SetApplicationName("AspireApp1.Web");

builder.Services
    .AddOptions<KeyManagementOptions>()
    .Configure<IConnectionMultiplexer>((options, redis) =>
    {
        options.XmlRepository = new RedisXmlRepository(
            () => redis.GetDatabase(),
            "AspireApp1.Web:DataProtectionKeys");
    });

// Add services to the container.
builder.Services
    .AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services
    .AddPipelineClient(new Uri("https+http://apiservice"));

builder.Services
    .AddHttpClient<WeatherApiClient>(client =>
    {
        // This URL uses "https+http://" to indicate HTTPS is preferred over HTTP.
        // Learn more about service discovery scheme resolution at https://aka.ms/dotnet/sdschemes.
        client.BaseAddress = new("https+http://apiservice");
    });

var app = builder.Build();

app.Use(async (context, next) =>
{
    context.Response.Headers["X-Instance-Id"] = Environment.MachineName;

    await next(context);
});

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

if (app.Configuration.GetValue("Hosting:UseHttpsRedirection", true))
{
    app.UseHttpsRedirection();
}

app.UseAntiforgery();
app.UseOutputCache();

app.MapStaticAssets();

app.MapRazorComponents<App>()
    .AddPipelinePages()
    .AddInteractiveServerRenderMode();

app.MapDefaultEndpoints();

app.Run();
