# Patware.Pipeline.HttpClient

HTTP-backed pipeline monitoring for a separate renderer host. Requires .NET 10. Version `0.3.0` is a pre-1.0 release.

```powershell
dotnet add package Patware.Pipeline.HttpClient --version 0.3.0
```

## Register remote monitoring

```csharp
using Pipeline.HttpClient;

// Register host-wide HTTP defaults first.
builder.Services.AddPipelineClient(
    new Uri("https://pipeline-backend.example/"));
```

In an Aspire host, call `builder.AddServiceDefaults()` first and use the referenced service name:

```csharp
builder.Services.AddPipelineClient(new Uri("https+http://apiservice"));
```

The registration supplies `Pipeline.Contracts.IPipelineMonitor` and `TimeProvider`, without registering an executor. The returned `IHttpClientBuilder` supports host-specific authentication and handlers. Configure one monitoring source per host; do not combine this registration with a local `AddPipeline` monitor.

The base address must be absolute and contain no query or fragment. Relative protocol paths preserve an application prefix, for example `https://host/myapp/` targets `myapp/api/pipeline/...`.

## Status and resilience

- Summary GETs validate skip and page size, and require a non-null JSON collection.
- Execution GETs map 404 to `null`.
- Retry POSTs map 409 to `false` and require 202 for acceptance.
- Other errors throw; unexpected successful protocol responses are rejected.
- Cancellation propagates to HTTP and JSON reads.

Registration removes inherited resilience handlers, retains other handlers such as service discovery, and installs a standard policy with retries disabled for unsafe methods. This prevents inherited policies from automatically replaying retry commands. Register host-wide defaults before this method.

`RemoveAllResilienceHandlers` is currently a Microsoft experimental API. Its `EXTEXP0001` suppression is limited to that call; review the dependency when upgrading Microsoft.Extensions.Http.Resilience. Avoid adding another retrying policy that replays POST requests.

The client does not submit executable plans, provide push notifications, or cache live snapshots. See the [backend endpoints](https://github.com/patware/Patware.Pipeline/blob/main/src/Pipeline.AspNetCore/README.md) and [Blazor integration](https://github.com/patware/Patware.Pipeline/blob/main/src/Pipeline.Blazor/README.md).

## License

[MIT](https://github.com/patware/Patware.Pipeline/blob/main/LICENSE).
