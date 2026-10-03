# Contributing

Bug reports, documentation improvements, and focused pull requests are welcome.
Participation is covered by the [Code of Conduct](CODE_OF_CONDUCT.md).

## Set up and verify

Install a stable .NET 10 SDK compatible with [global.json](global.json).
The SDK policy accepts the latest installed .NET 10 feature band starting at
10.0.200 and excludes preview SDKs. Visual Studio users can import
[.vsconfig](.vsconfig) for the ASP.NET and web development workload; use an IDE
that supports the selected SDK and `.slnx` solution format.

From the repository root:

```powershell
dotnet restore Pipeline.slnx
dotnet build Pipeline.slnx --configuration Release --no-restore
dotnet test Pipeline.slnx --configuration Release --no-build
```

The unit tests do not require SQL Server. See [tests/README.md](tests/README.md)
for coverage commands and integration-test limitations. The demo requires SQL
Server and migrations; follow [README.md](README.md).

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
dotnet pack Pipeline.slnx --configuration Release --no-build --output artifacts/packages
```

Use [GitHub issues](https://github.com/patware/Patware.Pipeline/issues) for
reproducible bugs and feature proposals. Include the package version, SDK,
expected behavior, actual behavior, and a minimal reproduction. Report
vulnerabilities using [SECURITY.md](SECURITY.md).
