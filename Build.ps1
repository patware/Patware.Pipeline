Set-Location $PSScriptRoot

$itemsToBuild = @(
  "Pipeline.Core"
  "Pipeline.Runtime"
  "Pipeline.Persistence.EntityFrameworkCore"
  "Pipeline.Hangfire"
  "Pipeline.Blazor"
)

foreach($item in $itemsToBuild) {
    Write-Host "Building [$item]..."
    & dotnet build "src/$item/$item.csproj" --configuration Release -p:ContinuousIntegrationBuild=true

    if ($LASTEXITCODE -ne 0) {
        Write-Error "Build failed for [$item]: Error code [$LASTEXITCODE]. Aborting build."
        exit $LASTEXITCODE
    }

  & dotnet test "tests/$($item).Tests/$($item).Tests.csproj" --configuration Release -p:ContinuousIntegrationBuild=true

  if ($LASTEXITCODE -ne 0) {
      Write-Error "Unit tests failed for [$item]: Error code [$LASTEXITCODE]. Aborting build."
      exit $LASTEXITCODE
  }

  & dotnet pack "src/$($item)/$($item).csproj" --configuration Release --output artifacts/packages -p:ContinuousIntegrationBuild=true

  if ($LASTEXITCODE -ne 0) {
      Write-Error "Packaging failed for [$item]: Error code [$LASTEXITCODE]. Aborting build."
      exit $LASTEXITCODE
  }

}

