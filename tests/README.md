# Tests

Each library has an MSTest project under `tests/`, included in `Pipeline.slnx`.
`Pipeline.Web` is intentionally excluded. All projects use Fluent Assertions and
NSubstitute; common versions and test settings are in `Directory.Build.props`.

## Run

Requires the .NET 10 SDK. From the repository root:

```powershell
dotnet test Pipeline.slnx
```

Run one suite:

```powershell
dotnet test tests/Pipeline.Runtime.Tests/Pipeline.Runtime.Tests.csproj
```

Collect coverage for the five libraries (excluding test assemblies and generated
build files):

```powershell
dotnet test Pipeline.slnx --settings tests/coverage.runsettings --collect:"XPlat Code Coverage" --results-directory TestResults
```

Coverlet writes one `coverage.cobertura.xml` per test project beneath
`TestResults/`. Reports also include referenced libraries: merge reports with a
Cobertura-compatible reporting tool for aggregate coverage; do not add their
percentages together. Generated results are ignored by Git.

## Coverage and design

Verified baseline: **179 passing tests**, no skipped tests. The following figures
are each library's coverage from its corresponding test project, not an aggregate
of transitive coverage from other suites.

| Library | Tests | Line coverage | Branch coverage |
| --- | ---: | ---: | ---: |
| Core | 21 | 93.16% | 81.54% |
| Runtime | 40 | 88.81% | 76.69% |
| Persistence.EntityFrameworkCore | 24 | 93.59% | 84.61% |
| Hangfire | 15 | 89.00% | 78.57% |
| Blazor | 79 | 93.57% | 90.76% |

- **Core:** graph construction and ordering, typed output bindings, conditions,
  transitive dependencies, cycles, invalid expressions, duplicate registrations,
  immutable builder lifecycle, request serialization, and example pipelines.
- **Runtime:** real coordinator, binder, invoker, and in-memory store working
  together through DI, with substituted business services. Covers success,
  exceptions, condition skips, blocked dependencies, polling and deadlines,
  retries preserving completed work, stale dispatch, scoped logging, registration,
  registry versioning, serialized operations, and worker shutdown.
- **Persistence:** a shared contract suite runs against both the in-memory store
  and EF with a fresh SQLite in-memory database per test. Covers graph and metadata
  round trips, optimistic revisions, append-only logs, rollback after rejected
  updates, output publication, lease fencing, scoped log validation, cancellation,
  and cascading reset. NSubstitute supplies the context factory; database queries
  and transactions use actual EF contexts. SQL Server registration is checked
  without connecting to a server.
- **Hangfire:** substituted job clients verify job type, arguments, scheduling,
  recovery filtering, duplicate-wakeup avoidance, persisted compatibility
  signatures, terminal-run no-ops, startup, and dispatch failure recovery.
- **Blazor:** bUnit exercises pages, query-string log selection, retry actions,
  polling countdowns, and lifecycle behavior. The framework HTML renderer tests
  reusable components and HTML encoding. ANSI parsing covers standard, bright,
  indexed and RGB colors, selective resets, malformed controls, and whitespace.
  JS interop tests cover lazy module import, arguments, and disposal.

`Shared/Samples.cs` contains deterministic sample graphs and a controllable clock.
`Shared/StoreContractTests.cs` is linked into the two store test projects so the
same public contract is checked for both implementations. Timing tests use fixed
clock readings or task-completion signals rather than sleeps. The simple clock
controls `GetUtcNow`; it does not simulate timer callbacks.

## Scope limits

These suites do not start SQL Server or a Hangfire server, or launch a browser.
SQLite cannot establish SQL Server-specific query, migration, locking, or
multi-process concurrency behavior. In particular, DateTimeOffset ordering is
covered in the in-memory store, not asserted against SQLite. Actual SQL Server
paging and active-run queries need a SQL Server integration environment.
Automatic schema initialization, migration upgrades, and startup ordering with 
real SQL Server and Hangfire require integration validation. 
SQLite store tests do not validate the bundled SQL Server migrations.

Long-running lease renewal, background refresh timers, browser reconnection, and
multi-worker races still warrant integration tests. The coverage figures describe
executed code, not proof against every interleaving.
