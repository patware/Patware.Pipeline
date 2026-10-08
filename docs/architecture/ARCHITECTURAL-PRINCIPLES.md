# Architectural principles

These are source-derived maintenance principles, not a historical record of formal decisions.

1. **Keep business work in services.** Core describes calls; Runtime resolves and invokes services; the application supplies domain behavior.
2. **Separate plans from state.** Immutable graph records contain expressions. Stores contain execution snapshots, restoration data, and logs.
3. **Persist before invoking.** Claims and bound arguments are saved before external work; outcomes are committed afterward under ownership checks.
4. **Make completion conditional.** Revisions reject stale snapshots, and leases reject expired or replaced owners. A local semaphore cannot protect separate processes.
5. **Preserve successful work on retry.** Retry reopens the same run without undoing completed steps or external effects.
6. **Choose providers at composition time.** Persistence and processing are selected once. Built-ins are automatic; business definitions and services remain application registrations.
7. **Read workflow state through monitoring contracts.** Blazor uses Pipeline.Contracts.IPipelineMonitor with a local or HTTP implementation. EF entities, executor ownership, and scheduler internals stay behind that boundary.

Consequences include stable definition versions, repeat-safe actions, and host-owned access and retention policies. See [definitions](PIPELINE-DEFINITIONS.md), [security](SECURITY.md), and [capability boundaries](../design/CAPABILITY-BOUNDARIES.md).

Sources: [plan](https://github.com/patware/Patware.Pipeline/blob/main/src/Pipeline.Core/PipelinePlan.cs), [coordinator](https://github.com/patware/Patware.Pipeline/blob/main/src/Pipeline.Runtime/PipelineExecutionCoordinator.cs), [retry](https://github.com/patware/Patware.Pipeline/blob/main/src/Pipeline.Runtime/PipelineRetryService.cs), [registration](https://github.com/patware/Patware.Pipeline/blob/main/src/Pipeline.Runtime/PipelineServiceCollectionExtensions.cs).
