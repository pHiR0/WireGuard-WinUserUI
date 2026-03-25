param(
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"

Write-Host "=== Build del proyecto ==="

if (Test-Path ".\WireGuard-WinUserUI.sln") {
    dotnet restore .\WireGuard-WinUserUI.sln
    dotnet build .\WireGuard-WinUserUI.sln -c $Configuration --no-restore
}
else {
    Write-Warning "No existe todavía la solución .sln"
}
