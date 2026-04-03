# Plan de Despliegue por Fases — WireGuard-WinUserUI

**Proyecto:** WireGuard Manager para usuarios no administradores en Windows  
**Stack:** C# / .NET 8 / Avalonia / Named Pipes / JSON+DPAPI  
**Última actualización:** 2026-04-03

---

## Estado actual

| Componente | Estado |
|------------|--------|
| Solución .NET y proyectos | ✅ Completo |
| Librería compartida (Shared): modelos, IPC, framing | ✅ Completo |
| Servicio base (Worker Service) | ✅ Completo |
| Gestión de túneles: listar, start, stop | ✅ Completo |
| RBAC: roles, JsonRoleStore con DPAPI | ✅ Completo |
| IPC Named Pipe: servidor y cliente | ✅ Completo |
| Auditoría mínima (JSON Lines) | ✅ Completo |
| UI Avalonia: lista de túneles, connect/disconnect | ✅ Completo |
| Bootstrap de admin (--add-admin CLI) | ✅ Completo |
| Protección anti pipe-squatting | ✅ Completo |
| Tests unitarios (27 + 12) | ✅ Completo |
| VS Code: tasks.json, launch.json | ✅ Completo |
| Instalador MSI | ❌ Pendiente |
| Empaquetado Chocolatey | ❌ Pendiente |
| Gestión de túneles avanzada (crear/editar/eliminar) | ❌ Pendiente |
| Importación de .conf | ❌ Pendiente |
| Gestión de usuarios desde UI | ❌ Pendiente |
| Reinicio de túneles | ❌ Pendiente |
| Notificaciones en tiempo real | ❌ Pendiente |
| Exportación de configuraciones | ❌ Pendiente |

---

## Fase 1 — MVP (COMPLETADA)

> **Objetivo:** Sistema funcional end-to-end: servicio + UI + IPC + RBAC básico + auditoría mínima.

### 1.1 Infraestructura base
- [x] Solución .NET 8 multi-proyecto
- [x] Librería compartida (`WireGuard.Shared`): modelos, enums, contratos IPC
- [x] Protocolo de mensajes: framing `[int32 length][JSON payload]`
- [x] `nuget.config` en la raíz para entornos sin configuración de usuario

### 1.2 Servicio Windows
- [x] Worker Service con `UseWindowsService()`
- [x] Named Pipe server con ACL (SYSTEM + Administrators + AuthenticatedUsers)
- [x] Protección anti pipe-squatting con `FILE_FLAG_FIRST_PIPE_INSTANCE`
- [x] Identificación de usuario llamante via `GetImpersonationUserName()` (post-primer-read)
- [x] `TunnelManager`: listar servicios `WireGuardTunnel$*`, start, stop via `ServiceController`
- [x] `JsonRoleStore`: persistencia de roles en JSON protegido con DPAPI (machine scope)
- [x] `AuthorizationService`: matriz de permisos por rol (Viewer / Operator / AdvancedOperator / Admin)
- [x] `RequestHandler`: dispatcher con autorización y auditoría integradas
- [x] `JsonAuditLogger`: eventos en JSON Lines (`%ProgramData%\WireGuard-WinUserUI\audit.jsonl`)
- [x] CLI `--add-admin <username>` para bootstrap del primer administrador
- [x] Warning al arranque si no hay usuarios configurados

### 1.3 Aplicación UI
- [x] Avalonia 11 con Fluent theme y MVVM (CommunityToolkit.Mvvm)
- [x] `PipeClient`: conexión, envío/recepción de mensajes con retry
- [x] Lista de túneles con estado visual (color por estado)
- [x] Botones Connect/Disconnect habilitados según rol
- [x] Auto-refresh cada 5 segundos
- [x] Indicador de conexión al servicio y rol del usuario actual
- [x] Banner de error descriptivo

### 1.4 Calidad
- [x] Tests unitarios: `AuthorizationServiceTests`, `RequestHandlerTests`, `IpcSerializationTests`, `TunnelViewModelTests`
- [x] VS Code: `tasks.json` (build, test, build-service, build-ui), `launch.json` (Service, UI, compound)

### Criterios de aceptación Fase 1
- [x] Usuario no-admin puede ver túneles sin prompt UAC
- [x] Operator puede start/stop; Viewer no puede
- [x] Audit log registra todas las operaciones
- [x] Servicio se identifica al usuario correctamente
- [x] Pipe-squatting detectado y bloqueado al arranque

---

## Fase 2 — Funcionalidad completa

> **Objetivo:** Paridad funcional con las necesidades operativas del día a día: gestión avanzada de túneles y administración de usuarios desde la propia UI.

### 2.1 Gestión avanzada de túneles (Service)

#### 2.1.1 Reiniciar túneles
- Nuevo comando IPC: `RestartTunnel`
- Implementar `RestartTunnelAsync()` en `TunnelManager`: stop → esperar parado → start
- Permiso mínimo: `Operator`
- UI: botón "Restart" en el ítem del túnel (visible cuando está Running o Error)

#### 2.1.2 Importar túnel desde `.conf`
- Nuevo comando IPC: `ImportTunnel`
- Request: `{ tunnelName, confContent (base64) }`
- Servicio: validar sintaxis del `.conf` antes de aplicar (campos obligatorios: `[Interface]`, `PrivateKey`, `[Peer]`, `PublicKey`)
- Guardar en `%ProgramData%\wireguard\<name>.conf` con permisos restringidos (solo SYSTEM)
- Registrar servicio: `wireguard.exe /installtunnelservice <path>`
- Backup automático si ya existe una versión previa
- Permiso mínimo: `AdvancedOperator`
- UI: diálogo "Import .conf" con selector de archivo y validación previa

#### 2.1.3 Crear túnel desde formulario
- Nuevo comando IPC: `CreateTunnel`
- Request: campos `Interface` (Address, DNS, PrivateKey) + `Peer` (PublicKey, Endpoint, AllowedIPs, PersistentKeepalive)
- Servicio: generar `.conf`, validar, instalar
- Permiso mínimo: `AdvancedOperator`
- UI: formulario con generación de par de claves en cliente (solo la pública viaja al servicio)

#### 2.1.4 Editar túnel existente
- Nuevo comando IPC: `EditTunnel`
- Servicio: detener el túnel si está running → reemplazar `.conf` con backup → reinstalar servicio → volver a arrancar si estaba running
- Permiso mínimo: `AdvancedOperator`
- UI: formulario de edición (igual que crear, pre-poblado)
- La `PrivateKey` solo se muestra a roles `Admin`; en otro caso aparece `*****`

#### 2.1.5 Eliminar túnel
- Nuevo comando IPC: `DeleteTunnel`
- Servicio: detener → `wireguard.exe /uninstalltunnelservice <name>` → eliminar `.conf`
- Confirmación doble antes de ejecutar
- Permiso mínimo: `AdvancedOperator`
- UI: botón "Delete" con modal de confirmación

#### 2.1.6 Exportar configuración
- Nuevo comando IPC: `ExportTunnel`
- Solo disponible para rol `Admin`
- Response: contenido del `.conf` en base64
- UI: botón "Export" → guardar como archivo `.conf` localmente
- La `PrivateKey` se incluye completa solo para `Admin`

### 2.2 Gestión de usuarios (Service + UI)

#### 2.2.1 Comandos IPC nuevos
- `ListUsers`: devuelve lista de `UserInfo` (username, role). Permiso: `Admin`
- `SetUserRole`: asigna rol a un usuario. Permiso: `Admin`
- `RemoveUser`: elimina usuario del role store. Permiso: `Admin`. No puede eliminar el último Admin
- `ListSystemUsers`: enumera usuarios locales de Windows usando `WMI` / `NetQueryDisplayInformation`

#### 2.2.2 UI: panel de administración de usuarios
- Pestaña "Users" visible solo para rol `Admin`
- Tabla: usuario Windows, rol actual, acciones (cambiar rol, eliminar)
- Dropdown con roles seleccionables
- Botón "Add user": selector de usuarios locales del sistema + asignación de rol
- Protección: no se puede degradar o eliminar el último Admin
- Indicador si un usuario tiene sesión activa

### 2.3 Auditoría desde UI
- Nuevo comando IPC: `GetAuditLog`
- Parámetros de filtro: usuario, acción, rango de fechas, resultado
- Paginación (página + tamaño)
- Pestaña "Audit" visible solo para rol `Admin`
- Tabla ordenable y filtrable
- Exportar log filtrado a CSV

### 2.4 Mejoras de IPC y protocolo

#### 2.4.1 Reconexión automática en la UI
- `PipeClient`: lógica de reconexión con backoff exponencial (1s, 2s, 4s, max 30s)
- Indicador visual de estado de conexión: Connected / Reconnecting / Disconnected
- Cola de comandos pendientes mientras se reconecta (opcional para Fase 2)

#### 2.4.2 Notificaciones básicas (polling mejorado)
- Mantener polling en la UI pero con intervalo configurable (default: 5s)
- Añadir campo `LastChanged` en `TunnelInfo` para detectar cambios sin refrescar toda la lista
- Preparar contrato para eventos push (implementación completa en Fase 3)

### 2.5 Validación del `.conf` de WireGuard

Implementar `WireGuardConfValidator` en `WireGuard.Shared`:
- Parser de formato INI para secciones `[Interface]` y `[Peer]`
- Validar campos obligatorios
- Validar formato de claves (base64, 32 bytes)
- Validar formato de IPs y CIDRs en `AllowedIPs` y `Address`
- Validar formato de `Endpoint` (host:port)
- Validar rangos de puerto
- Devolver lista de errores + warnings estructurados

### 2.6 Calidad

#### Tests nuevos
- `TunnelManagerTests`: mock de `Process` para `wireguard.exe`, verificar import/create/delete
- `WireGuardConfValidatorTests`: casos válidos e inválidos de `.conf`
- `RequestHandlerTests`: nuevos comandos de Fase 2
- `UserManagementViewModelTests`: añadir/editar/eliminar usuarios
- `AuditViewModelTests`: filtrado y exportación

#### Cobertura mínima objetivo: 70%
- Añadir `coverlet` con reporte HTML en `scripts/test.ps1`

### Criterios de aceptación Fase 2
- AdvancedOperator puede importar un `.conf` real y el túnel aparece en la lista
- Admin puede crear, editar y eliminar túneles desde la UI sin línea de comandos
- Admin puede añadir y gestionar usuarios desde la UI
- Admin puede ver y exportar el log de auditoría desde la UI
- La UI se reconecta automáticamente si el servicio se reinicia
- Un `.conf` malformado es rechazado con mensaje de error descriptivo
- La `PrivateKey` nunca aparece en la UI para roles no-Admin

---

## Fase 3 — Producto final y endurecimiento

> **Objetivo:** Producto listo para distribución: notificaciones en tiempo real, UX pulida, hardening de seguridad, instalador y empaquetado.

### 3.1 IPC: eventos push en tiempo real

Reemplazar el polling de estado por un modelo push desde el servicio:

#### 3.1.1 Diseño del canal de eventos
- Segundo Named Pipe dedicado a eventos: `WireGuardTunnerUI-Events`
- La UI se suscribe al canal de eventos al conectar
- El servicio emite eventos cuando cambia el estado de un túnel
- Formato: `{ "event": "TunnelStatusChanged", "tunnelName": "x", "status": "Running", "timestamp": "..." }`

#### 3.1.2 Monitor de estado en el servicio
- `TunnelStatusMonitor`: background task que observa cambios en servicios `WireGuardTunnel$*`
- Usa `ServiceController` con polling optimizado (250ms) o `EventLog` subscription para eventos de SCM
- Emite eventos al canal push cuando detecta cambios
- La UI actualiza el estado del túnel afectado sin refrescar toda la lista

### 3.2 Generación de claves en la UI

- Implementar generación de par de claves Curve25519 directamente en la UI (sin enviar la clave privada al servicio)
- Usar `System.Security.Cryptography` (ECDiffieHellman con curva X25519)
- Flujo: la UI genera el par → muestra la clave pública al usuario → solo la clave pública viaja al servicio en el formulario de creación/edición
- La clave privada nunca sale del proceso de la UI

### 3.3 UX y UI avanzada

#### 3.3.1 System tray (bandeja del sistema)
- Icono en la bandeja del sistema: verde (algún túnel conectado), gris (todos desconectados), rojo (error)
- Menú contextual: lista quick de túneles con toggle connect/disconnect, "Abrir", "Salir"
- La ventana principal se minimiza a la bandeja en lugar de cerrarse

#### 3.3.2 Ventana de detalle de túnel
- Panel lateral o ventana secundaria con información completa del túnel:
  - Configuración (sin PrivateKey si no es Admin)
  - Estadísticas (bytes transferidos — leer de la interfaz WireGuard si disponible)
  - Últimos eventos de auditoría del túnel
  - Historial de conexiones

#### 3.3.3 Notificaciones de escritorio (Windows Toast)
- Notificación cuando un túnel cambia de estado inesperadamente (e.g., se desconecta solo)
- Notificación cuando se le concede/retira acceso a un usuario
- Usar `Microsoft.Toolkit.Uwp.Notifications` o `Windows.UI.Notifications`

#### 3.3.4 Localización (i18n)
- Extraer todos los strings de la UI a archivos de recursos `.resx`
- Soporte inicial: Español e Inglés
- Estructura para añadir más idiomas fácilmente

#### 3.3.5 Accesibilidad
- Compatibilidad con lectores de pantalla (Narrator)
- Navegación completa por teclado
- Contraste suficiente en ambos modos (claro/oscuro)

### 3.4 Endurecimiento de seguridad

#### 3.4.1 Integridad del ejecutable de la UI
- El servicio verifica la firma digital del proceso cliente antes de aceptar la primera petición
- Usar `GetProcessById` + `Module.FileName` + `AuthenticodeTools` o `WinTrustVerify`
- Rechazar conexiones de procesos sin firma válida o con firma de editor desconocido
- Configurable: modo estricto (solo binarios firmados) / modo desarrollo (any)

#### 3.4.2 Rotación periódica de la clave de auditoría
- El archivo `audit.jsonl` se rota diariamente
- Archivos antiguos se comprimen y se mantienen N días (configurable)
- Protegidos con DPAPI individualmente

#### 3.4.3 Rate limiting en el pipe
- El servicio limita el número de peticiones por cliente por segundo
- Protección contra DoS local (un proceso enviando miles de peticiones)
- Límite configurable en `appsettings.json`

#### 3.4.4 Logs de seguridad en el Event Log de Windows
- Eventos críticos (acceso denegado, pipe squatting, usuario no registrado) se escriben también en el Windows Event Log
- Source: `WireGuard-WinUserUI`
- Facilita integración con SIEM corporativos

#### 3.4.5 Hardening del proceso de servicio
- Revisar y minimizar privilegios del servicio (usar cuenta de servicio dedicada en lugar de LocalSystem)
- Aplicar `SetProcessMitigationPolicy` para protección adicional: DEP, ASLR forzado, CFG
- Documentar cuenta de servicio recomendada en el instalador

### 3.5 Configuración y políticas

#### 3.5.1 Archivo `appsettings.json` del servicio
```json
{
  "WireGuardManager": {
    "TunnelRefreshIntervalMs": 5000,
    "AuditLogRetentionDays": 90,
    "MaxConnectionsPerSecond": 10,
    "SignatureVerification": "Strict",
    "WireGuardExePath": "C:\\Program Files\\WireGuard\\wireguard.exe",
    "TunnelConfDirectory": "C:\\ProgramData\\WireGuard"
  }
}
```

#### 3.5.2 Soporte de políticas de grupo (GPO)
- Plantilla ADMX para configurar el servicio via GPO
- Políticas: qué roles están habilitados, qué operaciones se permiten por dominio, logging obligatorio

### 3.6 Instalador y distribución

#### 3.6.1 Instalador WiX (MSI)
Estructura del instalador:
1. **Prerrequisitos:** verificar que WireGuard para Windows está instalado; si no, mostrar enlace de descarga
2. **Instalación del servicio:**
   - Copiar binarios a `C:\Program Files\WireGuard-WinUserUI\`
   - Registrar el servicio Windows: `sc.exe create WireGuard-WinUserUI binPath=...`
   - Configurar inicio automático del servicio
   - Crear directorio de datos: `C:\ProgramData\WireGuard-WinUserUI\` con permisos correctos
3. **Bootstrap de admin:**
   - Configurar el usuario que lanza el instalador (o uno especificado) como primer Admin
4. **Acceso directo:**
   - Crear acceso directo en el menú Inicio para la UI
   - Configurar al inicio de sesión del usuario (Task Scheduler o Run key para la UI)
5. **Desinstalación:**
   - Detener y eliminar el servicio
   - Preguntar si eliminar datos (`roles.json`, `audit.jsonl`)

**Artefactos:**
- `WireGuard-WinUserUI-Setup-x64.msi`
- Firma del MSI con certificado de código

#### 3.6.2 Empaquetado Chocolatey
Actualizar `packaging/chocolatey/`:
- `chocolateyInstall.ps1`:
  - Verificar prerrequisitos (WireGuard instalado)
  - Descargar MSI desde URL verificada con hash SHA256
  - Instalar MSI silenciosamente: `Start-ChocolateyPackage`
- `chocolateyUninstall.ps1`:
  - Desinstalar MSI y limpiar datos (con confirmación)
- Publicar en Chocolatey Community Repository o feed privado

#### 3.6.3 Script de actualización
- `scripts/update.ps1`: detener servicio → instalar nueva versión → conservar datos → reiniciar servicio
- Migración de `roles.json` si cambia el schema entre versiones

### 3.7 Calidad y CI/CD

#### 3.7.1 Tests de integración
- `IntegrationTests/`: proyecto adicional que levanta el servicio real en modo test y lanza la UI contra él
- Test: un usuario Viewer no puede start/stop (end-to-end sobre Named Pipe real)
- Test: importar un `.conf` de prueba y verificar que aparece en la lista
- Test: verificar que el audit log recibe la entrada correcta tras cada operación

#### 3.7.2 Pipeline CI (GitHub Actions o similar)
```
on: [push, pull_request]
jobs:
  build:
    - dotnet restore
    - dotnet build --configuration Release
    - dotnet test --collect:"XPlat Code Coverage"
    - Publicar reporte de cobertura
  package:
    - dotnet publish (self-contained, win-x64)
    - Construir MSI con WiX
    - Firmar MSI
    - Subir artefactos
```

#### 3.7.3 Publicación self-contained
- `dotnet publish -r win-x64 --self-contained true -p:PublishSingleFile=true`
- Eliminar dependencia del runtime .NET instalado en el sistema destino
- Separar publicación del Servicio y de la UI

### 3.8 Documentación final

#### Para usuarios
- Guía de instalación y requisitos del sistema
- Manual de uso por rol (Viewer, Operator, AdvancedOperator, Admin)
- FAQ: troubleshooting de conexión al servicio, permisos, logs

#### Para administradores
- Guía de despliegue empresarial
- Configuración de GPO
- Integración con SIEM (formato de eventos, campos)
- Procedimiento de backup y restauración de `roles.json`

#### Para desarrolladores
- Guía de contribución
- Arquitectura detallada (actualizar `docs/architecture.md`)
- Referencia del protocolo IPC (comandos, schemas JSON)
- Cómo añadir un nuevo comando IPC (checklist de 5 pasos)

### Criterios de aceptación Fase 3
- Todas las operaciones se reflejan en la UI en tiempo real sin polling
- La UI tiene icono en bandeja del sistema y notificaciones Toast
- El instalador MSI funciona en un sistema limpio con WireGuard preinstalado
- El paquete Chocolatey instala correctamente desde cero
- El servicio rechaza conexiones de procesos sin firma digital válida (modo estricto)
- La cobertura de tests es ≥ 70%
- El pipeline CI pasa en cada push
- Ningún secreto (PrivateKey) aparece en logs, UI para roles no-Admin, ni en tráfico IPC

---

## Resumen de hitos

| Hito | Contenido principal | Estado |
|------|--------------------|----|
| **Fase 1 — MVP** | Servicio + IPC + RBAC + UI básica + auditoría | ✅ Completa |
| **Fase 2.1** | Reiniciar túneles + import `.conf` | ⏳ Siguiente |
| **Fase 2.2** | Crear y editar túneles desde UI | ⏳ Pendiente |
| **Fase 2.3** | Eliminar + exportar túneles | ⏳ Pendiente |
| **Fase 2.4** | Gestión de usuarios desde UI | ⏳ Pendiente |
| **Fase 2.5** | Auditoría desde UI + exportar CSV | ⏳ Pendiente |
| **Fase 2.6** | Reconexión automática + tests Fase 2 | ⏳ Pendiente |
| **Fase 3.1** | Eventos push en tiempo real | ⏳ Pendiente |
| **Fase 3.2** | Generación de claves en cliente | ⏳ Pendiente |
| **Fase 3.3** | System tray + notificaciones + i18n | ⏳ Pendiente |
| **Fase 3.4** | Verificación de firma del cliente + rate limiting + Event Log | ⏳ Pendiente |
| **Fase 3.5** | Configuración avanzada + soporte GPO | ⏳ Pendiente |
| **Fase 3.6** | Instalador MSI + Chocolatey | ⏳ Pendiente |
| **Fase 3.7** | CI/CD + tests integración + self-contained | ⏳ Pendiente |

---

## Matriz de permisos final

| Operación | Viewer | Operator | Advanced Operator | Admin |
|-----------|--------|----------|-------------------|-------|
| Ver túneles y estado | ✅ | ✅ | ✅ | ✅ |
| Conectar / Desconectar | ❌ | ✅ | ✅ | ✅ |
| Reiniciar túnel | ❌ | ✅ | ✅ | ✅ |
| Crear túnel | ❌ | ❌ | ✅ | ✅ |
| Editar túnel | ❌ | ❌ | ✅ | ✅ |
| Importar `.conf` | ❌ | ❌ | ✅ | ✅ |
| Eliminar túnel | ❌ | ❌ | ✅ | ✅ |
| Exportar `.conf` | ❌ | ❌ | ❌ | ✅ |
| Ver PrivateKey | ❌ | ❌ | ❌ | ✅ |
| Gestionar usuarios | ❌ | ❌ | ❌ | ✅ |
| Ver log de auditoría | ❌ | ❌ | ❌ | ✅ |
| Exportar log auditoría | ❌ | ❌ | ❌ | ✅ |
| Configurar sistema | ❌ | ❌ | ❌ | ✅ |
