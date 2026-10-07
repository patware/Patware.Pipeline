# Definitions and restoration

`PipelineDefinition` contains an ID, display name, and version. The factory validates a positive version. A builder creates a `PipelinePlan`; `PipelineRequest.Create` serializes input/settings and verifies matching request/plan definitions.

## Builder contract

Use unique nonblank job IDs and unique step IDs within each job. Complete every reserved step with `Execute`, `Produces`, or `Check`. `Build()` rejects empty graphs/jobs, cycles, incomplete steps, and unavailable output dependencies. A successful build freezes the builder; it cannot be changed or built again.

`StepOutput<T>` is a reference, not an executed value. `Using` accepts outputs from an earlier step in the same job or a producer in an ancestor job. Cross-builder references fail. A condition must consume an ancestor output because its own job has not started.

## Expressions and binding

Invocations are direct instance-method calls on the service parameter, ending with the supplied cancellation token. Commands return `Task`, producers `Task<T>`, and polls `Task<bool>`.

Arguments support constants, declared outputs, captured instance fields/properties, and built-in conversions. Arbitrary method calls, static members, indexers, and user-defined operators are rejected. Conditions additionally support built-in comparisons and Boolean negation/conjunction/disjunction. Compute complex values in a service or before building the expression.

The binder serializes business arguments as a JSON array before invocation. Subsequent attempts reuse saved arguments. Capture stable data: immutable graph records do not deep-copy mutable objects captured by expressions before binding.

## Version compatibility

Register `IPipelineDefinitionRegistration` for each `(DefinitionId, DefinitionVersion)`. It deserializes saved input/settings and rebuilds the original graph. The registry rejects duplicate keys and throws for missing versions.

Keep old registrations, job/step IDs, selected service methods, and serialized contracts compatible while stored runs need execution, retry, or detailed inspection. Use a new version for incompatible graph changes; no automatic execution-graph migration exists. Snapshot configuration into settings at submission rather than reading new configuration during restoration.

Sources: [builder](https://github.com/patware/Patware.Pipeline/blob/main/src/Pipeline.Core/PipelineGraphBuilder.cs), [validator](https://github.com/patware/Patware.Pipeline/blob/main/src/Pipeline.Core/ArgumentExpressionValidator.cs), [binder](https://github.com/patware/Patware.Pipeline/blob/main/src/Pipeline.Runtime/StepArgumentBinder.cs), [registry](https://github.com/patware/Patware.Pipeline/blob/main/src/Pipeline.Runtime/PipelineDefinitionRegistry.cs), [demo restoration](https://github.com/patware/Patware.Pipeline/blob/main/src/Pipeline.Web/Pipelines/AssignLineEmployeeRegistration.cs).
