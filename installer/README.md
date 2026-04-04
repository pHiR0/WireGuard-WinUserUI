# Installer — WiX Toolset v5 (MSI)

Genera un instalador `.msi` de 64 bits que instala:
- **WireGuard.Service.exe** como servicio de Windows con inicio automático
- **WireGuard.UI.exe** como aplicación de escritorio con acceso directo en el Menú Inicio

## Archivos

| Archivo | Descripción |
|---------|-------------|
| `WireGuard-WinUserUI.wixproj` | Proyecto MSBuild / WiX SDK |
| `Product.wxs` | Definición del Package: versión, UpgradeCode, MajorUpgrade, Features |
| `Directories.wxs` | Estructura de directorios de instalación |
| `Components.wxs` | Componentes: ejecutables, servicio Windows, accesos directos |

## Requisitos previos

1. **.NET SDK 8+**
2. **WiX tool** (dotnet global tool):
   ```powershell
   dotnet tool install --global wix
   ```

## Cómo generar el MSI

Desde la raíz del repositorio:

```powershell
# Publicar proyectos + construir MSI
.\scripts\package.ps1 -Version "2026.04.04.1200"
```

El MSI resultante queda en `artifacts/WireGuard-WinUserUI-<version>-x64.msi`.

### Parámetros de `package.ps1`

| Parámetro | Descripción | Por defecto |
|-----------|-------------|-------------|
| `-Configuration` | Debug / Release | Release |
| `-Version` | Versión del producto (yyyy.MM.dd.HHmm) | 1.0.0.0 |
| `-SkipPublish` | Omite `dotnet publish` (usa artefactos existentes) | No |
| `-SkipInstaller` | Solo publica, no construye el MSI | No |

## Lógica de actualización (MajorUpgrade)

El `UpgradeCode` es fijo. Al instalar una versión más reciente:
1. El MSI detecta la versión anterior instalada
2. La desinstala automáticamente (`Schedule="afterInstallInitialize"`)
3. Instala la nueva versión

Para que el control de versiones funcione correctamente, usa siempre el formato `yyyy.MM.dd.HHmm` como versión (ver `docs/project-guidelines.md`, sección 7).

## Servicio Windows

- **Nombre del servicio**: `WireGuard-WinUserUI`
- **Inicio**: Automático (`Start="auto"`)  
- **Stop/Remove**: El instalador para y elimina el servicio al desinstalar

## Notas de seguridad

Los ficheros `.conf` en `%ProgramData%\WireGuard\` tienen ACL restringida (solo SYSTEM y Administradores) fijada durante la importación/edición desde el servicio.

