param(
    [string]$Configuration = "Release",
    [string]$Version       = "",
    [switch]$SkipPublish,
    [switch]$SkipInstaller
)

if ([string]::IsNullOrWhiteSpace($Version))
{
    $now = Get-Date
    # Normalize version to avoid leading zeros (e.g., "005" -> "5")
    # NuGet/Chocolatey strip leading zeros, so we do it upfront for consistency
    $Version = "{0}.{1}.{2}.{3}" -f $now.ToString("yy"), $now.Month, $now.Day, ([int]($now.ToString("Hmm")))
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

# ---------------------------------------------------------------------------
# 4. Generar paquete Chocolatey (.nupkg)
#    Requiere: choco (Chocolatey) instalado y disponible en PATH
#    Omitido si no se generó el MSI (SkipInstaller) o si no se encuentra choco.
# ---------------------------------------------------------------------------
$msiArtifact = Join-Path $ArtifactsDir "WireGuard-WinUserUI-$Version-x64.msi"
$ChocoDir    = Join-Path $Root "packaging\wireguard-manager"
$NuspecPath  = Join-Path $ChocoDir "wireguard-manager.nuspec"
$InstallScript = Join-Path $ChocoDir "tools\chocolateyinstall.ps1"
$ChocoOut    = Join-Path $ArtifactsDir "chocolatey"

if (-not $SkipInstaller -and (Test-Path $msiArtifact))
{
    Write-Host "`n-- Generando paquete Chocolatey..." -ForegroundColor Yellow

    $chocoCmd = Get-Command choco -ErrorAction SilentlyContinue
    if ($null -eq $chocoCmd)
    {
        Write-Warning "  'choco' no encontrado en PATH. Omitiendo generación del paquete Chocolatey."
        Write-Warning "  Instale Chocolatey (https://chocolatey.org/install) y vuelva a ejecutar."
    }
    else
    {
        # 4a. Calcular SHA256 del MSI recién generado
        $hash = (Get-FileHash -Path $msiArtifact -Algorithm SHA256).Hash.ToLower()
        Write-Host "  SHA256 del MSI: $hash"

        # 4b. Actualizar checksum64 en chocolateyinstall.ps1
        $installContent = Get-Content $InstallScript -Raw
        $installContent = $installContent -replace "checksum64\s*=\s*'[0-9a-fA-F]+'", "checksum64    = '$hash'"
        Set-Content -Path $InstallScript -Value $installContent -Encoding UTF8 -NoNewline
        Write-Host "  checksum64 actualizado en chocolateyinstall.ps1"

        # 4c. Actualizar <version> en el .nuspec con la versión actual del build
        [xml]$nuspec = Get-Content $NuspecPath
        $nuspec.package.metadata.version = $Version
        $nuspec.Save($NuspecPath)
        Write-Host "  Versión $Version actualizada en wireguard-manager.nuspec"

        # 4d. Ejecutar choco pack
        New-Item -ItemType Directory -Force -Path $ChocoOut | Out-Null
        Push-Location $ChocoDir
        try
        {
            choco pack --out "$ChocoOut"
            if ($LASTEXITCODE -ne 0) { throw "choco pack falló con código $LASTEXITCODE" }
        }
        finally { Pop-Location }

        $nupkg = Get-ChildItem $ChocoOut -Filter "*.nupkg" | Sort-Object LastWriteTime -Descending | Select-Object -First 1
        if ($nupkg)
        {
            Write-Host "  .nupkg generado: $($nupkg.FullName)" -ForegroundColor Green
        }
        else
        {
            Write-Warning "  No se encontró ningún .nupkg en $ChocoOut"
        }
    }
}
elseif ($SkipInstaller)
{
    Write-Host "  (paquete Chocolatey omitido porque se usó -SkipInstaller)"
}
else
{
    Write-Host "  (paquete Chocolatey omitido: no existe el MSI en $msiArtifact)"
}

Write-Host "`n=== Empaquetado completado ===" -ForegroundColor Cyan

