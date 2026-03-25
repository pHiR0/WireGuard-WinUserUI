$ErrorActionPreference = "Stop"

Write-Host "=== Bootstrap del entorno ==="

if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    Write-Warning ".NET SDK no está disponible en PATH"
} else {
    dotnet --info | Out-Host
}

Write-Host "Bootstrap finalizado"
