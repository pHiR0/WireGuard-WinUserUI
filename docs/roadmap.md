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

## Fase 4
- Hardening del canal IPC (deny NetworkSid, SID identity, rate limiting)
- Tests unitarios e integración (96 tests)

## Fase 5
- Compilación y empaquetado
- Fichero .sln de solución
- Publicación self-contained single-file (win-x64)
- Instalador MSI con WiX v5 SDK
- Servicio Windows con inicio automático
- Acceso directo en Menú Inicio
- MajorUpgrade para actualizaciones in-place
- Scripts: build.ps1, test.ps1, package.ps1
