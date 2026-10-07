# Patware.Pipeline.Blazor

**Your workflows do the work. Give them a window.**

Turn background execution into a story your team can follow. Patware.Pipeline.Blazor adds workflow monitoring to your Blazor application: recent runs, job and step progress, polling countdowns, formatted logs, and a way to resume failed work.

Built for the [Pipeline ecosystem](https://github.com/patware/Patware.Pipeline), it connects directly to `IPipelineMonitor` and brings execution state into your application's UI.

<!-- SCREENSHOT: Add a wide run-detail image showing job cards, a polling countdown, and colorful logs. Use an absolute public image URL for display on NuGet.org. -->

## See the work. Find the hold-up. Move it forward.

- **Follow every run:** see the latest 50 submissions, who started them, their status, and progress.
- **Open the details:** inspect a run's definition, submission and execution timestamps, jobs, and steps.
- **Watch the wait:** job cards show elapsed time and polling countdowns while external systems catch up.
- **Get straight to the evidence:** filter logs by job or step instead of searching an entire run.
- **Read logs with context:** severity labels and supported ANSI colors and styles make output easier to scan.
- **Resume after a failure:** the **Re-run from failure** button asks the runtime to retry unfinished work while preserving successful steps and their outputs.

Active run details refresh every second. The run overview and inactive run details refresh every 20 seconds. Updates use periodic runtime queries.

## Install

Requires **.NET 10** and a **Blazor Web App with Interactive Server rendering**. The included pages declare that render mode and access the runtime through server-side dependency injection.

Version `0.1.0` is an initial development release; the public API may change. Once available on your NuGet feed:

```shell
dotnet add package Patware.Pipeline.Blazor --version 0.1.0
```

Core and Runtime are package dependencies. Use the `Pipeline.Blazor.Pages` and `Pipeline.Blazor.Components` namespaces in your application.

## Add workflow monitoring to your app

### 1. Register the runtime and Interactive Server components

In `Program.cs`, configure the services and map the library's routable components alongside your app:

```csharp
using Pipeline.Blazor.Pages;
using Pipeline.Runtime;
// Keep your app's existing using for its App component.

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddPipeline();

// Register your workflow definitions, step services, and
// IPipelineDefinitionRegistration implementations here.

var app = builder.Build();

// Keep your app's existing exception handling and other middleware.
app.UseHttpsRedirection();
app.UseAntiforgery();
app.MapStaticAssets();

app.MapRazorComponents<App>()
    .AddAdditionalAssemblies(typeof(LivePipelinePage).Assembly)
    .AddInteractiveServerRenderMode();

app.Run();
```

If your app already calls `AddPipeline()`, use that registration. Call it once and configure any SQL Server or Hangfire providers in its callback. See the [Runtime README](https://github.com/patware/Patware.Pipeline/blob/main/src/Pipeline.Runtime/README.md) for workflow registration and execution.

### 2. Include the pages in your router

Add the library assembly to the existing `Router` in `Routes.razor`, retaining your application's layout and route handling:

```razor
<Router AppAssembly="typeof(Program).Assembly"
        AdditionalAssemblies="new[] {
            typeof(Pipeline.Blazor.Pages.LivePipelinePage).Assembly }">
    <Found Context="routeData">
        <RouteView RouteData="routeData"
                   DefaultLayout="typeof(Layout.MainLayout)" />
        <FocusOnNavigate RouteData="routeData" Selector="h1" />
    </Found>
</Router>
```

Both registrations matter: the endpoint mapping discovers the library pages on the server, and the router discovers them during interactive navigation. If you already list additional assemblies, append this assembly to that list.

### 3. Load the styles

The pages use **Bootstrap 5** classes and the library's isolated CSS. Include Bootstrap and your host's generated CSS isolation bundle in `App.razor`. A standard Bootstrap-based Blazor template already supplies these links:

```razor
<link rel="stylesheet" href="@Assets["lib/bootstrap/css/bootstrap.min.css"]" />
<link rel="stylesheet" href="@Assets["YourApp.styles.css"]" />
```

Replace `YourApp` with your host assembly name and use the Bootstrap path supplied by your app. The host bundle brings in the library's isolated styles. Keep the template's Blazor script, `HeadOutlet`, and interactive rendering configuration.

### 4. Open the dashboard

Add a navigation link to `/pipeline`, submit a workflow through your runtime, and follow it from the overview into its detail page.

| Route | What you'll see |
| --- | --- |
| `/pipeline` | The latest 50 runs, newest first. |
| `/pipeline/run/{RunId}` | Run metadata, job and step cards, logs, and retry controls for failed runs. |
| `/pipeline/run/{RunId}?job={JobId}` | Logs scoped to one job. |
| `/pipeline/run/{RunId}?job={JobId}&step={StepId}` | Logs scoped to one step within that job. |

<!-- SCREENSHOT: Add the run overview here. Use an absolute public image URL when embedding it. -->

## A dashboard that uses your execution stack

The pages query and control the registered `IPipelineMonitor`. Use the built-in worker and in-memory storage for a small starting point, or companion providers for SQL Server persistence and Hangfire processing.

With in-memory storage, history disappears when the process ends. Database-backed deployments retain history according to your storage management. Retry availability depends on the run state and the original workflow definition remaining registered.

The host application controls access to monitoring and retry actions. Integrate these routes with your app's authorization policy; the included pages do not declare an authorization requirement.

<!-- SCREENSHOT: Add a failed run with the retry action and step-filtered logs here. Use an absolute public image URL when embedding it. -->

## Reuse the components

You can also bring the building blocks into your own pages:

| Component | Purpose |
| --- | --- |
| `PipelineJobCards` | Show job and step execution state, timings, polling countdowns, and links to scoped run logs. Requires the registered `TimeProvider` supplied by `AddPipeline`. |
| `AnsiLogText` | Render log text with supported ANSI formatting as HTML-encoded spans. |
| `LivePipelinePage` | Derive a custom monitoring page with serialized snapshot loading, periodic refresh, and disposal cleanup. |

The [demo host](https://github.com/patware/Patware.Pipeline/tree/main/src/Pipeline.Web) shows the complete integration, including a directory simulator and an employee provisioning workflow. Follow license synchronization, phone assignment, and verification as they happen.

## License

[MIT](https://github.com/patware/Patware.Pipeline/blob/main/LICENSE).

---

**Make background work visible. Make the next action obvious.**
