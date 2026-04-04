param(
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"
$Root = Split-Path $PSScriptRoot -Parent

Write-Host "=== Tests del proyecto ==="

$slnPath = Join-Path $Root "WireGuard-WinUserUI.sln"
if (Test-Path $slnPath) {
    dotnet test $slnPath -c $Configuration --verbosity normal
}
else {
    Write-Host "  Sin .sln — ejecutando tests por proyecto..."
    dotnet test "$Root\tests\Service.Tests\WireGuard.Service.Tests.csproj" -c $Configuration --verbosity normal
    dotnet test "$Root\tests\UI.Tests\WireGuard.UI.Tests.csproj" -c $Configuration --verbosity normal
}

if ($LASTEXITCODE -ne 0) { throw "Algún test falló" }

Write-Host "=== Tests completados ==="
