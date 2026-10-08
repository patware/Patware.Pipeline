# Security and host responsibilities

Runtime APIs do not implement authentication or tenant authorization. Run IDs and caller-supplied `CreatedBy` values are metadata, not access-control decisions. Query, retry, and reset contracts accept no authorization principal.

The HTTP monitoring endpoints under /api/pipeline likewise require host policies. MapPipelineEndpoints returns a route group for authorization configuration. Responses expose display views and generic error messages, but that is not an access-control boundary. The Aspire /demo submission routes are sample-owned and explicitly enabled. Shared Data Protection keys and circuit affinity are frontend deployment responsibilities.

The demo [Program.cs](https://github.com/patware/Patware.Pipeline/blob/main/src/Pipeline.Web/Program.cs) enables HTTPS redirection, antiforgery, and production HSTS/error handling, but no application authentication/authorization policy. `/hangfire` is mapped unconditionally with Hangfire's dashboard defaults; source does not restrict the mapping to Development. Deployed hosts must configure access to workflow pages, submission, retry, reset, and dashboard operations.

Inputs, settings, arguments, outputs, and logs can contain sensitive data. The library does not redact or encrypt these fields. Prefer secret references and host-managed credentials. Database access, backups, transport protection, and retention are deployment responsibilities.

Definitions and step services are trusted code. Expression validation constrains supported binding syntax; it is not a sandbox. Property getters can execute application code and services run with host privileges.

ANSI rendering uses parser-generated CSS and Razor-encoded text. Preserve this boundary instead of rendering messages as raw HTML. Append-only checks protect normal store operations, not direct database modification.

Sources: [request](https://github.com/patware/Patware.Pipeline/blob/main/src/Pipeline.Core/PipelineRequest.cs), [validator](https://github.com/patware/Patware.Pipeline/blob/main/src/Pipeline.Core/ArgumentExpressionValidator.cs), [encoded rendering](https://github.com/patware/Patware.Pipeline/blob/main/src/Pipeline.Blazor/Components/AnsiLogText.razor), [disclosure policy](https://github.com/patware/Patware.Pipeline/blob/main/SECURITY.md).
