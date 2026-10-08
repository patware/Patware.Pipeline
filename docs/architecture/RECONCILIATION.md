# Recovery and external consistency

Recovery discovers stored `Queued` and `Running` runs. It does not reopen failed workflows. The built-in runtime repeatedly scans active runs. Hangfire enqueues recovery at startup and registers `pipeline-recovery` every minute.

Across replicas, recovery requires shared durable persistence and compatible definitions. The operation gate is process-local. Persisted leases last two minutes and renew every twenty seconds, so takeover can wait for expiry. See [distributed hosting](DISTRIBUTED-HOSTING.md) and [Kubernetes verification](../development/KUBERNETES.md).

| Interruption | Behavior |
| --- | --- |
| Saved run, failed Hangfire enqueue | Submission logs the failure; recovery can dispatch later |
| Selected work never starts | Selection created no claim; work can be selected again |
| Worker stops during invocation | Claim remains until lease expiry permits reclamation |
| Old worker finishes after losing ownership | Claim/revision checks reject result and log writes |
| Business action fails | Failure propagates; explicit retry required |
| In-memory process exits | Stored state is lost; restart recovery unavailable |

Recovery probes avoid creating duplicate delayed wake-ups when no transition progresses. Later scheduled work or discovery advances the run. Compatibility overloads in `PipelineRunJob` retain earlier persisted Hangfire signatures.

## External side effects

An external action can succeed before its result is committed. A crash in that gap can cause repeat invocation. Leases fence stored writes, not arbitrary external APIs. There is no dispatch outbox, compensation engine, or exactly-once external-effect guarantee.

Use stable business keys or application-managed idempotency keys. `IPipelineStepContext` exposes run/job/step identity inside invocations. If an API cannot deduplicate, query its state and implement domain-specific reconciliation. The simulator is not a production reconciliation service.

Sources: [Hangfire runtime](https://github.com/patware/Patware.Pipeline/blob/main/src/Pipeline.Hangfire/HangfirePipelineRuntime.cs), [recovery](https://github.com/patware/Patware.Pipeline/blob/main/src/Pipeline.Hangfire/PipelineRecoveryJob.cs), [startup](https://github.com/patware/Patware.Pipeline/blob/main/src/Pipeline.Hangfire/PipelineHangfireStartupService.cs), [context](https://github.com/patware/Patware.Pipeline/blob/main/src/Pipeline.Core/IPipelineStepContext.cs).
