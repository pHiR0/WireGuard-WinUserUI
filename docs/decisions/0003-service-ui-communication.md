# ADR 0003 - Mecanismo de comunicación entre Servicio y UI

## Estado

Propuesto

---

## Contexto

El proyecto **WireGuard-WinUserUI** se basa en una arquitectura de dos componentes:

1. Un **servicio Windows privilegiado**, encargado de ejecutar operaciones sensibles
2. Una **aplicación UI**, ejecutada por usuarios no administradores

Ambos componentes deben comunicarse de forma segura, fiable y eficiente dentro del mismo sistema.

---

## Problema

Definir el mecanismo de comunicación entre la UI y el servicio que permita:

- ejecutar comandos (start/stop túneles, etc.)
- consultar estado
- aplicar autorización por usuario
- mantener seguridad y aislamiento
- permitir evolución futura del sistema

---

## Objetivos

El mecanismo de comunicación debe permitir:

- comunicación **local (misma máquina)**
- identificación fiable del usuario que realiza la petición
- envío de comandos y recepción de respuestas
- manejo de errores estructurados
- soporte para múltiples operaciones concurrentes
- capacidad de auditoría
- extensibilidad futura

---

## Restricciones

- ❌ No exponer API en red por defecto
- ❌ No depender de UI oficial de WireGuard
- ❌ No almacenar credenciales administrativas
- ❌ No ejecutar lógica privilegiada en la UI
- ❌ No confiar en validaciones realizadas en cliente

---

## Requisitos funcionales

El canal de comunicación debe soportar:

- Request/Response:
  - conectar túnel
  - desconectar túnel
  - listar túneles
  - consultar estado

- Opcional (futuro):
  - notificación de eventos (estado de túneles)
  - streaming de logs
  - suscripción a cambios

---

## Requisitos no funcionales

### Seguridad
- autenticación del cliente
- identificación del usuario Windows llamante
- autorización en backend
- aislamiento frente a procesos no autorizados

### Rendimiento
- baja latencia
- bajo overhead

### Robustez
- manejo de errores
- recuperación ante fallos
- no bloqueo del sistema

### Mantenibilidad
- contrato claro
- facilidad de evolución
- facilidad de test

### Compatibilidad
- integración con Windows
- compatible con desarrollo desde VS Code
- soporte en .NET

---

## Modelos de interacción posibles

### 1. Modelo request/response
- la UI envía una petición
- el servicio responde

### 2. Modelo event-driven
- el servicio emite eventos
- la UI se suscribe

### 3. Modelo híbrido
- comandos por request/response
- estado por eventos

---

## Alternativas consideradas

---

### 1. Named Pipes (Windows IPC)

#### Descripción
Comunicación local mediante pipes del sistema operativo.

#### Ventajas
- IPC nativo de Windows
- no expone red
- buena integración con .NET
- permite identificar el cliente
- buen rendimiento
- control de acceso mediante ACLs

#### Desventajas
- específico de Windows
- protocolo debe definirse manualmente

---

### 2. gRPC sobre transporte local

#### Descripción
Uso de gRPC con transporte local (pipes o sockets restringidos).

#### Ventajas
- contratos bien definidos
- tipado fuerte
- soporte para streaming
- escalable
- buena separación de responsabilidades

#### Desventajas
- mayor complejidad inicial
- posible sobreingeniería para MVP

---

### 3. HTTP local (loopback)

#### Descripción
El servicio expone una API en `127.0.0.1`.

#### Ventajas
- fácil de entender
- fácil de probar
- herramientas estándar

#### Desventajas
- abre puerto local
- mayor superficie de ataque
- identificación del usuario menos directa
- requiere autenticación adicional

---

### 4. RPC clásico / COM / WCF

#### Descripción
Uso de tecnologías tradicionales de Windows para comunicación entre procesos.

#### Ventajas
- integración profunda con Windows
- modelos de seguridad existentes

#### Desventajas
- complejidad
- tecnologías en parte legacy
- menor mantenibilidad

---

### 5. Sistema de archivos (cola de comandos)

#### Descripción
Intercambio mediante archivos en directorios monitorizados.

#### Ventajas
- simple
- fácil de depurar

#### Desventajas
- frágil
- problemas de concurrencia
- pobre rendimiento
- difícil de securizar

---

### 6. CLI intermedia

#### Descripción
La UI invoca una CLI que actúa como intermediaria con el servicio.

#### Ventajas
- reutilización para scripting
- testabilidad

#### Desventajas
- capa adicional
- posible duplicación de lógica
- complejidad innecesaria en MVP

---

### 7. Memoria compartida

#### Descripción
Comunicación mediante regiones de memoria compartida.

#### Ventajas
- alto rendimiento

#### Desventajas
- complejidad elevada
- difícil de asegurar
- innecesario para este caso

---

### 8. Base de datos local como canal

#### Descripción
Uso de base de datos (ej. SQLite) como intermediario de comandos.

#### Ventajas
- persistencia
- trazabilidad

#### Desventajas
- no es IPC natural
- latencia
- complejidad innecesaria

---

### 9. Broker interno / Message Bus local

#### Descripción
Uso de un bus interno en el servicio para desacoplar lógica.

#### Ventajas
- arquitectura limpia
- escalabilidad interna
- separación de responsabilidades

#### Desventajas
- complejidad adicional
- no sustituye el canal IPC externo

---

## Comparación de alto nivel

| Alternativa            | Seguridad | Complejidad | Integración Windows | Escalabilidad |
|----------------------|----------|------------|---------------------|--------------|
| Named Pipes          | Alta     | Baja       | Alta                | Media        |
| gRPC local           | Alta     | Media      | Media-Alta          | Alta         |
| HTTP local           | Media    | Baja       | Media               | Alta         |
| RPC/COM              | Alta     | Alta       | Alta                | Media        |
| Archivos             | Baja     | Baja       | Media               | Baja         |
| CLI intermedia       | Media    | Media      | Alta                | Media        |
| Memoria compartida   | Baja     | Alta       | Alta                | Baja         |
| DB como canal        | Baja     | Media      | Media               | Baja         |
| Message Bus interno  | Alta     | Media      | Alta                | Alta         |

---

## Cuestiones abiertas

Antes de tomar una decisión final, deben responderse:

- ¿Se requiere soporte de eventos en tiempo real?
- ¿Se permitirá más de un cliente en el futuro?
- ¿Se necesita versionado del protocolo?
- ¿Se prioriza simplicidad o escalabilidad?
- ¿Se necesita depuración sencilla desde herramientas externas?
- ¿Cómo se autentica exactamente el cliente?
- ¿Cómo se controlará el acceso de procesos locales no autorizados?

---

## Riesgos

- elegir un mecanismo demasiado complejo para el MVP
- elegir uno demasiado simple que obligue a rehacer arquitectura
- introducir vulnerabilidades en el canal IPC
- no definir correctamente autenticación/autorización

---

## Siguiente paso

Definir en un ADR posterior:

> Selección del mecanismo de comunicación definitivo

Incluyendo:
- protocolo
- modelo de seguridad
- formato de mensajes
- estrategia de implementación