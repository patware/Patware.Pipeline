# Build and run locally

Use a stable .NET 10 SDK compatible with [global.json](https://github.com/patware/Patware.Pipeline/blob/main/global.json): configured minimum feature band 10.0.200, `latestFeature` roll-forward, no previews. Run these commands from the repository root:

```powershell
dotnet restore Pipeline.slnx
dotnet build Pipeline.slnx --configuration Release --no-restore
dotnet test Pipeline.slnx --configuration Release --no-build
```

Unit tests do not require SQL Server. The demo currently selects SQL Server and Hangfire. Configure an accessible development database, for example:

```powershell
$env:ConnectionStrings__Pipeline = 'Server=localhost;Database=PipelineDev;Integrated Security=True;TrustServerCertificate=True'
dotnet run --project src/Pipeline.Web --launch-profile https
```

Adjust for your SQL Server; certificate trust here is a local-development setting. Startup applies Pipeline migrations and prepares Hangfire storage. The identity needs schema permissions. Do not commit credentials.

Visit the printed host URL, open `/simulator`, and follow workflows at `/pipeline-runs`. Simulator controls change directory state and trigger synchronization; workflow polling uses real configured intervals. The current host maps `/hangfire` in every environment.

For a database-free host, replace the active registration in [Program.cs](https://github.com/patware/Patware.Pipeline/blob/main/src/Pipeline.Web/Program.cs) with `AddPipeline()`, remove the unconditional connection-string requirement, and remove the Hangfire dashboard mapping. Alternatively use `options.UseHangfire()` for in-memory Hangfire and retain its dashboard. In-memory history is lost on restart.

## Documentation and packages

With DocFX available on PATH:

```powershell
docfx docs/docfx.json
```

This generates API metadata for the five libraries and builds Markdown/TOCs. Edit source Markdown/XML comments; `docs/api`, `docs/_site`, and `docs/.cache` are ignored generated output. Add new pages to section `toc.yml` files.

After a Release build, package with `dotnet pack Pipeline.slnx --configuration Release --no-build --output artifacts/packages`. Shared versioning comes from [VERSION](https://github.com/patware/Patware.Pipeline/blob/main/VERSION) through [Directory.Build.props](https://github.com/patware/Patware.Pipeline/blob/main/Directory.Build.props).

Next: [authoring definitions](../architecture/PIPELINE-DEFINITIONS.md), [coding guidance](CODING-STANDARDS.md), [tests](TESTING.md), [UI](UI-GUIDELINES.md), and [contribution guide](https://github.com/patware/Patware.Pipeline/blob/main/CONTRIBUTING.md).
