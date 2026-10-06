# Patware.Pipeline.Hangfire

**Readable workflows. Background horsepower. A way back after interruption.**

Bring Hangfire processing to the [Pipeline ecosystem](https://github.com/patware/Patware.Pipeline). Dispatch workflow steps as background jobs, schedule the next polling check, and rediscover unfinished runs at startup and every minute.

Keep your C# workflow definitions and `IPipelineRuntime` calls. Select Hangfire in your registration callback and let the integration handle execution dispatch around them.

## Give your workflows their next gear

- **Background dispatch:** coordination jobs select eligible work and dispatch individual step attempts.
- **Scheduled polling:** delayed jobs wake the workflow when its next readiness check is due.
- **Storage that matches:** Hangfire uses in-memory storage or SQL Server according to your pipeline persistence selection.
- **Recovery built in:** an immediate startup scan and recurring minutely scans rediscover queued and running workflows.
- **Retry through the same API:** `IPipelineRuntime.RetryAsync` preserves successful work and dispatches the reopened run.
- **Jobs you can recognize:** Hangfire job names include workflow titles, run IDs, and step-attempt details.

## Install

Requires **.NET 10** and a running .NET host. Version `0.1.0` is an initial development release; the public API may change.

Once available on your NuGet feed:

```shell
dotnet add package Patware.Pipeline.Hangfire --version 0.1.0
```

Runtime, Core, and the Hangfire dependencies are brought in transitively. Use `Pipeline.Hangfire` for the integration and `Pipeline.Runtime` for the runtime API.

## Start with one callback

In your host's service registration:

```csharp
using Pipeline.Hangfire;
using Pipeline.Runtime;

builder.Services.AddPipeline(options => options.UseHangfire());
```

This selects **in-memory pipeline persistence and in-memory Hangfire storage**. It registers the Hangfire server, integration jobs, and recovery startup service. No separate `AddHangfireServer` or built-in `PipelineWorker` registration is needed.

Register your step services, workflow definitions, and `IPipelineDefinitionRegistration` implementations, then start the host. The [Runtime quick start](https://github.com/patware/Patware.Pipeline/blob/main/src/Pipeline.Runtime/README.md) shows a complete example; replace its `AddPipeline()` call with the callback above.

In-memory runs and scheduled jobs are lost when the process ends. Use SQL Server when execution state and job storage need to survive restarts.

## Put the work in SQL Server

Add `Patware.Pipeline.Persistence.EntityFrameworkCore` and configure both providers in the same callback:

```csharp
using Pipeline.Hangfire;
using Pipeline.Persistence.EntityFrameworkCore;
using Pipeline.Runtime;

var connectionString = builder.Configuration
    .GetConnectionString("Pipeline")
    ?? throw new InvalidOperationException(
        "Connection string 'Pipeline' is missing.");

builder.Services.AddPipeline(options =>
    options
        .UseSqlServer(connectionString)
        .UseHangfire());
```

The integration uses the same SQL Server connection string for pipeline persistence and Hangfire storage. Pipeline automatically initializes its pipeline schema before hosted workers start; Hangfire manages its own storage schema. See the [EF Core provider README](https://github.com/patware/Patware.Pipeline/blob/main/src/Pipeline.Persistence.EntityFrameworkCore/README.md) for database initialization details.

Call `AddPipeline` once. Its callback selects both persistence and processing; the Hangfire integration currently supports the in-memory and SQL Server persistence kinds.

## Submit, inspect, resume

Your application continues to use the runtime abstraction:

```csharp
// With an injected IPipelineRuntime and a built PipelineRequest.
var run = await runtime.EnqueueAsync(request, cancellationToken);

var execution = await runtime.GetExecutionAsync(
    run.Id, cancellationToken);

// When a run has failed and is eligible for retry.
var accepted = await runtime.RetryAsync(run.Id, cancellationToken);
```

Submission saves the run before dispatching its Hangfire coordination job. If dispatch fails, the integration logs the failure and leaves the saved run available for recovery. Retry similarly reopens eligible failed runs before dispatching them.

The returned run confirms persistence; it does not mean execution has completed. Keep the host running and use the query APIs or Blazor monitoring to follow progress. Hangfire owns processing, so do not call `IPipelineRuntime.RunAsync` with this provider.

## How work moves forward

1. **Coordinate:** a `PipelineRunJob` advances workflow state and dispatches one eligible step attempt.
2. **Execute:** a `PipelineStepJob` invokes the selected step through the shared execution coordinator.
3. **Continue:** active runs request another coordination pass to select subsequent work or schedule a polling wake-up.
4. **Recover:** a `PipelineRecoveryJob` scans queued and running runs and dispatches recovery coordination.

Expected attempt numbers, execution leases, and persisted state help reject stale dispatches. Recovery uses the original registered definition version to reconstruct the workflow.

Recovery scans active runs; **failed runs require an explicit pipeline retry**. Hangfire's integration jobs have automatic retry attributes with three retry attempts, which is separate from reopening a failed business workflow.

## Two views of the same work

Use the **Hangfire dashboard** to inspect dispatch, scheduled jobs, and processing infrastructure. Use **Pipeline.Blazor** to follow business workflows, job and step progress, scoped logs, and manual retry.

The dashboard is optional and must be mapped by your ASP.NET Core host. For a local Development setup, before `app.Run()`:

```csharp
using Hangfire;

if (app.Environment.IsDevelopment())
{
    app.UseHangfireDashboard("/hangfire");
}
```

Dashboard access follows Hangfire's authorization configuration. Configure that access policy if exposing it beyond the local development machine.

<!-- SCREENSHOT: Add a Hangfire dashboard showing named coordination and step jobs. Use an absolute public image URL for NuGet.org. -->

The [demo host](https://github.com/patware/Patware.Pipeline/tree/main/src/Pipeline.Web) combines SQL Server persistence, Hangfire processing, and Blazor monitoring for an employee provisioning workflow.

## Current processing defaults

| Setting | Value |
| --- | --- |
| Hangfire worker count | 4 |
| Scheduled-job polling interval | 1 second |
| Recovery schedule | Immediately at startup, then every minute |
| SQL Server queue polling interval | Zero |
| SQL Server sliding invisibility timeout | 5 minutes |

These values are set by the integration. `UseHangfire()` currently exposes no configuration callback for overriding them.

Keep every definition ID and version needed by stored runs registered on hosts that process them. Design external actions to tolerate repeated attempts: recovery and retry do not guarantee exactly-once execution of external side effects.

## Complete the workflow stack

| Library | Role |
| --- | --- |
| Pipeline.Core | Build typed C# workflow graphs with conditions, dependencies, and polling. |
| Pipeline.Runtime | Submit and query runs, coordinate execution, and request retry. |
| Pipeline.Persistence.EntityFrameworkCore | Persist run history and execution state in SQL Server. |
| Pipeline.Hangfire | Dispatch background work, schedule wake-ups, and rediscover unfinished runs. |
| Pipeline.Blazor | Show workflow progress, formatted logs, and retry controls. |

## License

[MIT](https://github.com/patware/Patware.Pipeline/blob/main/LICENSE).

---

**Define the process. Dispatch the work. Keep it moving.**
