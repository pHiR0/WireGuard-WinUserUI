# Arquitectura de WireGuard Manager

## Visión general

WireGuard Manager separa la lógica privilegiada de la interfaz de usuario en dos procesos independientes que se comunican mediante un Named Pipe local. La UI **nunca** ejecuta operaciones privilegiadas directamente.

```
┌─────────────────────────────────┐     Named Pipe (local)     ┌──────────────────────────────────┐
│   WireGuard.UI (usuario)        │ ◄────────────────────────► │   WireGuard.Service (SYSTEM)     │
│   Avalonia 11 / .NET 8          │    JSON framing            │   Worker Service / .NET 8        │
│   Sin privilegios               │    [int32 len][payload]    │   Privilegios de SYSTEM          │
└─────────────────────────────────┘                            └──────────────────────────────────┘
```

---

## Proyectos

| Proyecto | Ruta | Descripción |
|----------|------|-------------|
| `WireGuard.Shared` | `src/Shared/` | Contratos IPC, DTOs, enums compartidos |
| `WireGuard.Service` | `src/Service/` | Servicio Windows privilegiado |
| `WireGuard.UI` | `src/UI/` | Interfaz Avalonia para el usuario |
| `WireGuard.Service.Tests` | `tests/Service.Tests/` | Tests del servicio |
| `WireGuard.UI.Tests` | `tests/UI.Tests/` | Tests de ViewModels |

---

## Componentes del Servicio

### IPC (Named Pipe)
- Servidor Named Pipe con ACL diferenciada (véase [security.md](security.md))
- Protocolo de framing: `[int32 longitud][payload JSON]`
- Identificación del llamante: `GetImpersonationUserName()` + `NTAccount.Translate()` → SID
- Rate limiting: máximo 4 conexiones concurrentes por SID

### RBAC (Control de Acceso)
- Roles resueltos desde grupos de Windows locales (provisioned al arrancar el servicio):
  - `WireGuard UI - Administrator`
  - `WireGuard UI - Operador avanzado`
  - `WireGuard UI - Operador`
  - `WireGuard UI - Visualizador`
- Los miembros de `BUILTIN\Administrators` (SID `S-1-5-32-544`) tienen rol Admin automáticamente.

### Gestión de Túneles (`TunnelManager`)
- Interactúa con los servicios `WireGuardTunnel$<nombre>` via `ServiceController`
- Importa/edita/elimina configuraciones `.conf` en `%ProgramData%\WireGuard\`
- Los archivos `.conf` tienen ACL restringida (solo SYSTEM y Administrators)
- Inicio automático por túnel configurable (`sc.exe config start=auto|demand`)

### Auditoría
- `JsonAuditLogger`: escribe eventos en `%ProgramData%\WireGuard-WinUserUI\audit.jsonl`
- Rotación automática: se mantienen las últimas 10.000 entradas

---

## Componentes de la UI

### Comunicación
- `PipeClient`: envío/recepción de mensajes con retry automático
- Reconexión automática: intento cada 1 segundo si el servicio no está disponible

### MVVM
- `CommunityToolkit.Mvvm`: `ObservableObject`, `[RelayCommand]`, `[ObservableProperty]`
- `MainWindowViewModel`: coordinador principal, bucle de refresco, lista de túneles
- `TunnelViewModel`: estado de un túnel individual (color de card, estadísticas, tiempo conectado)
- `SettingsViewModel`: configuración persistida en `%AppData%\WireGuard-WinUserUI\settings.json`
- `TunnelEditorViewModel`: editor/importador modal de archivos `.conf`

### Tray Icon
- `App.axaml.cs`: gestiona el ciclo de vida con `ShutdownMode.OnExplicitShutdown`
- Icono cambia según si hay túneles activos (`icon.ico` / `icon_connected.ico`)
- Inicio minimizado configurable; MinimizeToTray aplica solo al botón Cerrar

### Servicios UI
- `PublicIpService`: resolución de IP pública con cadena de fallback (7 proveedores HTTP + DNS OpenDNS)
- `WindowsNotificationService`: notificaciones Toast de Windows (configurable)

---

## Principios de diseño

1. **La UI no tiene privilegios.** El servicio decide y valida todo.
2. **No confiar en el cliente.** Toda autorización ocurre en el servicio.
3. **Mínimo privilegio.** El servicio corre como `LocalSystem` pero solo hace lo estrictamente necesario.
4. **Defensa en profundidad.** ACL en pipe + identificación SID + rate limiting + anti-squatting.
5. **Sin instalación de WireGuard propia.** WireGuard para Windows debe estar instalado independientemente.
