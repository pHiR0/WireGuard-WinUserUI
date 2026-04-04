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

## Hardening del canal IPC (Fase 4)

### ACL del named pipe
- **DENY NetworkSid** — Deniega explícitamente el acceso remoto al pipe (defensa en profundidad).
- **SYSTEM FullControl** — Acceso total para el servicio Windows.
- **Administrators FullControl** — Acceso total para administradores locales.
- **AuthenticatedUsers ReadWrite** — Lectura/escritura para usuarios locales autenticados.

### Anti-squatting
- `FILE_FLAG_FIRST_PIPE_INSTANCE` (0x00080000) en la primera instancia del pipe. Si otro proceso ya creó un pipe con el mismo nombre, `CreateNamedPipe` falla con `ACCESS_DENIED`.

### Identificación SID del llamante
- `RunAsClient()` impersona temporalmente al cliente conectado para obtener su `WindowsIdentity` (SID, nombre, `IsAuthenticated`).
- Callers no autenticados o sin SID válido son rechazados inmediatamente.
- Más robusto que `GetImpersonationUserName()` que sólo devuelve un string.

### Rate limiting por usuario
- Máximo 4 conexiones concurrentes por SID de usuario.
- Previene que un usuario agote la capacidad del servidor (DoS local via pipe).

### Logging de seguridad
- Conexión/desconexión de clientes con SID.
- Eventos de rechazo por identidad inválida o rate limiting.
- Detección de pipe-squatting en arranque.
