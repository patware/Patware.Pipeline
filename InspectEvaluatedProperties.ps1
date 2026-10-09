$projects = @(
    "Pipeline.Core"
    "Pipeline.Contracts"
    "Pipeline.Runtime"
    "Pipeline.Persistence.EntityFrameworkCore"
    "Pipeline.Hangfire"
    "Pipeline.Blazor"
    "Pipeline.AspNetCore"
    "Pipeline.HttpClient"
)

$evaluatedProperties = foreach ($project in $projects) {
    Write-Host "`n$project"

    $evaluatedProperty = & dotnet msbuild "src/$project/$project.csproj" -getProperty:PackageId,Version,PackageReleaseNotes | ConvertFrom-Json

    if ($LASTEXITCODE -ne 0) {
        throw "Property evaluation failed for $project."
    }

    $evaluatedProperty.Properties
}

$evaluatedProperties
