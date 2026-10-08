# Changelog

User-visible changes are recorded here. `VERSION` is the current build version;
published releases are identified by package and release date below.

## Unreleased

No changes recorded after the 0.3.0 release preparation.

## 0.3.0 - Pending publication

### Added

- `Patware.Pipeline.Contracts`: transport-independent `IPipelineMonitor`, display enums, run summaries, and execution views.
- `Patware.Pipeline.AspNetCore`: library-owned run-list, execution, and retry endpoints under `/api/pipeline`.
- `Patware.Pipeline.HttpClient`: `AddPipelineClient` for remote monitoring, fixed protocol JSON, missing-run handling, and resilience that does not automatically retry POST commands.
- `AddPipelinePages` and `PipelineRouter` for discovering library pages while retaining the host's layout and navigation configuration.
- Automatic registration of `NoOpStep`, `ExampleStep`, `LogFormattingStep`, `LogFormattingPipeline`, and `LogFormattingRegistration` in `AddPipeline`.
- Opt-in `RunLogFormattingDemoOnStartup` configuration; each enabled host startup submits a new demonstration run.
- Aspire sample with separate renderer and executor, transport tests, and an Aspire initial-rendering integration test.
- Dockerfiles and Kubernetes manifests for two frontend and two backend replicas, shared SQL Server persistence, Redis-backed Data Protection keys, and Traefik frontend session affinity.

### Changed

- Blazor depends on Contracts rather than Runtime/Core and queries `IPipelineMonitor` through local or HTTP implementations.
- Monitoring pages use `/pipeline` and `/pipeline/run/{RunId:guid}`.
- Overview responses contain run summaries and ordered job statuses without logs or step details.
- Snapshot refresh failures retain existing data, expose a generic error, and permit subsequent refresh; stale detail loads are discarded after navigation.
- Shared package version is `0.3.0`; documentation and API generation cover all eight libraries.

### Upgrade notes

- Remove explicit built-in definition registrations: appending them after `AddPipeline` can produce a duplicate definition ID/version.
- Update old page links and custom Blazor code using runtime execution types to use Contracts display types.
- Executor hosts call `AddPipeline` and `MapPipelineEndpoints`; remote renderer hosts call `AddPipelineClient` and `AddPipelinePages` and use `PipelineRouter`.
- Register host-wide HTTP defaults before `AddPipelineClient`. Its targeted experimental-API suppression replaces inherited resilience handlers to prevent command replay.
- Use shared durable persistence for multiple executor replicas. Definitions and historical versions must be available on every replica.

### Validation and limits

- Maintainer reported passing tests, continued operation of Pipeline.Web, and successful Docker Desktop Kubernetes operation with submissions through both API replicas and API pod interruption.
- Cross-replica lease recovery should be evidenced by one run's persisted logs before and after loss of its executing pod; the overview alone does not establish execution ownership.
- No exactly-once external-effect guarantee, durable lifecycle-event delivery, seamless Blazor circuit migration, or SQL/Redis high-availability guarantee is introduced.

## 0.2.0 - 2026.10.06

### Added

- Repository formatting, Git attributes, SDK selection, and Visual Studio setup.
- Shared build version sourced from `VERSION`.
- Contribution, branching, conduct, security, ownership, and support guidance.
- Pipeline Runs now includes a Jobs column with one status icon per job,
  displayed in pipeline order. Icons link directly to job logs and include
  job names and statuses in tooltips and screen-reader labels. Running jobs
  use animated icons that respect reduced-motion preferences.
- Added pipeline run lifecycle notifications for Queued, Started, Completed,
  Failed, and RetryRequested transitions.
- Applications can register scoped IPipelineRunEventHandler implementations
  to react to persisted transitions with either the built-in processor or Hangfire.
- Notifications include run identity, definition, title, submitter, committed
  revision, status, timestamp, and status text.
- Added injectable IPipelineStepContext for step services to access the
  current run ID, pipeline definition and version, title, submitter,
  run creation timestamp, job ID, and step ID.
- Context is initialized before step construction and scoped to each
  invocation, enabling attribution of business changes to pipeline runs.

### Changed

- SQL Server persistence now ships and automatically applies library-owned migrations before hosted workers start.
- Pipeline tables use the `pipeline` schema, with applied migrations tracked in `pipeline.SchemaVersions`.
- Consolidated the initial development migration history into `InitialPipelineSchema` in the persistence library.
- Removed the requirement for consuming applications to configure or apply pipeline migrations.
- Pipeline.Web now filters Entity Framework Core development logs to Warning
  and above, hiding routine SQL output while retaining warnings and errors.

### Existing baseline

- Execution runtime, in-memory storage, and retry support.
- Entity Framework Core persistence and Hangfire processing.
- Blazor monitoring components and a demo web host.
- MSTest suites for the five libraries.

### Compatibility

- Databases created using the previous host-owned migration history require a fresh development database or an explicit conversion plan.

## Patware.Pipeline.Core 0.1.0 - 2026-10-03

Initial release published to [NuGet.org](https://www.nuget.org/packages/Patware.Pipeline.Core/0.1.0).

### Added

- Typed workflow graphs with dependencies, conditions, output bindings, and polling.
- .NET 10 support with no package dependencies.

### Validation

- Successfully installed and built in a separate smoke-test project using the
  published package.

This release covers Core only; companion libraries have not yet been published.
