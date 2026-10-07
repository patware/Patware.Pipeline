# Pipeline

These guides describe the implementation in `src/`, reviewed on 2026-10-06. They distinguish current behavior, implementation limits, and consuming-host responsibilities. Each guide links to the relevant source.

Browse the [Core API reference](xref:Pipeline.Core) or start with the library matching your task:

| Library | Purpose |
| --- | --- |
| [Pipeline.Core](xref:Pipeline.Core) | Define jobs, dependencies, conditions, step invocations, and typed outputs. |
| [Pipeline.Runtime](xref:Pipeline.Runtime) | Register services, submit and query runs, restore definitions, and coordinate execution. |
| [Pipeline.Persistence.EntityFrameworkCore](xref:Pipeline.Persistence.EntityFrameworkCore) | Persist pipeline data in SQL Server using Entity Framework Core. |
| [Pipeline.Hangfire](xref:Pipeline.Hangfire) | Dispatch execution through Hangfire and recover unfinished runs. |
| [Pipeline.Blazor](xref:Pipeline.Blazor) | Display runs, job progress, and formatted logs with periodic refresh. |

To register the default runtime, use `AddPipeline`. Register step services and an `IPipelineDefinitionRegistration` for each definition version separately. Select SQL Server persistence with `UseSqlServer` and Hangfire processing with `UseHangfire` in the same configuration callback when needed. SQL Server persistence automatically applies bundled migrations during host startup before hosted workers begin processing; applications only configure the connection string.

## Documentation guide

Start with the architecture overview to understand the library boundaries, or the development setup to build and run the demo. Design documents explain source-derived choices rather than claiming historical approval of formal decisions.

| Area | Documents |
| --- | --- |
| Architecture | [Library architecture](architecture/ARCHITECTURE.md), [principles](architecture/ARCHITECTURAL-PRINCIPLES.md), and [execution model](architecture/PIPELINES.md). |
| Definitions and execution | [Definitions](architecture/PIPELINE-DEFINITIONS.md), [orchestrator](architecture/PIPELINE-ORCHESTRATOR.md), [lifecycle semantics](architecture/STATE-MACHINES.md), and [waits and approvals](architecture/WAITS-AND-APPROVALS.md). |
| Persistence and integrations | [Persistence](architecture/DATABASE.md), [reconciliation](architecture/RECONCILIATION.md), and [extension contracts](architecture/PLUGIN-ARCHITECTURE.md). |
| Security and observability | [Security](architecture/SECURITY.md) and [execution history and observability](architecture/OBSERVABILITY.md). |
| Development | [Getting started](development/GETTING-STARTED.md), [coding guidance](development/CODING-STANDARDS.md), [testing](development/TESTING.md), and [UI integration](development/UI-GUIDELINES.md). |
| Design | [Implemented decisions](design/ADOPTION-DECISIONS.md) and [capability boundaries](design/CAPABILITY-BOUNDARIES.md). |

## Generate this documentation

With the .NET 10 SDK and DocFX installed, run these commands from the repository root:

```powershell
dotnet restore Pipeline.slnx
docfx docs/docfx.json
```

DocFX writes generated API metadata to `docs/api` and the HTML site to `docs/_site`. To build and preview the site locally, run:

```powershell
docfx docs/docfx.json --serve
```
