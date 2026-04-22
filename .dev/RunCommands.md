## 1. Publicar el paquete en GitHub Packages

> Necesitas un Personal Access Token (PAT) con scope `write:packages` (y `repo` si el repositorio es privado).

Desde el script de packaging, añade un push al feed de GitHub:

```powershell
choco push wireguard-manager.1.0.0.nupkg \
    --source "https://nuget.pkg.github.com/TU_ORG_O_USUARIO/index.json" \
    --api-key "<TU_USUARIO>:<ghp_TU_PAT>"
```

O con `nuget.exe` directamente:

```powershell
nuget push wireguard-manager.1.0.0.nupkg \
    -Source "https://nuget.pkg.github.com/TU_ORG_O_USUARIO/index.json" \
    -ApiKey "<TU_USUARIO>:<ghp_TU_PAT>"
```

> El `.nupkg` generado por tu `package.ps1` ya es válido para esto.

---

## 2. Requisito del nuspec

El paquete debe tener una URL de repositorio asociada. Añade esto en tu `wireguard-manager.nuspec`:

```xml
<repository type="git" url="https://github.com/TU_ORG_O_USUARIO/WireGuard-WinUserUI" />
```

---

## 3. Configurar el source en los equipos destino

En cada equipo, ejecuta esto una sola vez (como administrador):

```powershell
choco source add \
    --name="wireguard-private" \
    --source="https://nuget.pkg.github.com/TU_ORG_O_USUARIO/index.json" \
    --user="GITHUB_USER" \
    --password="ghp_PAT_CON_READ_PACKAGES"
```

Con eso ya puedes instalar/actualizar normalmente:

```powershell
choco install wireguard-manager
choco upgrade wireguard-manager
```

---

## Consideraciones importantes

| Aspecto                    | Detalle                                                                                  |
|----------------------------|-----------------------------------------------------------------------------------------|
| Autenticación              | GitHub Packages NuGet siempre requiere auth, incluso para paquetes en repos públicos     |
| Scope del PAT de lectura   | `read:packages` (más `repo` si el repo es privado)                                       |
| Scope del PAT de escritura | `write:packages` (más `repo` si el repo es privado)                                      |
| PAT expiration             | Gestiona la renovación; si expira, los equipos no podrán instalar                        |
| Automatización             | Puedes hacer el push desde GitHub Actions usando `GITHUB_TOKEN` automáticamente          |
