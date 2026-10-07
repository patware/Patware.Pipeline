# Extension contracts

Pipeline uses compiled libraries and dependency injection. It has no dynamic plugin discovery, assembly sandbox, or manifest-based loader.

| Extension | Responsibility |
| --- | --- |
| IPipelineDefinitionRegistration | Rebuild the exact definition version |
| Business step service | Direct Task-returning method ending in CancellationToken |
| IPipelineStore | Run/specification creation and queries, revision-checked metadata |
| IPipelineExecutionStore | Snapshot reads, active discovery, conditional transitions |
| IStepOutputReader | Producer success, output availability, JSON |
| IPipelineResetService | Provider-specific deletion semantics |
| IPipelineRuntime | Submission, query, retry, processor entry point |
| IPipelineRunEventHandler | Process-local lifecycle reactions |

Provider selection uses [PipelineRegistrationOptions](https://github.com/patware/Patware.Pipeline/blob/main/src/Pipeline.Runtime/PipelineRegistrationOptions.cs). Choose within one `AddPipeline` callback before options freeze. The supplied integrations are `UseSqlServer` and `UseHangfire`; Hangfire only recognizes in-memory and SQL Server persistence kinds.

The clock, builder factory, gate, runtime, and event queue are singletons. Registry, coordinator, binder, invoker, retry, step logger, and context are scoped. Each invocation creates a scope and initializes context/logger before resolving the service. Invocation-only context/logging should not be used as general request services.

Store changes should preserve revision semantics, graph identity, claim fencing, atomic logs, and output availability. Use [shared store tests](https://github.com/patware/Patware.Pipeline/blob/main/tests/Shared/StoreContractTests.cs), not just interface conformance, as a starting point.

Sources: [registration](https://github.com/patware/Patware.Pipeline/blob/main/src/Pipeline.Runtime/PipelineServiceCollectionExtensions.cs), [invoker](https://github.com/patware/Patware.Pipeline/blob/main/src/Pipeline.Runtime/StepInvoker.cs), [Hangfire configuration](https://github.com/patware/Patware.Pipeline/blob/main/src/Pipeline.Hangfire/PipelineHangfireExtensions.cs).
