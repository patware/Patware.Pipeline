# Contributing

Bug reports, documentation improvements, and focused pull requests are welcome.
Participation is covered by the [Code of Conduct](CODE_OF_CONDUCT.md).

## Set up and verify

Install a stable .NET 10 SDK compatible with [global.json](global.json).
The SDK policy accepts the latest installed .NET 10 feature band starting at
10.0.200 and excludes preview SDKs. Visual Studio users can import
[.vsconfig](.vsconfig) for the ASP.NET and web development workload; use an IDE
that supports the selected SDK and `.slnx` solution format.

The PowerShell scripts require PowerShell 7 or later

From the repository root:

```powershell
$packages = ./Build.ps1
./eng/Test-Packages.ps1 -PackageDirectory $packages
```

The unit tests do not require SQL Server. See [tests/README.md](tests/README.md)
for coverage commands and integration-test limitations. The demo requires SQL Server and initializes its schema automatically; follow README.md.

## Make a change

- Follow [.editorconfig](.editorconfig) and surrounding code conventions.
- Add or update tests for changed behavior, and update relevant documentation.
- Keep credentials, connection strings containing secrets, build output, and
  generated reports out of commits.
- Record user-visible changes under Unreleased in [CHANGELOG.md](CHANGELOG.md).
- Follow the [branch guide](branch-guide.md). Describe the problem, solution,
  tests run, and any remaining limitations in the pull request.

`Directory.Build.props` reads the version from `VERSION`; library projects
inherit it. The test-level props explicitly import the root settings. Change
`VERSION` once when preparing a release rather than adding project-specific
version values. Dependencies retain their own versions in the project files.

To inspect release packages after a successful Release build:

```powershell
# Include Docker-dependent Aspire tests.
$packages = ./Build.ps1 -IncludeAspire
./eng/Test-Packages.ps1 -PackageDirectory $packages
```

Use [GitHub issues](https://github.com/patware/Patware.Pipeline/issues) for
reproducible bugs and feature proposals. Include the package version, SDK,
expected behavior, actual behavior, and a minimal reproduction. Report
vulnerabilities using [SECURITY.md](SECURITY.md).

## Changing the persistence model

The persistence library owns pipeline migrations. Consuming applications
never generate them.

After changing the persistence model, generate a migration from the repository
root:

```powershell
dotnet ef migrations add DescribeYourChange --project src/Pipeline.Persistence.EntityFrameworkCore --startup-project src/Pipeline.Web --context PipelineDbContext --output-dir Migrations
```

Commit the migration, its designer file, and the updated model snapshot together.

Before releasing, check for model changes without a migration:

```powershell
dotnet ef migrations has-pending-model-changes --project src/Pipeline.Persistence.EntityFrameworkCore --startup-project src/Pipeline.Web --context PipelineDbContext
```

Verify initialization against a fresh SQL Server database and verify that a
second startup preserves existing data. For subsequent releases, also verify
upgrading a database created by the previous release.

The initial development migrations were consolidated into
`InitialPipelineSchema`. Databases created from the earlier host-owned migration
history require a fresh development database or an explicit conversion plan.
The new baseline does not automatically convert that old history.
