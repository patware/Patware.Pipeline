# Polling and approval boundaries

The implemented wait primitive is a Boolean polling step. There is no approval record, callback-resume API, signal correlation, or human approval UI.

`Check` requires positive interval and timeout values. The first claim sets `PollDeadline`. False results set `Waiting`, clear the lease, and choose the earlier of completion time plus the interval or the deadline for `NextAttemptAt`. The interval is measured after the unsuccessful check finishes.

At or after the deadline the step times out. An in-progress check receives cancellation for the remaining time, but cancellation cannot forcibly stop application code. Results returned after the deadline are treated as timed out. Other exceptions fail the step rather than counting as false results.

Saved business arguments are reused. Check current external state inside the service method rather than expecting changing captured inputs to be rebound. Polls do not publish downstream outputs.

The built-in processor discovers due work through scans; Hangfire schedules coordination wake-ups and recovers abandoned active work. Neither promises execution at an exact instant.

An application could poll its own approval service, but authorization, audit history, and approval semantics would belong to that application. This is an integration possibility, not a supplied approval feature.

Sources: [poll contract](https://github.com/patware/Patware.Pipeline/blob/main/src/Pipeline.Core/Interfaces.cs), [coordinator](https://github.com/patware/Patware.Pipeline/blob/main/src/Pipeline.Runtime/PipelineExecutionCoordinator.cs), [scheduler](https://github.com/patware/Patware.Pipeline/blob/main/src/Pipeline.Hangfire/PipelineRunJob.cs).
