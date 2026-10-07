# Orchestration and ownership

Both processors use [PipelineExecutionCoordinator](https://github.com/patware/Patware.Pipeline/blob/main/src/Pipeline.Runtime/PipelineExecutionCoordinator.cs). `AdvanceAsync` coordinates and invokes directly. Hangfire uses `PrepareNextStepAsync` to select a work item, then `ExecuteStepAsync` to attempt that job, step, and expected attempt.

## Coordination pass

1. Load the snapshot and stop for missing/terminal runs.
2. Validate targeted dispatch, restore the exact definition, and stop if a live lease owns an invocation.
3. Start a queued run, propagate step failure, or complete a run whose jobs succeeded or were condition-skipped.
4. Scan jobs in plan order, checking dependencies and conditions before finding the first unsuccessful step.
5. Enforce deadline and next-attempt time; select work or save a claim.
6. Save bound arguments, invoke the scoped service, and commit its outcome while ownership remains valid.

Each pass usually saves one transition. Selection alone does not claim a step. Targeted work is checked against the next step and `Attempt + 1`, so stale dispatches cannot simply rerun completed work.

## Leases and cancellation

Claims use GUID tokens and two-minute leases renewed every 20 seconds. Owned writes reload state and retry revision conflicts up to five times. Ownership requires a running step, matching token, and unexpired stored lease.

Invocation cancellation combines host shutdown, lease loss, and polling deadline. Shutdown or ownership loss leaves the claim to expire instead of saving a result. Cancellation is cooperative: code that ignores it may still change external systems after ownership expires.

The [operation gate](https://github.com/patware/Patware.Pipeline/blob/main/src/Pipeline.Runtime/PipelineOperationGate.cs) is process-wide and non-reentrant. It covers submission, retry, reset, and coordination, including awaited business execution. A step must not await another gated operation through the same host, such as enqueueing a run: that can wait on its own held gate.

## Processor loops

The [built-in runtime](https://github.com/patware/Patware.Pipeline/blob/main/src/Pipeline.Runtime/PipelineRuntime.cs) scans active runs sequentially, performs up to 100 immediate transitions per run, then delays one second after the pass. Long invocations delay later runs.

[Hangfire coordination](https://github.com/patware/Patware.Pipeline/blob/main/src/Pipeline.Hangfire/PipelineRunJob.cs) also caps passes at 100 transitions, dispatches one step job, or schedules a wake-up. The step job requests further coordination. Integration defaults are four workers and a one-second schedule polling interval. Infrastructure jobs have three automatic retries; persisted business failure still requires explicit pipeline retry.
