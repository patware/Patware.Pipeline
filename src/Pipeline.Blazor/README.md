# Patware.Pipeline.Blazor

Run history, job and step progress, formatted logs, and retry controls for a Blazor Web App with Interactive Server rendering. Requires .NET 10. Version `0.3.0` is a pre-1.0 release.

```powershell
dotnet add package Patware.Pipeline.Blazor --version 0.3.0
```

Blazor depends on Patware.Pipeline.Contracts, not Core or Runtime. Pages inject `IPipelineMonitor` and can display local or remote execution.

## Choose a monitoring source

For a combined executor/renderer host, reference Patware.Pipeline.Runtime and call `builder.Services.AddPipeline()` once. Built-in steps and the log-formatting definition are automatically registered; register your own steps and definition versions separately.

For a separate renderer, reference Patware.Pipeline.HttpClient:

```csharp
using Pipeline.HttpClient;

// Configure host-wide HTTP defaults first.
builder.Services.AddPipelineClient(new Uri("https://backend.example/"));
```

In Aspire, call `builder.AddServiceDefaults()` first and use `https+http://apiservice`. Remote renderers do not call AddPipeline or host an executor.

## Map the pages

Keep the host's existing middleware and App component:

```csharp
using Pipeline.Blazor;

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// After builder.Build(), before app.Run():
app.MapRazorComponents<App>()
    .AddPipelinePages()
    .AddInteractiveServerRenderMode();
```

In Routes.razor, use the library router while retaining your layout:

```razor
@using Pipeline.Blazor.Components

<PipelineRouter AppAssembly="typeof(Program).Assembly">
    <Found Context="routeData">
        <RouteView RouteData="routeData"
                   DefaultLayout="typeof(Layout.MainLayout)" />
        <FocusOnNavigate RouteData="routeData" Selector="h1" />
    </Found>
</PipelineRouter>
```

PipelineRouter combines the pipeline assembly with any AdditionalAssemblies you supply. It forwards Found, Navigating, OnNavigateAsync, and NotFoundPage. An existing framework Router can instead list the pipeline assembly manually. Endpoint discovery and component-router discovery are separate responsibilities.

## Styles and navigation

Load Bootstrap 5 and your application's generated CSS isolation bundle, such as `YourApp.styles.css`. Use the Bootstrap path supplied by your template. The host bundle imports the library's isolated styles; keep the template's Blazor script and head configuration.

| Route | Behaviour |
| --- | --- |
| `/pipeline` | Latest 50 summaries and ordered job statuses; 20-second refresh. |
| `/pipeline/run/{RunId:guid}` | Run, job, step, and log details; one-second active refresh and 20-second inactive refresh. |
| `/pipeline/run/{RunId}?job={JobId}` | Logs scoped to a job. |
| `/pipeline/run/{RunId}?job={JobId}&step={StepId}` | Logs scoped to a step in that job. |

Snapshot failures show a generic message and retain previously loaded data. Refresh continues, and detail pages discard stale results after route changes. Failed runs expose a retry action through the monitor.

## Upgrade from runtime-coupled rendering

Update old page links to the routes above. Custom components now use display enums and view types from Pipeline.Contracts instead of Runtime execution models. Core and Runtime are no longer transitive Blazor dependencies; applications using executor APIs must reference Runtime explicitly.

## Reusable components

| Component | Purpose |
| --- | --- |
| PipelineRouter | Discover pipeline routes while retaining host presentation. |
| PipelineJobCards | Display job/step state, timings, polling, and scoped-log links. |
| PipelineJobStatuses | Display ordered job status icons from summary job views. |
| AnsiLogText | Render supported ANSI styles using encoded text. |
| LivePipelinePage | Serialized snapshot loading, refresh, error handling, and async cleanup. |

AddPipeline and AddPipelineClient provide the TimeProvider used by timing components. Pages do not declare authentication requirements; hosts own access policies.

## Multiple frontend replicas

Use session affinity for Interactive Server circuits and a shared Data Protection key repository/application name. A frontend failure can require a new circuit; shared keys and Redis caching do not move an existing circuit between pods.

See [distributed hosting](https://github.com/patware/Patware.Pipeline/blob/main/docs/architecture/DISTRIBUTED-HOSTING.md), the [Aspire sample](https://github.com/patware/Patware.Pipeline/tree/main/src/AspireApp1), and [Kubernetes verification](https://github.com/patware/Patware.Pipeline/blob/main/docs/development/KUBERNETES.md).

## License

[MIT](https://github.com/patware/Patware.Pipeline/blob/main/LICENSE).
