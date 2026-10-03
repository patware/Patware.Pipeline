push-location $PSScriptRoot

if (!(Test-Path .\.gitignore))
{
    Write-Host ".gitignore from dotnet new gitignore"
    & dotnet new gitignore

}


if (!(Test-Path .\.editorconfig))
{
    Write-Host ".editorconfig from dotnet new editorconfig"
    & dotnet new editorconfig
}

if (!(Test-Path .\Pipeline.slnx))
{
    Write-Host "Pipeline.slnx from dotnet new sln"
    & dotnet new sln
}

if (!(Test-Path .\src\))
{
    Write-Host ".\src folder"
    New-Item -Path .\src -ItemType Directory
}

if (!(Test-Path .\src\Pipeline.Web\))
{
    Write-Host "New blazor Pipeline.Web from dotnet new blazor"
    & dotnet new blazor --name "Pipeline.Web" --output .\src\Pipeline.Web\ --interactivity Server --auth None --all-interactive
}

$projects = & dotnet sln .\Pipeline.slnx list

if ($projects -notcontains "src\Pipeline.Web\Pipeline.Web.csproj")
{
    Write-Host "Add Pipeline.Web to sln"
    & dotnet sln add .\src\Pipeline.Web\
}

Write-Host "Make sure .gitIgnore ignores wwwroot/lib files"
(get-content .\.gitIgnore -raw) -replace "(?m)^#wwwroot/?","**/wwwroot/lib/" | set-content .\.gitIgnore

if (Test-Path .\src\Pipeline.Web\wwwroot\lib)
{
    Write-Host "wwwroot/lib folder exists - deleting it because we want libman to manage it."
    Remove-Item -Recurse -Force .\src\Pipeline.Web\wwwroot\lib
}

Write-Host "Use libman for client-side library management instead of manually adding libraries"

if ((Test-Path .\src\Pipeline.Web\libman.json))
{
    push-location .\src\Pipeline.Web\
    & libman restore
    pop-location

}
else
{
    Write-Host "Initialize libman"
    push-location .\src\Pipeline.Web\
    & libman init --useDefault --default-destination wwwroot/lib

    & libman install bootstrap --destination wwwroot/lib/bootstrap
    pop-location
}

Write-Host "Fix App.razor to include the proper reference to the bootstrap CSS file"
(Get-Content .\src\Pipeline.Web\Components\App.razor -Raw) -replace "(?m)lib/bootstrap/dist/css/bootstrap.min.css", "lib/bootstrap/css/bootstrap.min.css" | Set-Content .\src\Pipeline.Web\Components\App.razor