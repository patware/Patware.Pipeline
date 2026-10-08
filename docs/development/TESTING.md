# Testing strategy

Six suites under tests cover Core, Runtime, Persistence, Hangfire, Blazor, and the HTTP transport boundary. AspireApp1.Tests separately verifies the distributed sample. Shared library settings configure MSTest, Fluent Assertions, NSubstitute, and Coverlet; individual projects override versions.

## Runners

global.json selects Microsoft.Testing.Platform for .NET 10. AspireApp1.Tests enables the executable MSTest runner:

```powershell
dotnet test --project src/AspireApp1/AspireApp1.Tests/AspireApp1.Tests.csproj
```

Library test projects currently retain VSTest adapters and do not enable the executable MTP runner. A single solution-wide MTP command is therefore not the documented mixed-runner path. Run library suites in Visual Studio Test Explorer, or build a suite and invoke VSTest directly:

```powershell
dotnet build tests/Pipeline.Runtime.Tests/Pipeline.Runtime.Tests.csproj
dotnet vstest tests/Pipeline.Runtime.Tests/bin/Debug/net10.0/Pipeline.Runtime.Tests.dll
```

Run the other library assemblies similarly. Keep runner-specific coverage options separate: tests/coverage.runsettings and the XPlat collector belong to the VSTest path. Do not apply those switches to the MTP Aspire command without configuring an appropriate MTP extension.

See [Microsoft runner guidance](https://learn.microsoft.com/en-us/dotnet/core/testing/unit-testing-with-dotnet-test). A unified solution command requires migrating every suite to a compatible runner; documentation changes do not perform that migration.

## Regression targets

| Suite | Targets |
| --- | --- |
| Core | Graph validation/order, bindings, conditions, freezing, serialization. |
| Runtime | Transitions, retry preservation, claims, scopes, local monitor projections, built-in registration. |
| Persistence | Store contracts, revisions, rollback, outputs, fencing, reset, registration. |
| Hangfire | Dispatch, scheduling/recovery, persisted signatures, enqueue failures. |
| Blazor | Views, filters/retry, load errors, disposal, rendering, ANSI encoding. |
| Transport | Endpoints/client round trips, status mapping, and prevention of retry POST replay under inherited policies. |
| Aspire | Backend execution and initial frontend HTML through remote monitoring. |

Tests use Arrange, Act, and Assert sections and propagate TestContext.CancellationToken. Shared/Samples.cs provides deterministic graphs and UTC readings; it does not simulate timer callbacks.

Coverage figures in older reports are historical, not fresh results. Merge reports rather than adding percentages when referenced libraries appear in several suites.

## Integration limits

EF store tests use SQLite in-memory databases; SQL Server registration tests do not connect to a server. Hangfire clients are substituted. bUnit and initial HTML tests do not establish browser reconnection or interactive failover.

The maintainer reported Docker Desktop Kubernetes validation with both API replicas and pod interruption. Record run ID, executing owner, interruption, replacement invocation, and final persisted completion when testing lease recovery. Current checked-in Aspire tests do not automate Kubernetes pod deletion.

Validate real SQL migration upgrades, contention, lease renewal/loss, and idempotent business effects when changing those areas. See [Kubernetes checks](KUBERNETES.md), [recovery semantics](../architecture/RECONCILIATION.md), and [test suite guide](https://github.com/patware/Patware.Pipeline/blob/main/tests/README.md).
