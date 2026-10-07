# Testing strategy

Five MSTest projects cover the libraries; no Pipeline.Web test project exists. [Shared settings](https://github.com/patware/Patware.Pipeline/blob/main/tests/Directory.Build.props) configure Fluent Assertions, NSubstitute, and Coverlet.

```powershell
dotnet test Pipeline.slnx
dotnet test tests/Pipeline.Runtime.Tests/Pipeline.Runtime.Tests.csproj
dotnet test Pipeline.slnx --settings tests/coverage.runsettings --collect:"XPlat Code Coverage" --results-directory TestResults
```

Coverage is per-project Cobertura XML. Referenced libraries can appear in multiple reports; merge reports instead of adding percentages. Historical counts/coverage in existing documentation are not current execution results.

| Suite | Regression targets |
| --- | --- |
| Core | Graph cycles/order, ancestry, expression restrictions, freezing, serialization |
| Runtime | Transitions, polling, retry preservation, stale dispatch, scopes, logs/events |
| Persistence | Shared store contract, revisions, rollback, outputs, claims, reset, registration |
| Hangfire | Dispatch arguments, scheduling/recovery, compatibility signatures, enqueue failure |
| Blazor | bUnit pages, filters/retry, rendering, ANSI encoding, disposal |

[Samples.cs](https://github.com/patware/Patware.Pipeline/blob/main/tests/Shared/Samples.cs) contains graphs and a controllable clock. It controls UTC readings, not timer callbacks. Prefer deterministic time changes/task signals over sleeps. [StoreContractTests.cs](https://github.com/patware/Patware.Pipeline/blob/main/tests/Shared/StoreContractTests.cs) is shared by in-memory and EF suites.

## Integration limits

EF tests use SQLite in-memory databases; SQL Server registration tests do not connect to a server. Hangfire clients are substituted; Blazor tests do not start a browser. Passing these suites does not establish SQL Server migrations/locking, real scheduler execution, browser reconnect, or multi-process race behavior.

For changes in those areas, validate a real SQL Server host: fresh/upgrade migrations, active-run queries/paging, crash/restart during claims, lease renewal/loss, duplicate dispatch, and external idempotency. UI changes also need interactive navigation/refresh/reconnect checks. Record which scenarios and environments were actually exercised.

See [tests/README.md](https://github.com/patware/Patware.Pipeline/blob/main/tests/README.md) and [recovery semantics](../architecture/RECONCILIATION.md).
