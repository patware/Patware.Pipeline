# Patware.Pipeline.Runtime

**Give your background work a workflow—and every workflow a story you can follow.**

Patware.Pipeline.Runtime is the execution runtime for the [Pipeline ecosystem](https://github.com/patware/Patware.Pipeline). Turn C# workflow definitions into queued runs, coordinate their jobs and steps, inspect progress, and resume unfinished work after a failure.

Keep business actions in dependency-injected services. Describe dependencies, conditions, typed outputs, and polling with `Pipeline.Core`. Let the runtime move the work forward.

## What you get

- **Background execution:** a hosted worker processes queued runs in your application.
- **A simple starting point:** in-memory persistence works with a single `AddPipeline()` call.
- **Execution visibility:** query run metadata, ordered logs, and job and step snapshots.
- **Versioned workflows:** reconstruct plans from a definition ID, version, and serialized inputs and settings.
- **Retry without starting over:** reopen failed runs while preserving successful steps, outputs, and log history.
- **Room to grow:** select SQL Server persistence and Hangfire processing through companion libraries.

## Install

Requires **.NET 10**. Package version `0.1.0` is an initial development release; the public API may change.

Once available on your NuGet feed:

```shell
dotnet add package Patware.Pipeline.Runtime --version 0.1.0
```

The NuGet package name is `Patware.Pipeline.Runtime`; the runtime namespace is `Pipeline.Runtime`.

## Your first running pipeline

The included log-formatting example provides a small, complete introduction. In a .NET 10 console application, add `Microsoft.Extensions.Hosting` as well as this package, then use the following `Program.cs`:

```csharp
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Pipeline.Core.Pipelines;
using Pipeline.Core.Steps;
using Pipeline.Runtime;
using Pipeline.Runtime.Pipelines;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddPipeline();
builder.Services.AddTransient<LogFormattingStep>();
builder.Services.AddTransient<LogFormattingPipeline>();
builder.Services.AddTransient<
    IPipelineDefinitionRegistration, LogFormattingRegistration>();

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

    Console.WriteLine($"Queued run: {run.Id}");
}

await host.WaitForShutdownAsync();
```

Keep the host running while work executes. The example writes formatting samples to the pipeline's stored logs; query them through the runtime or view them with the companion Blazor library. Error-level sample messages demonstrate formatting and do not fail this example.

## Bring your own workflow

A workflow has three parts:

1. **Step services** implement your asynchronous business actions and receive dependencies through normal constructor injection.
2. **A definition** builds a `PipelinePlan` and wraps it in a `PipelineRequest` containing the definition identity, title, submitter, inputs, and settings.
3. **A restoration registration** implements `IPipelineDefinitionRegistration`, deserializes the saved inputs and settings, and reconstructs the plan for that definition version.

Register each step service and definition in DI, and register the restoration implementation as `IPipelineDefinitionRegistration`. Submit the resulting request with `IPipelineRuntime.EnqueueAsync`.

For a complete business example, explore the [employee provisioning definition](https://github.com/patware/Patware.Pipeline/blob/main/src/Pipeline.Web/Pipelines/AssignLineEmployeeDefinition.cs) and its [restoration registration](https://github.com/patware/Patware.Pipeline/blob/main/src/Pipeline.Web/Pipelines/AssignLineEmployeeRegistration.cs). It prepares an employee, conditionally waits for directory synchronization, assigns a phone number, and verifies the final state.

Plans are rebuilt from registered C# definitions; expression trees are not persisted. Keep older definition versions registered while stored runs reference them, and preserve their job and step IDs and input contracts.

## Follow the work

Use these calls with an injected `IPipelineRuntime` and a submitted run's ID:

```csharp
// Metadata, lifecycle status, and ordered logs.
var run = await runtime.GetRunAsync(runId, cancellationToken);

// Run metadata plus job and step execution state.
var execution = await runtime.GetExecutionAsync(runId, cancellationToken);

// Recent runs, newest first.
var recentRuns = await runtime.GetRunsAsync(
    skip: 0, take: 50, cancellationToken: cancellationToken);

// Resume a failed run's unfinished work.
var accepted = await runtime.RetryAsync(runId, cancellationToken);
```

Single-run queries return `null` when a run is missing. Retry returns `false` if the run is missing, is no longer failed, or changed concurrently. It can throw if a step is still running or the original definition cannot be restored.

Successful steps remain complete during retry. Design external side effects to tolerate repeated attempts: retry and recovery do not provide an exactly-once guarantee for calls to other systems.

## Choose storage and processing

`AddPipeline()` defaults to an in-memory store and the built-in hosted worker. In-memory runs, outputs, and logs are lost when the process ends.

Companion libraries add SQL Server persistence and Hangfire processing. After referencing those libraries:

```csharp
using Pipeline.Hangfire;
using Pipeline.Persistence.EntityFrameworkCore;
using Pipeline.Runtime;

builder.Services.AddPipeline(options =>
    options
        .UseSqlServer(connectionString)
        .UseHangfire());
```

You can also select just SQL Server or just Hangfire. Hangfire uses storage matching the selected persistence configuration and registers startup recovery for unfinished runs.

Call `AddPipeline` **once**, choosing providers in that callback. Apply EF Core migrations before processing database-backed runs; registration does not create the pipeline schema. The host starts the selected processor automatically; application code does not need to call `IPipelineRuntime.RunAsync`.

## Complete the picture

| Companion library | Purpose |
| --- | --- |
| Pipeline.Core | Define graphs, job dependencies, conditions, typed outputs, and polling. |
| Pipeline.Persistence.EntityFrameworkCore | Store pipeline data and execution state in SQL Server through EF Core. |
| Pipeline.Hangfire | Dispatch and schedule work through Hangfire, with startup recovery. |
| Pipeline.Blazor | Display run history, progress, scoped logs, and manual retry controls. |

Find the demo application, source, and documentation in the [Pipeline repository](https://github.com/patware/Patware.Pipeline).

## License

[MIT](https://github.com/patware/Patware.Pipeline/blob/main/LICENSE).
