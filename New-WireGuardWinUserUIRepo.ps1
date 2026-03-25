param(
    [string]$RootPath = ".\WireGuard-WinUserUI",
    [switch]$Force
)

$ErrorActionPreference = "Stop"

function New-DirSafe {
    param(
        [Parameter(Mandatory)]
        [string]$Path
    )

    if (-not (Test-Path -LiteralPath $Path)) {
        New-Item -ItemType Directory -Path $Path | Out-Null
    }
}

function New-FileSafe {
    param(
        [Parameter(Mandatory)]
        [string]$Path,

        [Parameter(Mandatory)]
        [string]$Content
    )

    if ((Test-Path -LiteralPath $Path) -and (-not $Force)) {
        Write-Host "Omitido (ya existe): $Path"
        return
    }

    $parent = Split-Path -Parent $Path
    if ($parent) {
        New-DirSafe -Path $parent
    }

    Set-Content -LiteralPath $Path -Value $Content -Encoding UTF8
    Write-Host "Creado: $Path"
}

$repoName = Split-Path -Leaf (Resolve-Path -LiteralPath (Split-Path -Parent (Join-Path $PWD $RootPath)) -ErrorAction SilentlyContinue | ForEach-Object { $_.Path } | Select-Object -First 1)
if ([string]::IsNullOrWhiteSpace($repoName)) {
    $repoName = Split-Path -Leaf $RootPath
}

New-DirSafe -Path $RootPath

$dirs = @(
    "docs",
    "docs\decisions",
    "src",
    "src\Service",
    "src\UI",
    "src\Shared",
    "installer",
    "packaging",
    "packaging\chocolatey",
    "packaging\chocolatey\tools",
    "scripts",
    "build",
    "artifacts",
    ".github",
    ".github\workflows",
    "tests",
    "tests\Service.Tests",
    "tests\UI.Tests"
)

foreach ($dir in $dirs) {
    New-DirSafe -Path (Join-Path $RootPath $dir)
}

$readme = @"
# WireGuard-WinUserUI

A Windows UI for managing WireGuard tunnels as a non-admin user, powered by a privileged backend service.

## Estructura del repositorio

- `src/Service`: servicio Windows privilegiado
- `src/UI`: aplicación cliente para usuario no administrador
- `src/Shared`: contratos, modelos y utilidades compartidas
- `tests`: pruebas
- `installer`: instalador
- `packaging/chocolatey`: paquete Chocolatey
- `scripts`: automatización de build, empaquetado y publicación
- `docs`: documentación funcional, técnica y de seguridad

## Documentación

- `docs/requirements.md`
- `docs/architecture.md`
- `docs/security.md`
- `docs/roadmap.md`

## Notas

Este proyecto no pretende reemplazar WireGuard, sino ofrecer una UI alternativa para Windows que permita su uso controlado por usuarios no administradores mediante un servicio local privilegiado.
"@

$requirements = @"
# Requisitos

## Objetivo

Desarrollar una aplicación para Windows que permita gestionar túneles WireGuard desde cuentas no administradoras, mediante:

1. Un servicio Windows privilegiado y siempre activo
2. Una aplicación UI de usuario sin elevación

## Requisitos funcionales

- Listar túneles WireGuard existentes
- Ver estado de cada túnel
- Conectar y desconectar túneles
- Reiniciar túneles
- Crear, editar, importar y eliminar túneles según rol
- Gestionar usuarios autorizados y sus roles
- Registrar auditoría de operaciones

## Roles mínimos

- Viewer
- Operator
- Advanced Operator
- Admin

## Restricciones

- No usar la UI oficial de WireGuard
- No depender de UAC en la UI
- No almacenar credenciales de administrador
- No exponer API remota; solo local
- Toda validación de seguridad debe hacerse en el servicio
"@

$architecture = @"
# Arquitectura

## Componentes

### Servicio Windows
Responsable de:
- ejecutar operaciones privilegiadas
- validar permisos
- aplicar autorización
- exponer IPC local seguro

### Aplicación UI
Responsable de:
- mostrar túneles y estado
- enviar peticiones al servicio
- adaptar la experiencia según el rol del usuario

## Comunicación
Preferencia:
- Named Pipes

Alternativas:
- gRPC local
- RPC de Windows

## Principio de diseño
La UI no tiene privilegios. El servicio decide todo.
"@

$security = @"
# Seguridad

## Principios

- La UI nunca ejecuta operaciones privilegiadas directamente
- El servicio debe revalidar todas las operaciones
- No confiar en el cliente
- Aplicar mínimo privilegio
- Proteger secretos y configuraciones sensibles

## Riesgos principales

- Escalada de privilegios por validación insuficiente
- Exposición de claves privadas
- Ejecución de operaciones no autorizadas
- Exposición accidental de una API fuera del equipo local

## Controles mínimos

- Identificación fiable del usuario Windows llamante
- RBAC en backend
- Auditoría de acciones sensibles
- Protección de canal IPC
"@

$roadmap = @"
# Roadmap

## Fase 1
- Servicio Windows base
- IPC local básico
- Listado de túneles
- Start/Stop
- Roles básicos
- Auditoría mínima

## Fase 2
- Edición e importación de túneles
- Gestión de usuarios
- UI más completa
- Mejoras de seguridad

## Fase 3
- Paridad funcional avanzada
- Mejoras UX
- Políticas refinadas
- Endurecimiento de seguridad
"@

$decision = @"
# ADR 0001 - Arquitectura base

## Estado
Aceptada

## Contexto
La UI oficial de WireGuard para Windows no resuelve bien el uso desde usuarios no administradores.

## Decisión
Se adopta una arquitectura de dos componentes:
- Servicio Windows privilegiado
- Aplicación UI sin elevación

## Consecuencias
- Mayor complejidad que un wrapper simple
- Mejor aislamiento de privilegios
- Posibilidad de RBAC y auditoría
"@

$gitignore = @"
# Build
bin/
obj/
artifacts/
build/

# IDE
.vs/
.vscode/
*.user
*.suo

# Logs
*.log

# Test
TestResults/

# OS
Thumbs.db
.DS_Store

# Packaging output
*.nupkg
*.msi
*.exe

# Secrets / local config
appsettings.Development.json
*.pfx
*.snk
"@

$editorconfig = @"
root = true

[*]
charset = utf-8
end_of_line = crlf
insert_final_newline = true
indent_style = space
indent_size = 4
trim_trailing_whitespace = true

[*.md]
trim_trailing_whitespace = false
"@

$buildScript = @"
param(
    [string]`$Configuration = "Release"
)

`$ErrorActionPreference = "Stop"

Write-Host "=== Build del proyecto ==="

if (Test-Path ".\WireGuard-WinUserUI.sln") {
    dotnet restore .\WireGuard-WinUserUI.sln
    dotnet build .\WireGuard-WinUserUI.sln -c `$Configuration --no-restore
}
else {
    Write-Warning "No existe todavía la solución .sln"
}
"@

$testScript = @"
param(
    [string]`$Configuration = "Release"
)

`$ErrorActionPreference = "Stop"

Write-Host "=== Tests del proyecto ==="

if (Test-Path ".\WireGuard-WinUserUI.sln") {
    dotnet test .\WireGuard-WinUserUI.sln -c `$Configuration
}
else {
    Write-Warning "No existe todavía la solución .sln"
}
"@

$packageScript = @"
param(
    [string]`$Configuration = "Release"
)

`$ErrorActionPreference = "Stop"

Write-Host "=== Empaquetado ==="
Write-Host "Pendiente de implementar: MSI / Chocolatey / publicación de artefactos"
"@

$bootstrapScript = @"
`$ErrorActionPreference = "Stop"

Write-Host "=== Bootstrap del entorno ==="

if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    Write-Warning ".NET SDK no está disponible en PATH"
} else {
    dotnet --info | Out-Host
}

Write-Host "Bootstrap finalizado"
"@

$chocoNuspec = @"
<?xml version="1.0"?>
<package>
  <metadata>
    <id>wireguard-winuserui</id>
    <version>0.1.0</version>
    <title>WireGuard-WinUserUI</title>
    <authors>TuNombre</authors>
    <projectUrl>https://github.com/TU_USUARIO/WireGuard-WinUserUI</projectUrl>
    <licenseUrl>https://opensource.org/licenses/MIT</licenseUrl>
    <requireLicenseAcceptance>false</requireLicenseAcceptance>
    <description>Windows UI for managing WireGuard tunnels as a non-admin user, powered by a privileged backend service.</description>
    <tags>wireguard windows ui non-admin vpn</tags>
  </metadata>
  <files>
    <file src="tools\**" target="tools" />
  </files>
</package>
"@

$chocoInstall = @"
`$ErrorActionPreference = "Stop"
Write-Host "Instalación Chocolatey pendiente de implementar"
"@

$chocoUninstall = @"
`$ErrorActionPreference = "Stop"
Write-Host "Desinstalación Chocolatey pendiente de implementar"
"@

$servicePlaceholder = @"
# Placeholder

Aquí irá el código fuente del servicio Windows privilegiado.

Responsabilidades previstas:
- IPC local seguro
- Autorización RBAC
- Operaciones WireGuard
- Auditoría
"@

$uiPlaceholder = @"
# Placeholder

Aquí irá el código fuente de la aplicación UI de usuario.

Responsabilidades previstas:
- Listar túneles
- Mostrar estado
- Conectar / desconectar
- Administración según rol
"@

$sharedPlaceholder = @"
# Placeholder

Aquí irán contratos, DTOs, modelos compartidos y utilidades comunes entre el servicio y la UI.
"@

$installerPlaceholder = @"
# Placeholder

Aquí irá la lógica del instalador:
- instalación del servicio
- instalación de la UI
- registro de componentes
- configuración inicial
"@

$workflow = @"
name: CI

on:
  push:
    branches: [ main, master ]
  pull_request:

jobs:
  build:
    runs-on: windows-latest

    steps:
      - name: Checkout
        uses: actions/checkout@v4

      - name: Setup .NET
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '8.0.x'

      - name: Restore
        run: dotnet restore .\WireGuard-WinUserUI.sln
        shell: pwsh
        continue-on-error: true

      - name: Build
        run: dotnet build .\WireGuard-WinUserUI.sln -c Release --no-restore
        shell: pwsh
        continue-on-error: true

      - name: Test
        run: dotnet test .\WireGuard-WinUserUI.sln -c Release
        shell: pwsh
        continue-on-error: true
"@

New-FileSafe -Path (Join-Path $RootPath "README.md") -Content $readme
New-FileSafe -Path (Join-Path $RootPath ".gitignore") -Content $gitignore
New-FileSafe -Path (Join-Path $RootPath ".editorconfig") -Content $editorconfig

New-FileSafe -Path (Join-Path $RootPath "docs\requirements.md") -Content $requirements
New-FileSafe -Path (Join-Path $RootPath "docs\architecture.md") -Content $architecture
New-FileSafe -Path (Join-Path $RootPath "docs\security.md") -Content $security
New-FileSafe -Path (Join-Path $RootPath "docs\roadmap.md") -Content $roadmap
New-FileSafe -Path (Join-Path $RootPath "docs\decisions\0001-architecture.md") -Content $decision

New-FileSafe -Path (Join-Path $RootPath "src\Service\README.md") -Content $servicePlaceholder
New-FileSafe -Path (Join-Path $RootPath "src\UI\README.md") -Content $uiPlaceholder
New-FileSafe -Path (Join-Path $RootPath "src\Shared\README.md") -Content $sharedPlaceholder
New-FileSafe -Path (Join-Path $RootPath "installer\README.md") -Content $installerPlaceholder

New-FileSafe -Path (Join-Path $RootPath "scripts\build.ps1") -Content $buildScript
New-FileSafe -Path (Join-Path $RootPath "scripts\test.ps1") -Content $testScript
New-FileSafe -Path (Join-Path $RootPath "scripts\package.ps1") -Content $packageScript
New-FileSafe -Path (Join-Path $RootPath "scripts\bootstrap.ps1") -Content $bootstrapScript

New-FileSafe -Path (Join-Path $RootPath "packaging\chocolatey\wireguard-winuserui.nuspec") -Content $chocoNuspec
New-FileSafe -Path (Join-Path $RootPath "packaging\chocolatey\tools\chocolateyInstall.ps1") -Content $chocoInstall
New-FileSafe -Path (Join-Path $RootPath "packaging\chocolatey\tools\chocolateyUninstall.ps1") -Content $chocoUninstall

New-FileSafe -Path (Join-Path $RootPath ".github\workflows\ci.yml") -Content $workflow

Write-Host ""
Write-Host "Estructura creada correctamente en: $((Resolve-Path $RootPath).Path)"
Write-Host ""
Write-Host "Siguientes pasos recomendados:"
Write-Host "1. Crear la solución .NET"
Write-Host "2. Añadir proyectos Service/UI/Shared/Tests"
Write-Host "3. Completar docs/requirements.md con el texto definitivo"
Write-Host "4. Implementar scripts de build y empaquetado"