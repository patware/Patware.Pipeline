# Patware.Pipeline.AspNetCore

Library-owned HTTP endpoints for pipeline monitoring and retry. Requires .NET 10 and ASP.NET Core. Version `0.3.0` is a pre-1.0 release.

```powershell
dotnet add package Patware.Pipeline.AspNetCore --version 0.3.0
dotnet add package Patware.Pipeline.Runtime --version 0.3.0
```

## Configure the executor host

```csharp
using Pipeline.AspNetCore;
using Pipeline.Runtime;

builder.Services.AddPipeline();

// After builder.Build(), before app.Run():
app.MapPipelineEndpoints();
```

Select SQL Server and/or Hangfire in the AddPipeline callback when needed. Register application-defined steps and definition versions; built-in steps and the log-formatting definition are automatic.

| Method and route | Behaviour |
| --- | --- |
| `GET /api/pipeline/runs?skip=0&take=50` | Run summaries and ordered job statuses. Invalid skip/take returns 400; take must be 1–100. |
| `GET /api/pipeline/runs/{runId:guid}/execution` | Display snapshot; 404 when missing. |
| `POST /api/pipeline/runs/{runId:guid}/retry` | 202 when reopened; 409 when missing, ineligible, or changed concurrently. |

JSON uses fixed web defaults independently of unrelated application JSON settings. Monitoring responses carry `Cache-Control: no-store`. Endpoint failures are logged and return a generic problem response; request cancellation remains cancellation.

`MapPipelineEndpoints` returns a route group for host configuration, including `.RequireAuthorization(...)`. Authentication and per-run authorization are host responsibilities; mapping does not establish a tenant policy.

There is no general-purpose submission endpoint. The Aspire sample's `/demo` endpoints belong to that sample and are enabled explicitly through configuration.

This package references Contracts and the `Microsoft.AspNetCore.App` framework, not the deprecated NuGet package of the same name. It does not register a worker or choose persistence itself.

See the [HTTP client](https://github.com/patware/Patware.Pipeline/blob/main/src/Pipeline.HttpClient/README.md) and [distributed-hosting guide](https://github.com/patware/Patware.Pipeline/blob/main/docs/architecture/DISTRIBUTED-HOSTING.md).

## License

[MIT](https://github.com/patware/Patware.Pipeline/blob/main/LICENSE).
