# State transitions and retry

The coordinator, rather than enum names alone, determines reachable behavior.

| Entity | Normal path | Other modeled outcomes |
| --- | --- | --- |
| Run | Queued → Running → Completed | Failed, Cancelled |
| Job | Pending → Running → Succeeded | ConditionSkipped, Blocked, Failed, TimedOut, Cancelled |
| Step | Pending → Running → Succeeded | Waiting → Running, Failed, TimedOut, Cancelled |

False conditions skip the job while its steps remain pending. Completion requires every job to succeed or be condition-skipped. A dependency that rejects a skipped prerequisite blocks the dependent job and fails the run. Condition exceptions fail the job and run.

A step's failure/timeout is saved before a subsequent pass propagates it to the job/run. Other unfinished jobs become blocked; succeeded and condition-skipped jobs are preserved. A snapshot can therefore briefly contain a failed step inside a running run.

`Cancelled` is recognized by terminal checks, but `IPipelineRuntime` has no user cancel-run API. Host shutdown leaves interrupted work recoverable rather than representing a user cancellation.

## Manual retry

`RetryAsync` reopens the same failed run as `Running`, clearing its finish time. Missing, nonfailed, or concurrently changed runs return false. Running steps or unrestorable definitions prevent reopening and can throw.

| Preserved | Reset for unfinished work |
| --- | --- |
| Run ID, submission/start metadata, logs | Run finish time and status text |
| Succeeded and condition-skipped jobs | Other jobs become pending; condition/timing/error cleared |
| Successful steps and outputs | Other steps become pending; output/error/lease/timing cleared |
| Saved arguments and attempt counters | Poll deadline and next-attempt time |

A retried poll gets a fresh deadline at its next claim. Retry does not compensate external actions, and a failed step may have partially succeeded externally.

Sources: [coordinator](https://github.com/patware/Patware.Pipeline/blob/main/src/Pipeline.Runtime/PipelineExecutionCoordinator.cs), [retry](https://github.com/patware/Patware.Pipeline/blob/main/src/Pipeline.Runtime/PipelineRetryService.cs), [run](https://github.com/patware/Patware.Pipeline/blob/main/src/Pipeline.Runtime/PipelineRun.cs), [job](https://github.com/patware/Patware.Pipeline/blob/main/src/Pipeline.Runtime/JobExecutionState.cs), [step](https://github.com/patware/Patware.Pipeline/blob/main/src/Pipeline.Runtime/StepExecutionState.cs).
