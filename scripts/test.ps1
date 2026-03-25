param(
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"

Write-Host "=== Tests del proyecto ==="

if (Test-Path ".\WireGuard-WinUserUI.sln") {
    dotnet test .\WireGuard-WinUserUI.sln -c $Configuration
}
else {
    Write-Warning "No existe todavía la solución .sln"
}
