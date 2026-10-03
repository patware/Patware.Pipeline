# Patware.Pipeline.Persistence.EntityFrameworkCore

**The process restarts. The workflow's story stays.**

Give your pipelines a memory. This SQL Server persistence provider stores run history, workflow specifications, job and step execution state, typed outputs, and logs through Entity Framework Core.

Part of the [Pipeline ecosystem](https://github.com/patware/Patware.Pipeline), it replaces the runtime's in-memory storage through a single registration callback. Keep the same workflow definitions, submission API, and monitoring UI—and put their data in your database.

## Keep more than the final status

- **Run history:** retain titles, submitters, definition identities, lifecycle timestamps, and status.
- **The original request:** save definition versions and serialized inputs and settings so registered definitions can reconstruct their plans.
- **Execution detail:** persist job and step states, attempts, errors, polling schedules, and execution leases.
- **Useful results:** store serialized step outputs and bound arguments for downstream work and retry.
- **The evidence:** preserve ordered logs with severity and job and step scope.
- **Coordinated updates:** database transactions and concurrency revisions protect execution-state transitions from stale writes.

Persistence supplies the stored state used by runtime retry and processor recovery. The selected processor performs the execution; this package provides its database-backed stores.

## Install

Requires **.NET 10** and **SQL Server**. The package uses EF Core's SQL Server provider; it does not currently select other database providers.

Version `0.1.0` is an initial development release; the public API may change. Once available on your NuGet feed:

```shell
dotnet add package Patware.Pipeline.Persistence.EntityFrameworkCore --version 0.1.0
```

Runtime and Core arrive through transitive dependencies. The integration namespace is `Pipeline.Persistence.EntityFrameworkCore`.

## Switch on database-backed workflows

In an ASP.NET Core or Blazor host, read the connection string from configuration and select the provider:

```csharp
using Pipeline.Persistence.EntityFrameworkCore;
using Pipeline.Runtime;

var connectionString = builder.Configuration
    .GetConnectionString("Pipeline")
    ?? throw new InvalidOperationException(
        "Connection string 'Pipeline' is missing.");

builder.Services.AddPipeline(options =>
    options.UseSqlServer(connectionString));
```

Configure `ConnectionStrings:Pipeline` using your host's configuration sources, such as the `ConnectionStrings__Pipeline` environment variable. `UseSqlServer` requires a nonblank connection string.

This keeps the built-in hosted worker and replaces in-memory storage. Register your definitions, step services, and `IPipelineDefinitionRegistration` implementations as described in the [Runtime README](https://github.com/patware/Patware.Pipeline/blob/main/src/Pipeline.Runtime/README.md).

Call `AddPipeline` once. Select persistence and processing together in its callback.

## Give the database its schema

**Registration does not create tables or apply migrations.** Your host owns schema deployment, and the pipeline schema must be ready before processing starts. This package contains `PipelineDbContext` and its model, but does not ship migrations.

Because the context lives in this library, configure a migrations assembly belonging to your application or a dedicated migrations project. For example, if migrations live in your host assembly, `YourApp`:

```csharp
builder.Services.AddPipeline(options =>
    options.UseSqlServer(
        connectionString,
        configureSqlServer: sql => sql.MigrationsAssembly("YourApp")));
```

Replace `YourApp` with your actual assembly name. Use the same migrations assembly in the design-time configuration.

### A host-owned migration setup

Add `Microsoft.EntityFrameworkCore.Design` version `10.0.12` to the host with `PrivateAssets="all"`, and make a compatible .NET 10 `dotnet-ef` tool available. Add this design-time factory to the host project:

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Pipeline.Persistence.EntityFrameworkCore;

public sealed class PipelineDesignTimeFactory
    : IDesignTimeDbContextFactory<PipelineDbContext>
{
    public PipelineDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable(
            "ConnectionStrings__Pipeline")
            ?? throw new InvalidOperationException(
                "Set ConnectionStrings__Pipeline before running EF tools.");

        var migrationsAssembly =
            typeof(PipelineDesignTimeFactory).Assembly.GetName().Name!;

        var options = new DbContextOptionsBuilder<PipelineDbContext>()
            .UseSqlServer(connectionString,
                sql => sql.MigrationsAssembly(migrationsAssembly))
            .Options;

        return new PipelineDbContext(options);
    }
}
```

From the host project directory, generate and apply its initial migration:

```shell
dotnet ef migrations add InitialPipeline --context PipelineDbContext --output-dir Migrations/Pipeline
dotnet ef database update --context PipelineDbContext
```

Review the generated migration before applying it. As you upgrade this package, generate migrations for model changes and deploy them before starting the updated processors. For a separate migrations project, follow [EF Core's migrations project guidance](https://learn.microsoft.com/en-us/ef/core/managing-schemas/migrations/projects).

The [demo host's migrations](https://github.com/patware/Patware.Pipeline/tree/main/src/Pipeline.Web/Migrations) show the repository's schema evolution.

## What lives in SQL Server?

| Table | Stored data |
| --- | --- |
| `PipelineRuns` | Run identity, workflow definition, title, submitter, lifecycle state, timestamps, and revision. |
| `PipelineSpecifications` | Definition ID and version plus serialized input and settings. |
| `PipelineJobs` | Job execution state, condition results, timing, errors, and revision. |
| `PipelineSteps` | Step execution state, attempts, bound arguments, outputs, polling deadlines, leases, and errors. |
| `PipelineRunLogs` | Ordered log entries with timestamps, severity, and optional job and step identifiers. |

Stores use short-lived contexts obtained from `IDbContextFactory<PipelineDbContext>`. Application code normally accesses these records through the runtime APIs rather than manipulating persistence entities directly.

## Pair persistence with your processor

For SQL Server storage with the built-in worker, use `UseSqlServer` alone. To use Hangfire processing, reference the Hangfire companion library and select both providers:

```csharp
using Pipeline.Hangfire;
using Pipeline.Persistence.EntityFrameworkCore;
using Pipeline.Runtime;

builder.Services.AddPipeline(options =>
    options
        .UseSqlServer(connectionString,
            sql => sql.MigrationsAssembly("YourApp"))
        .UseHangfire());
```

Hangfire uses the same SQL Server connection string for its storage and registers startup recovery of unfinished runs. Configure and deploy the pipeline schema separately from Hangfire's storage setup.

## Keep recovery compatible

Persisted runs reference an exact workflow definition ID and version. Keep those restoration registrations available while their runs remain stored, including the original job and step IDs and compatible input and settings contracts.

The database stores specifications and execution state; executable plans are rebuilt from your C# definitions. Design external actions to tolerate repeated attempts: persistence does not make an external API call and its state update one atomic operation.

## Complete the workflow stack

| Library | What it adds |
| --- | --- |
| Pipeline.Core | Readable C# graphs with dependencies, conditions, typed outputs, and polling. |
| Pipeline.Runtime | Submission, execution coordination, queries, and manual retry. |
| Pipeline.Persistence.EntityFrameworkCore | SQL Server storage for workflow history and execution state. |
| Pipeline.Hangfire | Background dispatch, scheduling, and startup recovery. |
| Pipeline.Blazor | Run history, job and step progress, formatted logs, and retry controls. |

Explore the [demo application](https://github.com/patware/Patware.Pipeline/tree/main/src/Pipeline.Web) to see database-backed employee provisioning with Hangfire and Blazor monitoring.

## License

[MIT](https://github.com/patware/Patware.Pipeline/blob/main/LICENSE).

---

**Keep the history. Preserve the progress. Know what happens next.**
