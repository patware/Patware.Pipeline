# Pipeline

### Turn “it’s still running” into “here’s exactly where it is.”

**Composable workflows for .NET. Written in C#. Visible in Blazor.**

Real business workflows have dependencies, slow external systems, conditional steps, and the occasional spectacular failure. Pipeline gives those workflows a shape: define the work, connect the jobs, wait for the right conditions, and follow each run from submission to completion.

Keep your business logic in ordinary dependency-injected services. Let Pipeline handle the orchestration around it.

[See the workflow](#a-workflow-you-can-read) · [Get started](#your-first-run) · [Choose your libraries](#one-workflow-stack-pick-the-pieces-you-need) · [Explore the demo](#see-it-in-action)

<!-- SCREENSHOT: Add a wide run-detail screenshot here. Show job cards, a polling step, and a few colorful log lines. Suggested asset: docs/images/pipeline-run.png. -->

## Business processes deserve better than a mystery background task

Provision an employee. Wait for directory synchronization. Assign a phone number. Apply a policy. Verify the change actually took effect.

That’s a workflow. And when step four fails, you want to know what succeeded, what failed, and what can run next.

Pipeline brings the execution story into your application:

- **Express the order.** Connect jobs with `.After(...)` and keep steps inside each job sequential.
- **Make results useful.** Produce typed outputs and pass them into later steps with `.Using(...)`.
- **Branch on what happened.** Gate jobs with `.When(...)` and explicitly allow downstream work after a condition skips a job.
- **Wait with a plan.** Poll external systems with a defined interval and timeout.
- **Resume unfinished work.** Retry a failed run while preserving successful steps, their outputs, and log history.
- **See the whole run.** Blazor pages show status, job and step progress, and logs filtered down to the work you’re investigating.

**You write the business action. Pipeline connects it to everything that happens before and after.**

## A workflow you can read

This excerpt comes from the [employee phone-line provisioning demo](src/Pipeline.Web/Pipelines/AssignLineEmployeeDefinition.cs). The action services contain the business logic; the graph makes the process readable.

```csharp
var pipeline = builders.Create(Definition);

var prepare = pipeline.AddJob("prepare");

var preparation = prepare
    .Step<PrepareEmployee>("prepare-employee")
    .Produces((step, ct) => step.ExecuteAsync(input.Upn, ct));

var waitForSync = pipeline
    .AddJob("wait-for-directory-sync")
    .After(prepare)
    .When(preparation, result => result.RequiresDirectorySync);

waitForSync
    .Poll<CheckPhoneSystemLicense>("check-license")
    .Check(
        (step, ct) => step.ExecuteAsync(
            input.Upn, settings.PhoneSystemLicense, ct),
        every: TimeSpan.FromMinutes(1),
        timeout: TimeSpan.FromMinutes(30));

pipeline
    .AddJob("assign-line")
    .After(waitForSync, allowConditionSkipped: true)
    .Step<AssignPhoneNumber>("assign-number")
    .Execute((step, ct) =>
        step.ExecuteAsync(input.Upn, input.PhoneNumber, ct))
    .Step<AssignCallingPolicy>("assign-policy")
    .Execute((step, ct) =>
        step.ExecuteAsync(input.Upn, settings.CallingPolicy, ct));

var plan = pipeline.Build();
```

```mermaid
flowchart LR
    A[Prepare employee] --> B{Directory sync needed?}
    B -->|Yes| C[Poll for license]
    B -->|No: skip waiting| D[Assign phone number]
    C --> D
    D --> E[Apply calling policy]
```

The complete demo also polls for enterprise voice enablement after applying the policy. Each check returns `true` when ready; `false` schedules another attempt until the timeout.

## One workflow stack. Pick the pieces you need.

| Library | What it brings |
| --- | --- |
| [Pipeline.Core](src/Pipeline.Core) | Fluent graph building, dependencies, conditions, typed outputs, and versioned definitions. |
| [Pipeline.Contracts](src/Pipeline.Contracts) | Transport-independent monitoring interface, display enums, summaries, and execution views. |
| [Pipeline.Runtime](src/Pipeline.Runtime) | Dependency injection, submission, execution coordination, run queries, and manual retry. Includes in-memory storage and a hosted worker. |
| [Pipeline.Persistence.EntityFrameworkCore](src/Pipeline.Persistence.EntityFrameworkCore) | SQL Server persistence for run data, execution state, outputs, and logs through EF Core. |
| [Pipeline.Hangfire](src/Pipeline.Hangfire) | Hangfire processing, scheduled work, and startup recovery of unfinished runs. |
| [Pipeline.Blazor](src/Pipeline.Blazor) | Run overview and detail pages, job cards, periodically refreshed progress, and ANSI-formatted logs. |
| [Pipeline.AspNetCore](src/Pipeline.AspNetCore) | Library-owned HTTP monitoring and retry endpoints for an executor host. |
| [Pipeline.HttpClient](src/Pipeline.HttpClient) | Remote monitoring client for a separate renderer host. |

The libraries target **.NET 10**. The shared version is **0.3.0**, prepared for publication; the public API remains pre-1.0. See [release and upgrade notes](CHANGELOG.md). Examples targeting 0.3.0 require that version on your chosen package feed.

### Package dependencies

Arrows point from a package to its direct dependencies within the eight-package
Pipeline family. External dependencies are omitted.

```mermaid
flowchart TD
    Blazor["Patware.Pipeline.Blazor"] --> Contracts["Patware.Pipeline.Contracts"]
    AspNetCore["Patware.Pipeline.AspNetCore"] --> Contracts
    HttpClient["Patware.Pipeline.HttpClient"] --> Contracts
    Hangfire["Patware.Pipeline.Hangfire"] --> Runtime
    Persistence["Patware.Pipeline.Persistence.EntityFrameworkCore"] --> Runtime
    Runtime --> Core
    Runtime --> Contracts
```

Runtime brings in Core transitively for Hangfire and Entity Framework Core
persistence. Blazor references Contracts and can use local or remote monitoring. Choose the
processing, persistence, and UI packages independently to suit your application.

## Your first run

Start with the included log-formatting pipeline to see registration, submission, and execution without writing a definition first.

Reference [Pipeline.Runtime](src/Pipeline.Runtime/Pipeline.Runtime.csproj) from a .NET 10 application using the .NET Generic Host. Built-in steps and the log-formatting definition are registered automatically:

```csharp
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Pipeline.Core.Pipelines;
using Pipeline.Runtime;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddPipeline();

using var host = builder.Build();
await host.StartAsync();

using (var scope = host.Services.CreateScope())
{
    var definition = scope.ServiceProvider
        .GetRequiredService<LogFormattingPipeline>();
    var runtime = scope.ServiceProvider
        .GetRequiredService<IPipelineRuntime>();

    var run = await runtime.EnqueueAsync(
        definition.Build(createdBy: "quick-start"));

    Console.WriteLine($"Queued pipeline run: {run.Id}");
}

await host.WaitForShutdownAsync();
```

`AddPipeline()` selects in-memory persistence and the built-in background worker. The running host processes queued work; in-memory run history lasts for the lifetime of the process.

For an automatic demonstration at startup, use `AddPipeline(options => options.RunLogFormattingDemoOnStartup = true)`. The option defaults to false and submits a new run for every enabled host startup.

For your own workflow, register its step services and an `IPipelineDefinitionRegistration` that rebuilds the plan from its persisted definition ID, version, inputs, and settings. The [demo definition](src/Pipeline.Web/Pipelines/AssignLineEmployeeDefinition.cs) and its [registration](src/Pipeline.Web/Pipelines/AssignLineEmployeeRegistration.cs) show the complete pattern.

## Start small. Add persistence and scheduling when you need them.

Persistence and processing are independent choices. Choose **one** of the four
registrations below for your application. Use these namespaces for the options
you select:

```csharp
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Pipeline.Hangfire;
using Pipeline.Persistence.EntityFrameworkCore;
using Pipeline.Runtime;
```

### Option 1: Built-in processor + in-memory persistence

The defaults use `PipelineRuntime` with the built-in background worker and
in-memory persistence:

```csharp
builder.Services.AddPipeline();
```

### Option 2: Hangfire processor + in-memory persistence

```csharp
builder.Services.AddPipeline(options =>
{
    options.UseHangfire();
});
```

### Configure the connection string for options 3 and 4

Read and validate the connection string before registering either SQL Server
option:

```csharp
var connectionString = builder.Configuration.GetConnectionString("Pipeline");

if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException("Connection string 'Pipeline' is missing.");
}
```

### Option 3: Built-in processor + SQL Server persistence

```csharp
builder.Services.AddPipeline(options =>
{
    options.UseSqlServer(connectionString: connectionString);
});
```

### Option 4: Hangfire processor + SQL Server persistence

```csharp
builder.Services.AddPipeline(options =>
{
    options
        .UseSqlServer(connectionString: connectionString)
        .UseHangfire();
});
```

At a glance:

| Storage | Processing | Configuration inside `AddPipeline` |
| --- | --- | --- |
| In memory | Built-in worker | No callback needed |
| In memory | Hangfire | `options.UseHangfire()` |
| SQL Server | Built-in worker | `options.UseSqlServer(connectionString)` |
| SQL Server | Hangfire | `options.UseSqlServer(connectionString).UseHangfire()` |

SQL Server persistence initializes automatically when the host starts. The persistence library applies its bundled migrations to the pipeline schema before hosted workers begin processing. Applications only supply the connection string. Keep the definition versions used by persisted runs registered so recovery and retry can reconstruct their original plans.

## Give every run a front row seat

The Blazor library turns execution state into something people can follow:

- **Run overview:** recent submissions, who started them, current status, and progress.
- **Run detail:** job and step state with active runs refreshing every second.
- **Focused logs:** inspect the entire run or narrow the view to a job or step.
- **Readable output:** log levels and ANSI formatting bring context to the console.
- **Recovery controls:** failed runs expose a **Re-run from failure** action.

<!-- SCREENSHOT: Add the run overview here. Suggested asset: docs/images/pipeline.runs.png. -->
<!-- SCREENSHOT: Add a failed run with the retry button and filtered step logs here. Suggested asset: docs/images/pipeline-retry.png. -->

The demo host shows how to [register pipeline pages and interactive server rendering](src/Pipeline.Web/Program.cs). Its monitoring routes are `/pipeline` and `/pipeline/run/{RunId}`. Use `PipelineRouter` in the host's Routes component to discover the library pages automatically while retaining the host layout.

## Run the renderer and executor separately

The executor host owns definitions, persistence, and processing:

```csharp
using Pipeline.AspNetCore;
using Pipeline.Runtime;

builder.Services.AddPipeline();
// After builder.Build(), before app.Run():
app.MapPipelineEndpoints();
```

The renderer host registers a remote monitor instead of an executor:

```csharp
using Pipeline.Blazor;
using Pipeline.HttpClient;

// Register host-wide HTTP defaults first.
builder.Services.AddPipelineClient(new Uri("https://backend.example/"));

app.MapRazorComponents<App>()
    .AddPipelinePages()
    .AddInteractiveServerRenderMode();
```

The library HTTP routes are under `/api/pipeline`; the UI routes remain under `/pipeline`. No general-purpose submission endpoint is included.

[AspireApp1](src/AspireApp1/README.md) demonstrates separate frontend and backend processes. Its Kubernetes sample uses two replicas of each, shared SQL Server storage, Redis Data Protection keys, and Traefik frontend affinity. See [distributed hosting](docs/architecture/DISTRIBUTED-HOSTING.md) and [Kubernetes verification](docs/development/KUBERNETES.md).

Multiple executor replicas require shared storage and compatible definition versions. Revisions and leases fence stored updates; external side effects can repeat. Blazor circuits remain local to a frontend pod.

## See it in action

The repository includes two separate sample applications. They demonstrate how to consume the Pipeline libraries and are not part of the library packages.

| Sample application | Purpose |
| --- | --- |
| [Pipeline.Web](src/Pipeline.Web) | An all-in-one Blazor application that hosts rendering and execution together. It showcases how to use the libraries and build your own pipelines and tasks. |
| [AspireApp1](src/AspireApp1/README.md) | A demonstration and test application for the libraries' distributed capabilities, with separate Blazor frontend and API backend hosts and a Kubernetes setup for testing multiple replicas and executor recovery. |

### Pipeline.Web: build your own workflows

Pipeline.Web includes a simulated directory environment and an employee phone-line provisioning workflow. Watch licensing synchronization, phone assignment, policy updates, and verification play out as separate steps.

To run the demo:

1. Install the .NET 10 SDK and have a SQL Server instance available.
2. Set the `ConnectionStrings__Pipeline` environment variable to your SQL Server connection string.
3. Start the host:

   ```powershell
   dotnet run --project src/Pipeline.Web --launch-profile https
   ```

The host automatically prepares Pipeline and Hangfire storage. The configured SQL Server identity needs permission to create and update their database objects.

Open the application URL printed by the host and visit `/simulator`. Follow submitted workflows at `/pipeline`. The current sample maps the Hangfire dashboard at `/hangfire`; deployed hosts must configure access.

<!-- SCREENSHOT: Add the directory simulator here, ideally alongside a run waiting for synchronization. Suggested asset: docs/images/pipeline-simulator.png. -->

### AspireApp1: exercise distributed hosting

Use AspireApp1 to run the renderer and executor in separate processes and test the libraries across host boundaries. Its Kubernetes configuration adds load-balanced frontend and backend replicas, shared SQL Server persistence, and Redis-backed Data Protection keys.

See the [AspireApp1 sample guide](src/AspireApp1/README.md) for local setup and the [Kubernetes verification guide](docs/development/KUBERNETES.md) for replica and recovery checks.

## Build, explore, contribute

```powershell
dotnet restore Pipeline.slnx
dotnet build Pipeline.slnx
# See tests/README.md for the library and Aspire test runners.
```

Explore the [documentation overview](docs/index.md), or generate the API documentation with DocFX:

```powershell
docfx docs/docfx.json --serve
```

Have a workflow that would make a great example? Found a rough edge? Open an issue with the process you’re trying to model, the behavior you expected, and a minimal reproduction when possible. Pull requests are welcome.

The SDK is selected by [global.json](global.json). Visual Studio users can import
[.vsconfig](.vsconfig) to install the web development workload. The shared build
version comes from [VERSION](VERSION).

See the [contribution guide](CONTRIBUTING.md), [branch guide](branch-guide.md),
[test guide](tests/README.md), and [changelog](CHANGELOG.md) for development details.
For help, see [support](SUPPORT.md). Participation follows the
[Code of Conduct](CODE_OF_CONDUCT.md); report vulnerabilities using the
[security policy](SECURITY.md).

## License

[MIT](LICENSE) — build something useful with it.

---

**Make the workflow readable. Make the execution visible. Make the next step obvious.**
