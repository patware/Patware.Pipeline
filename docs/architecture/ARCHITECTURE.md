# Architecture

Pipeline is a C# workflow library with versioned graphs, persisted execution snapshots, selectable processors, and Blazor monitoring. These guides describe the source reviewed on 2026-10-06, not proposed features or generated site output.

| Project | Responsibility | Internal dependencies |
| --- | --- | --- |
| Pipeline.Core | Fluent graph validation, expressions, requests, typed output references, step context/logging contracts | None |
| Pipeline.Runtime | Registration, restoration, submission, coordination, retry, in-memory stores, lifecycle events | Core |
| Pipeline.Persistence.EntityFrameworkCore | SQL Server stores, schema, migrations, reset | Runtime |
| Pipeline.Hangfire | Background dispatch, delayed wake-ups, active-run recovery | Runtime |
| Pipeline.Blazor | Run list/detail pages, job components, ANSI logs | Core, Runtime |
| Pipeline.Web | Demo composition root, simulated directory services, employee provisioning | All five libraries |

See [solution membership](https://github.com/patware/Patware.Pipeline/blob/main/Pipeline.slnx) and [service registration](https://github.com/patware/Patware.Pipeline/blob/main/src/Pipeline.Runtime/PipelineServiceCollectionExtensions.cs). Build artifacts under `bin/` and `obj/` do not establish additional supported projects.

## Execution flow

```mermaid
flowchart TD
    Host[Host builds PipelineRequest] --> Runtime[IPipelineRuntime.EnqueueAsync]
    Runtime --> Store[Persist run, specification, graph state and log]
    Store --> Processor[Built-in worker or Hangfire jobs]
    Processor --> Coordinator[PipelineExecutionCoordinator]
    Coordinator --> Registry[Restore versioned C# plan]
    Coordinator --> Claim[Persist claim and bound arguments]
    Claim --> Invoke[Invoke scoped business service]
    Invoke --> Commit[Commit outcome and transition log]
    Commit --> Processor
    Store --> UI[Blazor snapshot queries]
```

Stored specifications contain definition ID/version and JSON input/settings, not executable expression trees. Compatible application code remains necessary for execution and detailed inspection.

`AddPipeline` can be called once. Its callback independently selects persistence and processing, then freezes. The default uses a singleton in-memory store and hosted worker. SQL Server adds library-owned startup migrations. Hangfire replaces the built-in worker and selects storage matching the persistence configuration.

The coordinator permits one live invocation per run. The singleton `PipelineOperationGate` also serializes gated operations across all runs in a process and remains held during business execution. Four Hangfire workers do not imply four concurrent business invocations in one host. Database revisions and leases protect cross-process state changes; external effects can still repeat after interruption.

Continue with [orchestration](PIPELINE-ORCHESTRATOR.md), [persistence](DATABASE.md), [recovery](RECONCILIATION.md), and [design decisions](../design/ADOPTION-DECISIONS.md).
