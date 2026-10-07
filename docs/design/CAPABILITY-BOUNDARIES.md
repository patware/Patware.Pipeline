# Capability boundaries

An enum, interface name, or demo placeholder does not establish a complete feature.

| Capability | Current implementation |
| --- | --- |
| Dependencies/branching | Static job DAG, one output-based condition per job |
| Parallelism | One live step per run; global gate further serializes a process |
| Waiting | Persisted Boolean polling with interval/deadline |
| Approvals/signals/callbacks | No first-class implementation |
| Retry | Explicit failed-run reopening, preserving successes |
| Cancellation | Cooperative invocation/host cancellation; no user cancel-run API |
| Recovery | Active discovery and lease reclamation; durable only with durable storage |
| External effects | Repetition possible; no compensation/exactly-once guarantee |
| Persistence | In-memory or SQL Server; SQLite used in tests |
| Events | Best-effort process-local notifications |
| Security/tenancy | Host-owned; no per-run principal or tenant policy |
| Monitoring | Blazor polling and stored logs; no metrics exporter |
| Retention | All-runs reset; no age-based retention |
| Demo | Simulated directory/Graph/Teams; other MyPipelines operations remain placeholders |

Future proposals should address persistence, restart behavior, ownership, compatibility, and tests. See [architecture](../architecture/ARCHITECTURE.md), [state semantics](../architecture/STATE-MACHINES.md), and [test limits](../development/TESTING.md).
