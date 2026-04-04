#Requires -Version 5.1
<#
.SYNOPSIS
    Configura el entorno de desarrollo local tras clonar el repositorio privado.

.DESCRIPTION
    Ejecutar UNA SOLA VEZ después de clonar WireGuard-WinUserUI-dev.
    Configura: remote público, driver merge=ours, exclude local y rama dev.

.EXAMPLE
    cd WireGuard-WinUserUI-dev
    .\.dev\setup-dev-env.ps1
#>

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Write-Step([string]$msg) {
    Write-Host "`n>> $msg" -ForegroundColor Cyan
}
function Write-Ok([string]$msg) {
    Write-Host "   OK: $msg" -ForegroundColor Green
}
function Write-Skip([string]$msg) {
    Write-Host "   --: $msg" -ForegroundColor DarkGray
}

# ── Verificar que estamos en la raíz del repo ─────────────────────────────────
if (-not (Test-Path ".git")) {
    Write-Error "Ejecuta este script desde la raíz del repositorio."
    exit 1
}

# ── 1. Remote público ─────────────────────────────────────────────────────────
Write-Step "Configurar remote 'public' (repo público)"
$remotes = git remote 2>&1
if ($remotes -contains "public") {
    Write-Skip "Remote 'public' ya existe"
} else {
    git remote add public https://github.com/pHiR0/WireGuard-WinUserUI.git
    Write-Ok "Remote 'public' añadido -> https://github.com/pHiR0/WireGuard-WinUserUI.git"
}

# ── 2. Driver merge=ours ──────────────────────────────────────────────────────
Write-Step "Configurar driver git 'merge=ours' (para .gitattributes)"
$current = git config --local merge.ours.driver 2>&1
if ($current -eq "true") {
    Write-Skip "Driver merge.ours.driver ya configurado"
} else {
    git config merge.ours.driver true
    Write-Ok "merge.ours.driver = true"
}

# ── 3. Crear / cambiar a rama dev ────────────────────────────────────────────
Write-Step "Configurar rama 'dev'"
$currentBranch = git branch --show-current 2>&1
$localBranches = git branch --format="%(refname:short)" 2>&1

if ($localBranches -contains "dev") {
    Write-Skip "Rama 'dev' ya existe localmente"
} else {
    $remoteBranches = git branch -r --format="%(refname:short)" 2>&1
    if ($remoteBranches -match "origin/dev") {
        git checkout -t origin/dev | Out-Null
        Write-Ok "Rama 'dev' creada desde origin/dev y activada"
    } else {
        Write-Host "   !!: origin/dev no encontrada. Crea la rama manualmente si es necesario." -ForegroundColor Yellow
    }
}

if ($currentBranch -ne "dev") {
    git checkout dev 2>&1 | Out-Null
    Write-Ok "Cambiado a rama 'dev'"
} else {
    Write-Skip "Ya estás en la rama 'dev'"
}

# ── Resumen ───────────────────────────────────────────────────────────────────
Write-Host "`n========================================" -ForegroundColor Cyan
Write-Host " Entorno de desarrollo configurado " -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""
Write-Host " Remotes:" -ForegroundColor White
git remote -v
Write-Host ""
Write-Host " Rama actual: $(git branch --show-current)" -ForegroundColor White
Write-Host ""
Write-Host " Listo. Puedes empezar a trabajar." -ForegroundColor Green
