# Pipeline

These guides describe the 0.3.0 implementation in `src/`, reviewed on 2026-10-08. They distinguish current behavior, implementation limits, and consuming-host responsibilities. Publication is pending; VERSION identifies the prepared build.

Browse the [Core API reference](xref:Pipeline.Core) or start with the library matching your task:

| Library | Purpose |
| --- | --- |
| [Pipeline.Core](xref:Pipeline.Core) | Define jobs, dependencies, conditions, step invocations, and typed outputs. |
| [Pipeline.Contracts](xref:Pipeline.Contracts) | Monitoring contracts and display views without executor dependencies. |
| [Pipeline.Runtime](xref:Pipeline.Runtime) | Register services, submit and query runs, restore definitions, and coordinate execution. |
| [Pipeline.Persistence.EntityFrameworkCore](xref:Pipeline.Persistence.EntityFrameworkCore) | Persist pipeline data in SQL Server using Entity Framework Core. |
| [Pipeline.Hangfire](xref:Pipeline.Hangfire) | Dispatch execution through Hangfire and recover unfinished runs. |
| [Pipeline.Blazor](xref:Pipeline.Blazor) | Display runs, job progress, and formatted logs with periodic refresh. |
| [Pipeline.AspNetCore](xref:Pipeline.AspNetCore) | Map HTTP monitoring and retry endpoints in the backend. |
| [Pipeline.HttpClient](xref:Pipeline.HttpClient) | Query a separate backend from the renderer. |

To register the default runtime, use `AddPipeline`. Built-in steps and the log-formatting definition are automatic. Register application steps and an `IPipelineDefinitionRegistration` for each application definition version separately. Select `UseSqlServer` and `UseHangfire` in the same callback when needed. SQL Server automatically applies bundled migrations before hosted workers start.

Separate hosts use `MapPipelineEndpoints` on the executor and `AddPipelineClient` on the renderer. Blazor uses `AddPipelinePages` and `PipelineRouter`. Start with [distributed hosting](architecture/DISTRIBUTED-HOSTING.md) and [Kubernetes verification](development/KUBERNETES.md).

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
dotnet build Pipeline.slnx --configuration Release --no-restore
docfx docs/docfx.json
```

DocFX reads the eight compiled Release assemblies and their adjacent XML documentation files. This includes generated Razor components without requiring DocFX's bundled compiler to run the SDK's Razor generator. Rebuild after source/XML documentation changes. DocFX writes metadata to `docs/api` and HTML to `docs/_site`; both are ignored generated output. See [DocFX assembly input](https://dotnet.github.io/docfx/docs/dotnet-api-docs.html).

To preview the site locally after building the assemblies, run:

```powershell
docfx docs/docfx.json --serve
```
