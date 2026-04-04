# Roadmap — WireGuard Manager

## Estado general: ✅ Fases 1–5 completadas

---

## Fase 1 — MVP: Servicio + UI + IPC + RBAC ✅
- Servicio Windows base (Worker Service)
- IPC Named Pipe con ACL y framing JSON
- Listado de túneles y Start/Stop
- RBAC básico (Viewer / Operator / AdvancedOperator / Admin)
- Auditoría mínima (JSON Lines)
- UI Avalonia con lista de túneles y estado

## Fase 2 — Funcionalidad avanzada ✅
- Importación, creación y edición de túneles (editor modal)
- Eliminación de túneles
- Validación de configuración `.conf`
- Notificaciones Toast de Windows
- Exportación de configuraciones
- Inicio automático por túnel configurable

## Fase 3 — UX y seguridad mejorada ✅
- Rol resuelto desde grupos de Windows (4 grupos Windows locales)
- Eliminación de gestión de usuarios desde la UI
- Estadísticas de túnel (IP local, endpoint, handshake, Rx/Tx, tiempo conectado)
- IP pública con cadena de fallback y control de tasa
- Tray icon con cambio de icono según estado
- Marca de agua, colores de card según estado, iconos en botones
- Settings con autoguardado, ToggleSwitches, inicio con Windows
- Tab "Acerca de" con enlace al repo
- Comprobación de WireGuard instalado
- Protección ACL en archivos `.conf`

## Fase 4 — Hardening del canal IPC ✅
- DENY explícito a NetworkSid (bloqueo acceso remoto)
- SYSTEM y Administrators FullControl
- Anti-squatting (`FILE_FLAG_FIRST_PIPE_INSTANCE`)
- Identificación de llamante por SID (`NTAccount.Translate`)
- Rate limiting: máximo 4 conexiones por SID
- 96 tests unitarios (84 servicio + 12 UI)

## Fase 5 — Compilación e instalador ✅
- Fichero `.sln` unificando los 5 proyectos
- Publicación self-contained single-file (win-x64)
- Instalador MSI con WiX v5 SDK
  - Servicio Windows con inicio automático
  - Acceso directo en Menú Inicio y Escritorio
  - MajorUpgrade para actualizaciones in-place
  - Localización en español, Términos y Condiciones
  - Icono y versión en "Desinstalar programas"
- Scripts: `build.ps1`, `test.ps1`, `package.ps1` (versión automática `yy.MM.dd.HHmm`)

---

## Posibles fases futuras

- Métricas/telemetría local (opcional)
- Empaquetado Chocolatey (repositorio independiente)
- Soporte multi-idioma (i18n)
- Logs de diagnóstico exportables desde la UI
