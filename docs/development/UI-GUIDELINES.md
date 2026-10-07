# Blazor integration

`Pipeline.Blazor` queries `IPipelineRuntime`. The demo uses interactive server rendering. Add its assembly to both endpoint discovery and Router `AdditionalAssemblies`, following [Program.cs](https://github.com/patware/Patware.Pipeline/blob/main/src/Pipeline.Web/Program.cs) and [Routes.razor](https://github.com/patware/Patware.Pipeline/blob/main/src/Pipeline.Web/Components/Routes.razor).

| Route | Behavior |
| --- | --- |
| `/pipeline-runs` | Latest 50 runs and execution snapshots; 20-second refresh |
| `/pipeline-runs/{RunId:guid}` | Jobs, steps, logs; one-second active and 20-second terminal refresh |

## Lifecycle

`LivePipelinePage` loads on parameter changes and starts a periodic timer after first render. It serializes loads, invokes rendering through `InvokeAsync`, and cancels/awaits work on disposal. Detail loads discard results if navigation changed `RunId` during the request. Preserve these protections.

The UI polls rather than consuming lifecycle events. Handler scopes differ from circuit scopes. Event-driven UI would require an application notification service and cross-process transport where applicable.

## Logs and retry

Detail supports `?job=...&step=...`. Step selection requires a valid job/step; invalid filters display errors. Filtered logs retain original sequence numbering. Escape query values when constructing links.

`AnsiLogText` converts supported formatting to spans while Razor encodes text. Do not render messages with raw HTML or `MarkupString`. Test malformed ANSI, hostile text, whitespace, and resets when changing rendering.

Failed-run retry disables duplicate clicks, handles false returns/exceptions, and reloads state. Preserve missing-run, empty-log, and invalid-filter presentation. A click is not evidence that retry succeeded.

Components do not enforce authorization; hosts protect queries and mutations. Full snapshots/logs and per-run list loading may grow expensive; refresh-rate changes alone do not provide server-side log pagination.

Sources: [base page](https://github.com/patware/Patware.Pipeline/blob/main/src/Pipeline.Blazor/Pages/LivePipelinePage.cs), [detail](https://github.com/patware/Patware.Pipeline/blob/main/src/Pipeline.Blazor/Pages/PipelineRun.razor), [list](https://github.com/patware/Patware.Pipeline/blob/main/src/Pipeline.Blazor/Pages/PipelineRuns.razor.cs), [ANSI component](https://github.com/patware/Patware.Pipeline/blob/main/src/Pipeline.Blazor/Components/AnsiLogText.razor), [page tests](https://github.com/patware/Patware.Pipeline/blob/main/tests/Pipeline.Blazor.Tests/PageTests.cs).
