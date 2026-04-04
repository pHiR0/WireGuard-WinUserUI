#Requires -Version 5.1
<#
.SYNOPSIS
    Publica los cambios de 'dev' al repo público mediante el mirror de GitHub Actions.

.DESCRIPTION
    1. Cambia a la rama main
    2. Mergea desde dev (.gitattributes bloquea .dev/ automáticamente)
    3. Push a origin/main (dispara el mirror Action → repo público)
    4. Vuelve a la rama dev

.EXAMPLE
    .\.dev\publish-to-public.ps1
#>

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Write-Step([string]$msg) { Write-Host "`n>> $msg" -ForegroundColor Cyan }
function Write-Ok([string]$msg)   { Write-Host "   OK: $msg" -ForegroundColor Green }
function Write-Err([string]$msg)  { Write-Host "   !! $msg" -ForegroundColor Red }

# ── Verificar que estamos en la raíz del repo ─────────────────────────────────
if (-not (Test-Path ".git")) {
    Write-Err "Ejecuta este script desde la raíz del repositorio."
    exit 1
}

# ── Verificar que estamos en dev ──────────────────────────────────────────────
$currentBranch = git branch --show-current
if ($currentBranch -ne "dev") {
    Write-Err "Este script debe ejecutarse desde la rama 'dev'. Rama actual: '$currentBranch'"
    exit 1
}

# ── Verificar que no hay cambios sin commitear ────────────────────────────────
$dirty = git status --porcelain
if ($dirty) {
    Write-Err "Hay cambios sin commitear en dev. Haz commit o stash antes de publicar."
    git status --short
    exit 1
}

# ── 1. Cambiar a main ─────────────────────────────────────────────────────────
Write-Step "Cambiando a rama 'main'..."
git checkout main
Write-Ok "En rama 'main'"

# ── 2. Merge desde dev ───────────────────────────────────────────────────────
Write-Step "Mergeando 'dev' → 'main'..."
try {
    git merge dev --no-edit
    Write-Ok "Merge completado (.dev/ bloqueada por .gitattributes)"
} catch {
    Write-Err "El merge falló. Resuelve los conflictos y vuelve a ejecutar."
    git checkout dev
    exit 1
}

# ── 3. Push a origin/main ────────────────────────────────────────────────────
Write-Step "Pusheando 'main' a origin (dispara mirror → repo público)..."
git push origin main
Write-Ok "Push completado. El GitHub Action mirror se encargará del resto."

# ── 4. Volver a dev ──────────────────────────────────────────────────────────
Write-Step "Volviendo a rama 'dev'..."
git checkout dev
Write-Ok "De vuelta en 'dev'"

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host " Publicación completada " -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host " Revisa el Action en el repo privado:" -ForegroundColor White
Write-Host " https://github.com/pHiR0/WireGuard-WinUserUI-dev/actions" -ForegroundColor DarkGray
Write-Host ""
