# Blazor integration

Pipeline.Blazor consumes Pipeline.Contracts.IPipelineMonitor through server-side DI. Use AddPipeline for a local executor/renderer or AddPipelineClient for a remote renderer. Blazor has no Runtime or Core dependency.

## Discovery

Call AddPipelinePages on the MapRazorComponents builder, followed by AddInteractiveServerRenderMode. Use PipelineRouter in Routes.razor to include library pages while retaining your layout and route handling. A framework Router may alternatively set AdditionalAssemblies explicitly.

See the [package integration examples](https://github.com/patware/Patware.Pipeline/blob/main/src/Pipeline.Blazor/README.md).

| Route | Behaviour |
| --- | --- |
| /pipeline | Latest 50 summaries and ordered job statuses; 20-second refresh. |
| /pipeline/run/{RunId:guid} | Jobs, steps, logs; one-second active and 20-second inactive refresh. |

Bootstrap 5 and the host's generated CSS isolation bundle supply the styles.

## Lifecycle

LivePipelinePage serializes snapshot loads, loads on parameter changes, starts periodic refresh after rendering, and cancels/awaits refresh during async disposal. It suppresses finalization as part of DisposeAsync.

Loading failures are logged and expose a generic error while retaining previously published data. The timer continues. Parameter versions protect loading/error state; detail loads discard data when navigation changed RunId during a request.

Keep these protections when adding pages. A missing run, a loading failure, and a pending initial load are different states.

## Logs and retry

Detail supports ?job=...&step=.... Step selection requires a valid job/step. Invalid filters display errors and filtered logs retain their original numbering. Escape query values when constructing links.

AnsiLogText converts supported formatting into spans while Razor encodes text. Never render messages with raw HTML or MarkupString. Test malformed controls, hostile text, whitespace, and resets.

Retry disables duplicate clicks and handles false returns or exceptions. HTTP retry commands are not automatically replayed. Hosts still own authorization for viewing and retrying runs.

## Distributed limits

The UI polls the monitor rather than consuming lifecycle events. Process-local handler scopes differ from circuit scopes, and events are not a cross-process bus.

Use sticky frontend sessions and shared Data Protection keys for multiple Interactive Server replicas. Existing circuits do not move to another pod after failure.

Summaries omit logs and step details, but the backend still loads snapshots per listed run; detail logs remain unbounded. See [distributed hosting](../architecture/DISTRIBUTED-HOSTING.md).
