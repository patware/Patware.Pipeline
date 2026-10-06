# Changelog

User-visible changes are recorded here. `VERSION` is the current build version;
published releases are identified by package and release date below.

## Unreleased

### Added

- Repository formatting, Git attributes, SDK selection, and Visual Studio setup.
- Shared build version sourced from `VERSION` (currently `0.1.0`).
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
