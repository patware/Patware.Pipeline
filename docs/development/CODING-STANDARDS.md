# Development conventions

Follow [.editorconfig](https://github.com/patware/Patware.Pipeline/blob/main/.editorconfig) and surrounding code. Projects enable nullable references and implicit usings. Preserve asynchronous APIs, cancellation propagation, immutable state records, and guard clauses. Public XML comments feed DocFX and must reflect behavior.

## Layer ownership

Place graph validation/contracts in Core; orchestration and provider-neutral state in Runtime; EF entities/migrations in Persistence; scheduling in Hangfire; reusable rendering in Blazor; business actions/configuration in the consuming host. Keep web/database/scheduler dependencies out of Core.

## Adding a workflow

1. Implement and register services returning `Task`, `Task<T>`, or `Task<bool>`, ending with `CancellationToken`.
2. Define input/settings records and stable definition identity/version; snapshot configuration on submission.
3. Build a graph with stable IDs and supported expressions.
4. Register `IPipelineDefinitionRegistration` to reconstruct that exact graph from saved data.
5. Test restoration and the workflow's success, failure, output, skip, and polling behavior as applicable.

Use the [demo definition](https://github.com/patware/Patware.Pipeline/blob/main/src/Pipeline.Web/Pipelines/AssignLineEmployeeDefinition.cs) and [registration](https://github.com/patware/Patware.Pipeline/blob/main/src/Pipeline.Web/Pipelines/AssignLineEmployeeRegistration.cs) as the pattern. Keep historical versions compatible.

## Execution and persistence changes

Use injected `TimeProvider` for execution timestamps. Preserve revision and claim checks. Keep external calls outside database transactions. Do not recursively acquire `PipelineOperationGate`; steps currently execute while it is held, so awaiting submission/retry/reset through the same host can deadlock.

Make side effects tolerate repeated attempts and cancellation. Use `IPipelineStepContext` and `IPipelineStepLogger` within invocation scopes. An error log alone does not signal step failure.

Library maintainers own migrations. From the repository root:

```powershell
dotnet tool restore --tool-manifest dotnet-tools.json
dotnet ef migrations add DescribeYourChange --project src/Pipeline.Persistence.EntityFrameworkCore --startup-project src/Pipeline.Web --context PipelineDbContext --output-dir Migrations
dotnet ef migrations has-pending-model-changes --project src/Pipeline.Persistence.EntityFrameworkCore --startup-project src/Pipeline.Web --context PipelineDbContext
```

Commit the migration, designer, and snapshot together. Validate fresh initialization, repeat startup, and upgrades on SQL Server. Consumers supply connection strings, not Pipeline migrations. See [CONTRIBUTING.md](https://github.com/patware/Patware.Pipeline/blob/main/CONTRIBUTING.md) and [testing](TESTING.md).
