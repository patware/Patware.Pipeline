# Persistence and schema

The default singleton [InMemoryPipelineStore](https://github.com/patware/Patware.Pipeline/blob/main/src/Pipeline.Runtime/InMemoryPipelineStore.cs) implements run, execution, output, and reset contracts. Its data disappears with the process. SQL Server replaces these with EF implementations using short-lived contexts from `IDbContextFactory<PipelineDbContext>`.

## SQL model

All Pipeline tables use the `pipeline` schema.

| Table | Key | Contents |
| --- | --- | --- |
| PipelineRuns | Id | Definition, title, submitter, status, timestamps, revision |
| PipelineSpecifications | RunId | Definition ID/version, input/settings JSON |
| PipelineJobs | RunId, JobId | Status, condition result, timing, error, revision |
| PipelineSteps | RunId, JobId, StepId | Attempts, arguments/output, deadlines, lease, state, error, revision |
| PipelineRunLogs | RunId, Sequence | Ordered messages, severity, job/step scope |
| SchemaVersions | EF migration history key | Applied library migrations |

Runs own specifications, jobs, and logs; jobs own steps. Foreign keys cascade deletion. Indexes support run ordering, active discovery, and step due-state lookup. Definition/job/step IDs have 200-character database limits; fluent nonblank checks do not enforce every SQL length limit.

## Transactions

Creation saves the run, specification, initial graph, and logs together. Execution snapshot reads use a short serializable transaction. `TrySaveAsync` advances the run revision through an EF concurrency token, validates the required claim, updates the graph, appends a log, and commits. Rejected claim checks roll back. External service calls remain outside database transactions.

Metadata updates reject changes to the existing log prefix. This is application-level append-only behavior, not a tamper-proof audit store. Run queries include all logs, including list queries, so large histories increase read costs.

## Initialization and deletion

`UseSqlServer` registers `PipelineDatabaseInitializer`. Its `StartingAsync` applies bundled migrations before hosted processing starts, using library-controlled migration assembly/history configuration. Initialization failure prevents normal startup; the database identity needs sufficient schema permissions.

The baseline is `InitialPipelineSchema`. Earlier host-owned migration histories are not automatically converted; see [CONTRIBUTING.md](https://github.com/patware/Patware.Pipeline/blob/main/CONTRIBUTING.md).

`ResetAsync` deletes every run and dependent record, returning the run count. It is neither retention nor per-run cancellation. The gate protects local operations only; remote workers and external effects are not stopped or undone. Separate Hangfire job storage is not cleared by this reset.

Sources: [model](https://github.com/patware/Patware.Pipeline/blob/main/src/Pipeline.Persistence.EntityFrameworkCore/PipelineDbContext.cs), [execution store](https://github.com/patware/Patware.Pipeline/blob/main/src/Pipeline.Persistence.EntityFrameworkCore/EfPipelineExecutionStore.cs), [registration](https://github.com/patware/Patware.Pipeline/blob/main/src/Pipeline.Persistence.EntityFrameworkCore/PipelineEntityFrameworkCoreExtensions.cs), [reset](https://github.com/patware/Patware.Pipeline/blob/main/src/Pipeline.Persistence.EntityFrameworkCore/EfPipelineResetService.cs).
