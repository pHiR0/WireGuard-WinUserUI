# 🧠 PROMPT PARA IA (Claude / Codex)
## Proyecto: WireGuard Manager para usuarios no administradores en Windows

---

## 🎯 CONTEXTO

Estoy desarrollando una aplicación para Windows que permita a usuarios **NO administradores** gestionar túneles WireGuard de forma segura.

WireGuard en Windows:

- Solo permite usar su UI desde cuentas administradoras
- Requiere privilegios elevados para:
  - crear interfaces de red
  - modificar routing
  - activar/desactivar túneles
- Los túneles pueden instalarse como servicios (`WireGuardTunnel$NOMBRE`) :contentReference[oaicite:0]{index=0}  
- Dichos servicios pueden controlarse mediante el gestor de servicios (`sc`, `net start/stop`) :contentReference[oaicite:1]{index=1}  

---

## ⚠️ RESTRICCIONES CRÍTICAS (NO NEGOCIABLES)

1. ❌ No usar la UI oficial de WireGuard
2. ❌ No depender de elevación UAC en la UI
3. ❌ No almacenar credenciales de administrador
4. ❌ No ejecutar comandos privilegiados desde la UI directamente
5. ❌ No asumir que el cliente es de confianza (validar todo en backend)
6. ❌ No exponer API en red (solo local)

---

## 🏗️ ARQUITECTURA OBLIGATORIA

El sistema DEBE tener dos componentes:

### 1. Servicio Windows (backend privilegiado)

- Ejecutado como:
  - LocalSystem o cuenta con privilegios equivalentes
- Siempre activo
- Responsable de:
  - ejecutar operaciones sobre WireGuard
  - validar permisos
  - aplicar reglas de autorización
  - exponer API local segura

---

### 2. Aplicación cliente (UI)

- Ejecutada como usuario normal
- Sin elevación
- Responsable de:
  - mostrar túneles
  - enviar peticiones al servicio
  - mostrar estado
  - aplicar UX

---

## 🔌 COMUNICACIÓN (IPC)

La comunicación entre UI y servicio DEBE ser:

- Local (no red)
- Autenticada
- Segura

Opciones válidas:
- Named Pipes (preferido)
- gRPC local con seguridad
- RPC Windows

Requisitos:
- identificar el usuario llamante
- impedir acceso a procesos no autorizados
- validar identidad en backend

---

## 🔐 MODELO DE SEGURIDAD

### Principio clave:
> La UI nunca tiene privilegios. El servicio decide todo.

---

## 👥 MODELO DE USUARIOS Y ROLES

Implementar RBAC (Role-Based Access Control)

### Roles mínimos:

#### 1. Viewer
- ver túneles
- ver estado
- NO puede modificar nada

#### 2. Operator
- conectar/desconectar túneles
- ver estado

#### 3. Advanced Operator
- crear túneles
- editar túneles
- eliminar túneles

#### 4. Admin
- todo lo anterior
- gestionar usuarios
- asignar roles
- ver auditoría
- configurar sistema

---

## 🔒 AUTORIZACIÓN

- El servicio debe identificar al usuario Windows que hace la petición
- Resolver su rol
- Validar operación

Ejemplo:


Usuario: juan
Rol: Operator
Acción: editar túnel

→ DENEGADO


---

## ⚙️ FUNCIONALIDADES (REQUISITOS)

### 🔹 Gestión de túneles

- Listar túneles existentes (`WireGuardTunnel$*`)
- Obtener estado:
  - Running
  - Stopped
  - Error
- Conectar túnel
- Desconectar túnel
- Reiniciar túnel

---

### 🔹 Gestión avanzada (según rol)

- Crear túnel
- Editar túnel
- Eliminar túnel
- Importar `.conf`
- Exportar configuración (opcional)

---

### 🔹 Administración

- Listar usuarios locales del sistema
- Permitir autorizar usuarios
- Asignar rol
- Revocar acceso

---

### 🔹 Auditoría

Registrar eventos:

- start/stop túnel
- creación/modificación
- login lógico (uso de app)
- errores
- accesos denegados

---

## 🧱 INTEGRACIÓN CON WIREGUARD

La IA DEBE usar el modelo de servicio de WireGuard:

### Instalación de túnel:

wireguard.exe /installtunnelservice C:\ruta\config.conf


### Resultado:
- Se crea servicio:

WireGuardTunnel$NOMBRE


### Control:

Start-Service WireGuardTunnel$NOMBRE
Stop-Service WireGuardTunnel$NOMBRE


⚠️ Estas operaciones requieren privilegios → deben ejecutarse SOLO en el servicio backend.

---

## 🗂️ GESTIÓN DE CONFIGURACIONES

- Los `.conf` deben validarse antes de aplicar
- Nunca sobrescribir sin backup
- Validar:
  - sintaxis
  - campos obligatorios
  - formato

---

## 🔑 GESTIÓN DE SECRETOS

- NO exponer `PrivateKey` en UI salvo rol Admin
- NO guardar claves en texto plano adicional
- usar mecanismos seguros del sistema si es necesario

---

## 🧾 LOGGING

Implementar:

- logs estructurados
- logs de auditoría

Formato mínimo:


timestamp
usuario
acción
túnel
resultado
error (si aplica)


---

## ⚙️ TECNOLOGÍAS RECOMENDADAS

### Backend (servicio)
- C# (.NET 6+ o superior)
- Windows Service

### IPC
- Named Pipes

### UI
- WPF o WinUI

### Persistencia
- SQLite o JSON protegido

---

## 🚫 ANTÍPATRONES A EVITAR

- Ejecutar `wireguard.exe` desde la UI
- Usar `runas`
- Guardar passwords de admin
- Ejecutar scripts arbitrarios
- confiar en validación del cliente
- mezclar lógica de UI y lógica privilegiada

---

## 🧪 CRITERIOS DE ACEPTACIÓN

La solución será válida si:

- Un usuario NO admin puede:
  - ver túneles
  - conectar/desconectar (según rol)
- No aparece ningún prompt UAC
- No se usan credenciales admin en runtime
- Los permisos se respetan estrictamente
- El servicio controla TODA la lógica privilegiada
- Hay trazabilidad completa de acciones

---

## 🧭 FASES DE IMPLEMENTACIÓN

### Fase 1 (MVP)
- Servicio funcionando
- IPC básico
- listar túneles
- start/stop
- roles básicos
- auditoría mínima

---

### Fase 2
- edición de túneles
- import/export
- UI completa
- gestión de usuarios

---

### Fase 3
- mejoras UX
- políticas avanzadas
- seguridad reforzada
- optimización

---

## 🧠 EXPECTATIVA DE LA IA

La IA debe:

- generar arquitectura completa
- proponer estructura de proyecto
- implementar código base funcional
- incluir validación de seguridad
- NO simplificar decisiones críticas
- NO ignorar restricciones

---

## 📌 NOTA FINAL

Esto NO es un wrapper de WireGuard.

Es una capa de control privilegiado con delegación segura.

Cualquier implementación que:
- ejecute directamente comandos desde UI
- o evite el servicio

→ es incorrecta.