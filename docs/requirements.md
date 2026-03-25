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
