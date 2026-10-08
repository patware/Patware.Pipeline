Set-Location $PSScriptRoot

$version = get-content "VERSION" | select-object -first 1

$artifactsPath = Join-Path ".\artifacts\packages" -ChildPath "v$version"

if (Test-Path $artifactsPath)
{
  Write-Error "Artifacts path [$artifactsPath] already exists. Please remove it before running the build script."
  exit 1
}

New-Item $artifactsPath -ItemType Directory -Force

$itemsToBuild = @(
  "Pipeline.Core"
  "Pipeline.Contracts"
  "Pipeline.Runtime"
  "Pipeline.Persistence.EntityFrameworkCore"
  "Pipeline.Hangfire"
  "Pipeline.Blazor"
  "Pipeline.AspNetCore"
  "Pipeline.HttpClient"
)

foreach($item in $itemsToBuild)
{
  Write-Host "Building [$item]..."
  & dotnet build "src/$item/$item.csproj" --configuration Release -p:ContinuousIntegrationBuild=true

  if ($LASTEXITCODE -ne 0) {
      Write-Error "Build failed for [$item]: Error code [$LASTEXITCODE]. Aborting build."
      exit $LASTEXITCODE
  }

  if (Test-Path "tests/$($item).Tests/$($item).Tests.csproj")
  {
    Write-Host "Running unit tests for [$item]..."
    & dotnet test "tests/$($item).Tests/$($item).Tests.csproj" --configuration Release -p:ContinuousIntegrationBuild=true

    if ($LASTEXITCODE -ne 0) {
        Write-Error "Unit tests failed for [$item]: Error code [$LASTEXITCODE]. Aborting build."
        exit $LASTEXITCODE
    }
  }
  else
  {
      Write-Host "No unit tests found for [$item]. Skipping test execution."
  }

  & dotnet pack "src/$($item)/$($item).csproj" --configuration Release --output $artifactsPath -p:ContinuousIntegrationBuild=true

  if ($LASTEXITCODE -ne 0) {
      Write-Error "Packaging failed for [$item]: Error code [$LASTEXITCODE]. Aborting build."
      exit $LASTEXITCODE
  }

}

