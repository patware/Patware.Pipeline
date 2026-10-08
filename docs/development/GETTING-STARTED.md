# Build and run locally

Use a stable .NET 10 SDK compatible with [global.json](https://github.com/patware/Patware.Pipeline/blob/main/global.json): configured minimum feature band 10.0.200, `latestFeature` roll-forward, no previews. Run these commands from the repository root:

```powershell
dotnet restore Pipeline.slnx
dotnet build Pipeline.slnx --configuration Release --no-restore
# See TESTING.md for separate library and Aspire runner commands.
```

Unit tests do not require SQL Server. The demo currently selects SQL Server and Hangfire. Configure an accessible development database, for example:

```powershell
$env:ConnectionStrings__Pipeline = 'Server=localhost;Database=PipelineDev;Integrated Security=True;TrustServerCertificate=True'
dotnet run --project src/Pipeline.Web --launch-profile https
```

Adjust for your SQL Server; certificate trust here is a local-development setting. Startup applies Pipeline migrations and prepares Hangfire storage. The identity needs schema permissions. Do not commit credentials.

Visit the printed host URL, open `/simulator`, and follow workflows at `/pipeline`. Simulator controls change directory state and trigger synchronization; workflow polling uses real configured intervals. The current host maps `/hangfire` in every environment.

For a database-free host, replace the active registration in [Program.cs](https://github.com/patware/Patware.Pipeline/blob/main/src/Pipeline.Web/Program.cs) with `AddPipeline()`, remove the unconditional connection-string requirement, and remove the Hangfire dashboard mapping. Alternatively use `options.UseHangfire()` for in-memory Hangfire and retain its dashboard. In-memory history is lost on restart.

## Documentation and packages

With DocFX available on PATH:

```powershell
dotnet build Pipeline.slnx --configuration Release
docfx docs/docfx.json
```

This generates API metadata from the eight compiled Release assemblies and adjacent XML documentation, then builds Markdown/TOCs. Assembly input avoids incompatibilities between DocFX's bundled compiler and the SDK Razor generator. Edit source Markdown/XML comments and rebuild; `docs/api`, `docs/_site`, and `docs/.cache` are ignored generated output. Add new pages to section `toc.yml` files.

The prepared release version is 0.3.0, sourced from [VERSION](https://github.com/patware/Patware.Pipeline/blob/main/VERSION) through [Directory.Build.props](https://github.com/patware/Patware.Pipeline/blob/main/Directory.Build.props). Rebuild after changing VERSION before packing. Pack the eight library projects explicitly; the solution also contains sample hosts and tests:

```powershell
Get-ChildItem src -Directory -Filter 'Pipeline.*' |
    Where-Object {
        $_.Name -ne 'Pipeline.Web' -and
        (Test-Path -LiteralPath (Join-Path $_.FullName ($_.Name + '.csproj')))
    } |
    ForEach-Object {
        $project = Join-Path $_.FullName ($_.Name + '.csproj')
        dotnet pack $project --configuration Release --output artifacts/packages
        if ($LASTEXITCODE -ne 0) { throw "Packing failed: $project" }
    }
```

Inspect package versions, dependencies, bundled README files, and release notes before publishing. 0.3.0 includes Blazor view/route changes: see [CHANGELOG.md](https://github.com/patware/Patware.Pipeline/blob/main/CHANGELOG.md). Packing creates local artifacts and does not publish packages.

## Separate hosts and Kubernetes

Run the Aspire AppHost with Docker available for Redis:

```powershell
dotnet run --project src/AspireApp1/AspireApp1.AppHost/AspireApp1.AppHost.csproj
```

The API owns execution; the frontend uses AddPipelineClient and PipelineRouter. Local defaults use in-memory storage unless a Pipeline SQL connection is supplied. See [distributed hosting](../architecture/DISTRIBUTED-HOSTING.md), the [sample README](https://github.com/patware/Patware.Pipeline/blob/main/src/AspireApp1/README.md), and [Kubernetes verification](KUBERNETES.md).

Next: [authoring definitions](../architecture/PIPELINE-DEFINITIONS.md), [coding guidance](CODING-STANDARDS.md), [tests](TESTING.md), [UI](UI-GUIDELINES.md), and [contribution guide](https://github.com/patware/Patware.Pipeline/blob/main/CONTRIBUTING.md).
