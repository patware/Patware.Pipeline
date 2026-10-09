# Tests

Six library suites live under tests: Core, Runtime, Persistence.EntityFrameworkCore, Hangfire, Blazor, and Transport. AspireApp1.Tests under src/AspireApp1 verifies separate-process hosting. All appear in Pipeline.slnx; there is no Pipeline.Web test project.

## Run library tests

The library test projects enable the MSTest executable runner.
global.json selects Microsoft.Testing.Platform for dotnet test.

Run one suite:

```powershell
dotnet test --project tests/Pipeline.Runtime.Tests/Pipeline.Runtime.Tests.csproj --configuration Release -- --minimum-expected-tests 1
```

Build the eight libraries, run every library suite, and pack:

```powershell
$packages = ./Build.ps1
```

Build.ps1 discovers test projects independently of library names, so it
includes Pipeline.Transport.Tests. Any failed suite stops packaging.
Each suite must discover at least one test.

## Run Aspire tests

global.json selects Microsoft.Testing.Platform, and AspireApp1.Tests enables its executable MSTest runner:

```powershell
dotnet test --project src/AspireApp1/AspireApp1.Tests/AspireApp1.Tests.csproj
```

Docker must be available for Aspire's Redis resource. These tests start the sample application, so they are separate from infrastructure-free library suites. The initial-rendering test verifies that the frontend displays a run executed by the backend.

A single solution-wide MTP invocation requires every test project to use MTP.  See [runner documentation](https://learn.microsoft.com/en-us/dotnet/core/testing/unit-testing-with-dotnet-test).

## Coverage and conventions

The existing coverage.runsettings file and XPlat Code Coverage collector apply to VSTest. For a built library assembly:

```powershell
dotnet vstest tests/Pipeline.Runtime.Tests/bin/Debug/net10.0/Pipeline.Runtime.Tests.dll --Settings:tests/coverage.runsettings --Collect:"XPlat Code Coverage" --ResultsDirectory:TestResults
```

Do not apply VSTest coverage switches to the Aspire MTP command. Configure MTP coverage separately if needed. Referenced assemblies may appear in multiple reports; merge them rather than adding percentages.

Tests use Arrange, Act, and Assert sections and pass TestContext.CancellationToken through asynchronous operations. Shared/Samples.cs provides deterministic graphs and a controllable UTC clock; Shared/StoreContractTests.cs checks both in-memory and EF stores.

## What the suites establish

- Core: graphs, dependencies, conditions, binding expressions, serialization, and examples.
- Runtime: coordination, retry, claims, scoped context/logging, monitor projection, registrations, and shutdown.
- Persistence: real EF/SQLite store contract tests, revisions, rollback, output publication, leases, and reset.
- Hangfire: substituted clients checking dispatch, schedules, recovery, and compatibility.
- Blazor: bUnit and HTML rendering, view contracts, error handling, navigation-related stale loads, retry, ANSI safety, and disposal.
- Transport: in-process TestServer checks of library endpoints and the HTTP monitor, including inherited resilience policies.
- Aspire: actual backend/frontend processes and initial server rendering.

Earlier documented baseline: 179 tests across the original five suites. That count and its coverage percentages predate the transport/refactoring work and are not current release verification results.

## Limits

SQLite tests do not establish SQL Server-specific migrations, queries, or locking. Substituted Hangfire clients do not establish scheduler execution. Initial rendering does not establish browser circuit reconnection.

The maintainer reported successful Kubernetes operation during API pod interruption. Kubernetes failure injection remains a separate validation scenario; record actual execution owners and persisted completion for a specific run. No checked-in test shown here automates pod deletion.

See [distributed hosting](../docs/architecture/DISTRIBUTED-HOSTING.md) and [Kubernetes verification](../docs/development/KUBERNETES.md). External idempotency, long-running leases, competing workers, migration upgrades, and browser failover deserve environment-specific checks.
