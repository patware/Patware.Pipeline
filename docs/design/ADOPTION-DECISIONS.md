# Implemented design decisions

This source-derived record explains current choices and consequences. It does not assert historical approval of formal ADRs.

| Choice | Benefit | Constraint |
| --- | --- | --- |
| Typed C# graph and restricted expressions | Type guidance and early validation | No portable executable graph serialization |
| Exact ID/version restoration | Compact persisted specifications | Historical code/contracts remain required |
| Persist arguments before invoking | Attempts reuse business inputs | Captured changes are not rebound; sensitive values may persist |
| One live step and process-wide gate | Simple local coordination | Long steps serialize unrelated local work |
| Revisions and renewable leases | Stale database writes rejected | External side effects are not fenced |
| Independent persistence/processor selection | Four supplied combinations | Hangfire recognizes only known persistence kinds |
| Retry same run, retain success | Resume unfinished work | No compensation or clean-slate rerun semantics |
| Library-owned startup migrations | Simple host configuration | Startup needs schema rights and upgrade validation |
| Process-local event channel | Simple scoped lifecycle handlers | Crash loss and no handler replay |
| Periodic Blazor reads | Provider-neutral UI | Repeated full snapshots/logs can be costly |

Changing a choice requires explicit contract and compatibility work. Per-run gates need a revised reset protocol; durable events need atomic persistence/delivery; graph evolution needs historical restoration support.

Evidence: [builder](https://github.com/patware/Patware.Pipeline/blob/main/src/Pipeline.Core/PipelineGraphBuilder.cs), [coordinator](https://github.com/patware/Patware.Pipeline/blob/main/src/Pipeline.Runtime/PipelineExecutionCoordinator.cs), [registration](https://github.com/patware/Patware.Pipeline/blob/main/src/Pipeline.Runtime/PipelineServiceCollectionExtensions.cs), [dispatcher](https://github.com/patware/Patware.Pipeline/blob/main/src/Pipeline.Runtime/PipelineRunEventDispatcher.cs), [initializer](https://github.com/patware/Patware.Pipeline/blob/main/src/Pipeline.Persistence.EntityFrameworkCore/PipelineDatabaseInitializer.cs), [UI](https://github.com/patware/Patware.Pipeline/blob/main/src/Pipeline.Blazor/Pages/LivePipelinePage.cs).
