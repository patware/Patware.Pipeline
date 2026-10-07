# Logs, snapshots, and events

Runtime queries expose metadata/logs or full job/step snapshots. `GetExecutionAsync` restores the plan to order jobs, so missing historical registrations can break detailed inspection. Missing runs return null. List queries default to 50 recent runs.

## Stored logs

Transitions append messages with state updates. Services inject `IPipelineStepLogger` for information, warning, and error messages scoped to the current step. It verifies the claim, retries revision conflicts up to five times, and rejects writes after ownership loss. A logging exception can fail an invocation if the service does not handle it.

An error-level message alone does not fail a run; an invocation exception does. `Style` supplies ANSI formatting. Blazor parses supported sequences into styled spans and Razor encodes their text.

## Lifecycle notifications

`Queued`, `Started`, `Completed`, `Failed`, and `RetryRequested` are published after successful state changes. Events carry run ID, committed revision, definition, title, submitter, state, time, and status text.

An unbounded in-memory channel feeds one dispatcher. Each notification gets a fresh DI scope; handlers execute sequentially. Exceptions are logged without changing workflow outcomes. Delivery has no durable replay or automatic retry. A process crash can lose notifications, and publication after commit is not an atomic outbox transaction.

## Monitoring

Blazor polls snapshots rather than subscribing to events. The list refreshes every 20 seconds; active detail every second; terminal detail every 20 seconds. Hangfire's dashboard represents scheduling infrastructure, while Pipeline pages represent business progress.

There is no dedicated metrics exporter or tracing integration. Host `ILogger` output captures worker discovery/dispatch errors and handler failures; inspect it alongside stored outcomes.

Sources: [step logger](https://github.com/patware/Patware.Pipeline/blob/main/src/Pipeline.Runtime/PipelineStepLogger.cs), [queue](https://github.com/patware/Patware.Pipeline/blob/main/src/Pipeline.Runtime/PipelineRunEventQueue.cs), [dispatcher](https://github.com/patware/Patware.Pipeline/blob/main/src/Pipeline.Runtime/PipelineRunEventDispatcher.cs), [live pages](https://github.com/patware/Patware.Pipeline/blob/main/src/Pipeline.Blazor/Pages/LivePipelinePage.cs).
