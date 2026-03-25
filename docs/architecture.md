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
