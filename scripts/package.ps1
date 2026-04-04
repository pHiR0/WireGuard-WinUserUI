param(
    [string]$Configuration = "Release",
    [string]$Version       = "",
    [switch]$SkipPublish,
    [switch]$SkipInstaller
)

if ([string]::IsNullOrWhiteSpace($Version))
{
    $Version = (Get-Date -Format "yy.M.d.Hmm")
}

$ErrorActionPreference = "Stop"
$Root = Split-Path $PSScriptRoot -Parent

Write-Host "=== WireGuard-WinUserUI — Package (version $Version, config $Configuration) ===" -ForegroundColor Cyan

# ---------------------------------------------------------------------------
# 1. Directorios de salida
# ---------------------------------------------------------------------------
$ArtifactsDir   = Join-Path $Root "artifacts"
$ServiceOut     = Join-Path $ArtifactsDir "publish\service"
$UIOut          = Join-Path $ArtifactsDir "publish\ui"
$InstallerOut   = Join-Path $ArtifactsDir "installer"

New-Item -ItemType Directory -Force -Path $ServiceOut, $UIOut, $InstallerOut | Out-Null

# ---------------------------------------------------------------------------
# 2. Publicar los proyectos (self-contained, single-file, win-x64)
# ---------------------------------------------------------------------------
if (-not $SkipPublish)
{
    Write-Host "`n-- Publicando servicio..." -ForegroundColor Yellow
    dotnet publish "$Root\src\Service\WireGuard.Service.csproj" `
        -c $Configuration `
        -r win-x64 `
        --self-contained `
        -p:PublishSingleFile=true `
        -p:IncludeNativeLibrariesForSelfExtract=true `
        -p:Version=$Version `
        -o $ServiceOut
    if ($LASTEXITCODE -ne 0) { throw "Error al publicar el servicio" }

    Write-Host "`n-- Publicando UI..." -ForegroundColor Yellow
    dotnet publish "$Root\src\UI\WireGuard.UI.csproj" `
        -c $Configuration `
        -r win-x64 `
        --self-contained `
        -p:PublishSingleFile=true `
        -p:IncludeNativeLibrariesForSelfExtract=true `
        -p:Version=$Version `
        -o $UIOut
    if ($LASTEXITCODE -ne 0) { throw "Error al publicar la UI" }

    Write-Host "  Servicio publicado en: $ServiceOut"
    Write-Host "  UI publicada en:       $UIOut"
}
else
{
    Write-Host "  (publicación omitida con -SkipPublish)"
}

# ---------------------------------------------------------------------------
# 3. Construir el instalador MSI con WiX v5
#    Requisito previo: dotnet tool install --global wix
# ---------------------------------------------------------------------------
if (-not $SkipInstaller)
{
    Write-Host "`n-- Comprobando herramienta WiX..." -ForegroundColor Yellow
    $wixCmd = Get-Command wix -ErrorAction SilentlyContinue
    if ($null -eq $wixCmd)
    {
        Write-Host "  WiX no encontrado. Instalando como dotnet global tool..." -ForegroundColor DarkYellow
        dotnet tool install --global wix
        if ($LASTEXITCODE -ne 0) { throw "No se pudo instalar wix tool" }
    }

    Write-Host "`n-- Construyendo MSI (WiX v5)..." -ForegroundColor Yellow
    $InstallerProj = Join-Path $Root "installer\WireGuard-WinUserUI.wixproj"
    dotnet build $InstallerProj `
        -c $Configuration `
        -p:Platform=x64 `
        -p:Version=$Version `
        -p:ServicePublishDir="$ServiceOut\" `
        -p:UIPublishDir="$UIOut\" `
        -p:OutputPath="$InstallerOut\"
    if ($LASTEXITCODE -ne 0) { throw "Error al construir el instalador MSI" }

    $msi = Get-ChildItem $InstallerOut -Filter "*.msi" -Recurse | Select-Object -First 1
    if ($msi)
    {
        Write-Host "  MSI generado: $($msi.FullName)" -ForegroundColor Green
        # Copiar a la raíz de artifacts con el nombre versionado
        $dest = Join-Path $ArtifactsDir "WireGuard-WinUserUI-$Version-x64.msi"
        Copy-Item $msi.FullName $dest -Force
        Write-Host "  Artefacto final: $dest" -ForegroundColor Green
    }
    else
    {
        Write-Warning "No se encontró ningún .msi en $InstallerOut"
    }
}
else
{
    Write-Host "  (instalador omitido con -SkipInstaller)"
}

Write-Host "`n=== Empaquetado completado ===" -ForegroundColor Cyan

