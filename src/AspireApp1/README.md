# Aspire distributed-hosting sample

This .NET 10 sample hosts Pipeline execution in AspireApp1.ApiService and Blazor monitoring in AspireApp1.Web. AspireApp1.AppHost orchestrates local development; it is not the application server deployed to Kubernetes.

## Run locally

Docker is needed for the Redis resource. From the repository root:

```powershell
dotnet run --project src/AspireApp1/AspireApp1.AppHost/AspireApp1.AppHost.csproj
```

Open the frontend from the Aspire dashboard and visit /pipeline. Without a Pipeline SQL connection, the backend uses in-memory persistence. RunLogFormattingDemoOnStartup submits the built-in demonstration by default in this sample.

The frontend registers service defaults before AddPipelineClient and resolves `https+http://apiservice`. It uses PipelineRouter and AddPipelinePages. Redis provides output caching and shared Data Protection keys.

## Kubernetes

The checked-in Dockerfiles and k8s manifests deploy two frontends and two SQL-backed executors. Traefik balances HTTP traffic and pins browser sessions to their Blazor frontend. See the [complete deployment and verification guide](../../docs/development/KUBERNETES.md).

Sample endpoints /demo/log-formatting and /demo/probe are mapped only when Pipeline:EnableDemoEndpoints is true. They must be registered before app.Run. The delayed probe logs its executing pod and waits one minute so interruption and recovery can be inspected.

The manifests disable automatic demo submission, require shared persistence, expose orchestration health checks, and configure local HTTP access. X-Instance-Id identifies responding pods.

## Tests

```powershell
dotnet test --project src/AspireApp1/AspireApp1.Tests/AspireApp1.Tests.csproj
```

The Aspire test verifies backend execution and initial frontend HTML. It does not automate Kubernetes pod deletion or browser circuit failover. Those checks are described separately in the Kubernetes guide.

The maintainer reported successful Kubernetes operation during API pod interruption. Shared SQL retains run history; recovery is lease-based and external actions may repeat. The single SQL and Redis instances in this local sample do not establish datastore high availability.
