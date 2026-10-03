# Patware.Pipeline.Core

**Make your workflow read like the process it represents.**

Define asynchronous workflows in C# with named jobs, explicit dependencies, conditional branches, typed outputs, and polling. Patware.Pipeline.Core gives your business process a clear, validated execution graph that the [Pipeline runtime](https://github.com/patware/Patware.Pipeline/tree/main/src/Pipeline.Runtime) can run.

Your actions stay in ordinary service classes. Your workflow describes how they fit together.

## What you can express

- **Job dependencies:** `.After(...)` makes prerequisites explicit.
- **Ordered actions:** steps within a job run in registration order.
- **Typed data flow:** `.Produces(...)` exposes an output handle; `.Using(...)` binds it to a later invocation.
- **Conditional jobs:** `.When(...)` evaluates an earlier output to decide whether a job should run.
- **Eventual consistency:** `.Poll(...).Check(...)` describes a readiness check with an interval and timeout.
- **Versioned definitions:** stable identities distinguish your workflow and its definition version.
- **Validation before submission:** `Build()` rejects cycles, incomplete steps, empty jobs, and invalid output dependencies.

## Install

Requires **.NET 10**. Version `0.1.0` is an initial development release; the public API may change.

Once available on your NuGet feed:

```shell
dotnet add package Patware.Pipeline.Core --version 0.1.0
```

Use the `Pipeline.Core` namespace. This library builds plans and requests; execution, storage, and background processing are supplied by companion libraries. Installing the runtime package, `Patware.Pipeline.Runtime`, also brings Core in as a dependency.

## Build your first workflow

This complete console example defines two jobs: load an order, then pass the typed result to a shipping action.

```csharp
using Pipeline.Core;

var definition = new PipelineDefinition(
    Id: "ship-order",
    DisplayName: "Ship an order",
    Version: 1);

var input = new ShipOrderInput("ORDER-123");
var settings = new ShipOrderSettings("standard");
var pipeline = new PipelineBuilderFactory().Create(definition);

var load = pipeline.AddJob("load-order");
var order = load
    .Step<LoadOrder>("load")
    .Produces((step, ct) => step.ExecuteAsync(input.OrderId, ct));

pipeline
    .AddJob("ship-order")
    .After(load)
    .Step<ShipOrder>("ship")
    .Using(order)
    .Execute((step, value, ct) =>
        step.ExecuteAsync(value, settings.ServiceLevel, ct));

var plan = pipeline.Build();
var request = PipelineRequest.Create(
    definition: definition,
    title: $"Ship {input.OrderId}",
    createdBy: "order-service",
    input: input,
    settings: settings,
    plan: plan);

Console.WriteLine($"Built workflow: {request.Title}");

public sealed record ShipOrderInput(string OrderId);
public sealed record ShipOrderSettings(string ServiceLevel);
public sealed record Order(string Id, bool ReadyToShip);

public sealed class LoadOrder
{
    public Task<Order> ExecuteAsync(string orderId, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        // Replace with your repository or API call.
        return Task.FromResult(new Order(orderId, ReadyToShip: true));
    }
}

public sealed class ShipOrder
{
    public Task ExecuteAsync(
        Order order, string serviceLevel, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        // Replace with your shipping API call.
        Console.WriteLine($"Shipping {order.Id} via {serviceLevel}");
        return Task.CompletedTask;
    }
}
```

Building the plan records the invocations; it does not call the action services. To execute it, register the services and a versioned restoration registration with the runtime, then submit the request using `IPipelineRuntime.EnqueueAsync`. See the [runtime quick start](https://github.com/patware/Patware.Pipeline/blob/main/src/Pipeline.Runtime/README.md).

## Branch on a result

In the example above, add a condition to the shipping job before `Build()`:

```csharp
pipeline
    .AddJob("ship-if-ready")
    .After(load)
    .When(order, result => result.ReadyToShip)
    .Step<ShipOrder>("ship")
    .Using(order)
    .Execute((step, value, ct) =>
        step.ExecuteAsync(value, settings.ServiceLevel, ct));
```

Use this job **in place of** the unconditional `ship-order` job. A false condition skips the job. If subsequent work should proceed after that skip, connect it with `.After(conditionalJob, allowConditionSkipped: true)`.

Conditions use supported Boolean expressions over an ancestor job's output. Method calls and user-defined operators in conditions are rejected.

## Wait for the world to catch up

Directory synchronization, license assignment, provisioning, and external systems often take time. Describe readiness explicitly rather than embedding a delay loop inside a business action:

```csharp
// Within your definition, before Build().
pipeline.AddJob("wait-for-readiness")
    .Poll<ReadinessCheck>("check")
    .Check(
        (step, ct) => step.ExecuteAsync(input.OrderId, ct),
        every: TimeSpan.FromSeconds(10),
        timeout: TimeSpan.FromMinutes(5));
```

Supply a `ReadinessCheck` service with an `ExecuteAsync(string, CancellationToken)` method returning `Task<bool>`. The runtime completes the step on `true` and reschedules `false` results until the timeout. Polling can also consume a prior typed output with `.Using(...)`.

## A few rules keep the graph predictable

- Give each job a unique, nonblank ID; give each step a unique ID within its job.
- Use output handles from the same builder, produced by an earlier step in the current job or an ancestor job.
- Step expressions must call an instance method directly on the supplied service parameter, with the cancellation token as the last argument. Put business logic inside that method.
- Set positive polling intervals and timeouts.
- Build each builder once. The resulting plan is immutable, and the builder cannot be changed afterward.
- Keep request inputs and settings serializable with the default `System.Text.Json` options.

`StepOutput<T>` is a reference to a future result, not the result itself. The runtime resolves it when a dependent step executes.

## Version the workflow independently of the package

`PipelineDefinition.Version` identifies your application's workflow contract. It is separate from this NuGet package's version.

The runtime restores plans from a saved definition ID and version plus serialized input and settings. Keep compatible restoration registrations available for stored runs, including their original job and step IDs. C# expression trees are rebuilt from your definitions rather than persisted.

## From definition to execution

| Library | Role |
| --- | --- |
| Pipeline.Core | Build and validate workflow plans and submission requests. |
| Pipeline.Runtime | Register services, queue requests, coordinate execution, query state, and retry failures. |
| Pipeline.Persistence.EntityFrameworkCore | Persist workflow data and execution state in SQL Server. |
| Pipeline.Hangfire | Process and schedule work through Hangfire. |
| Pipeline.Blazor | Show run history, job and step progress, logs, and retry controls. |

Explore the [employee provisioning workflow](https://github.com/patware/Patware.Pipeline/blob/main/src/Pipeline.Web/Pipelines/AssignLineEmployeeDefinition.cs) for a business example combining outputs, conditions, dependencies, and polling.

## License

[MIT](https://github.com/patware/Patware.Pipeline/blob/main/LICENSE).
