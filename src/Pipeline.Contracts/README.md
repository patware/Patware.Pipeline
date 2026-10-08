# Patware.Pipeline.Contracts

Transport-independent monitoring contracts for .NET 10. Version `0.3.0` is a pre-1.0 release.

```powershell
dotnet add package Patware.Pipeline.Contracts --version 0.3.0
```

Use the `Pipeline.Contracts` namespace. This package has no dependency on Core, Runtime, ASP.NET Core, or a persistence provider.

## Monitoring contract

`IPipelineMonitor` supplies three asynchronous operations, each accepting cancellation:

| Operation | Result |
| --- | --- |
| `GetRunsAsync(skip, take, cancellationToken)` | Recent summaries with ordered job statuses; no logs or step details. |
| `GetExecutionAsync(runId, cancellationToken)` | A display snapshot, or `null` for a missing run. |
| `RetryAsync(runId, cancellationToken)` | Whether a failed run was reopened; `false` for missing, ineligible, or concurrently changed runs. |

Views contain display data rather than executable plans, bound arguments, outputs, revisions, or lease ownership. Contracts enums are distinct from similarly named Runtime enums: custom renderers should use the Contracts types.

`AddPipeline` in Patware.Pipeline.Runtime supplies a local adapter. `AddPipelineClient` in Patware.Pipeline.HttpClient supplies an HTTP implementation. Patware.Pipeline.Blazor consumes this interface without depending on the executor.

The HTTP integration limits page size to 1–100. Implementations may throw for transport or execution errors; cancellation and missing-run handling are separate from a failed request.

This is a monitoring and retry contract, not a general-purpose submission or cancellation API. Lifecycle notifications remain process-local Runtime features.

See the [distributed-hosting guide](https://github.com/patware/Patware.Pipeline/blob/main/docs/architecture/DISTRIBUTED-HOSTING.md).

## License

[MIT](https://github.com/patware/Patware.Pipeline/blob/main/LICENSE).
