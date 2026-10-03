# Changelog

User-visible changes are recorded here. `VERSION` is the current build version;
published releases are identified by package and release date below.

## Unreleased

### Added

- Repository formatting, Git attributes, SDK selection, and Visual Studio setup.
- Shared build version sourced from `VERSION` (currently `0.1.0`).
- Contribution, branching, conduct, security, ownership, and support guidance.

### Existing baseline

- Execution runtime, in-memory storage, and retry support.
- Entity Framework Core persistence and Hangfire processing.
- Blazor monitoring components and a demo web host.
- MSTest suites for the five libraries.

## Patware.Pipeline.Core 0.1.0 - 2026-10-03

Initial release published to [NuGet.org](https://www.nuget.org/packages/Patware.Pipeline.Core/0.1.0).

### Added

- Typed workflow graphs with dependencies, conditions, output bindings, and polling.
- .NET 10 support with no package dependencies.

### Validation

- Successfully installed and built in a separate smoke-test project using the
  published package.

This release covers Core only; companion libraries have not yet been published.
