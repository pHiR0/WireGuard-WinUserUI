param(
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"
$Root = Split-Path $PSScriptRoot -Parent

Write-Host "=== Build del proyecto ==="

$slnPath = Join-Path $Root "WireGuard-WinUserUI.sln"
if (Test-Path $slnPath)
{
    dotnet restore $slnPath
    dotnet build   $slnPath -c $Configuration --no-restore
}
else
{
    Write-Host "  Sin .sln — compilando proyectos individuales..."
    dotnet build "$Root\src\Service\WireGuard.Service.csproj" -c $Configuration
    dotnet build "$Root\src\UI\WireGuard.UI.csproj"           -c $Configuration
}

Write-Host "=== Build completado ==="
