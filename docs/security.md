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
