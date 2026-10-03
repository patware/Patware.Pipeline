# Pipeline

Pipeline provides a graph builder, an execution runtime, persistence providers, and Blazor components for defining and monitoring asynchronous workflows.

Browse the [Core API reference](xref:Pipeline.Core) or start with the library matching your task:

| Library | Purpose |
| --- | --- |
| [Pipeline.Core](xref:Pipeline.Core) | Define jobs, dependencies, conditions, step invocations, and typed outputs. |
| [Pipeline.Runtime](xref:Pipeline.Runtime) | Register services, submit and query runs, restore definitions, and coordinate execution. |
| [Pipeline.Persistence.EntityFrameworkCore](xref:Pipeline.Persistence.EntityFrameworkCore) | Persist pipeline data in SQL Server using Entity Framework Core. |
| [Pipeline.Hangfire](xref:Pipeline.Hangfire) | Dispatch execution through Hangfire and recover unfinished runs. |
| [Pipeline.Blazor](xref:Pipeline.Blazor) | Display runs, job progress, and formatted logs with periodic refresh. |

To register the default runtime, use `AddPipeline`. Register step services and an `IPipelineDefinitionRegistration` for each definition version separately. Select SQL Server persistence with `UseSqlServer` and Hangfire processing with `UseHangfire` in the same configuration callback when needed.

## Generate this documentation

With the .NET 10 SDK and DocFX installed, run these commands from the repository root:

```powershell
dotnet restore Pipeline.slnx
docfx doc/docfx.json
```

DocFX writes generated API metadata to `doc/api` and the HTML site to `doc/_site`. To build and preview the site locally, run:

```powershell
docfx doc/docfx.json --serve
```
