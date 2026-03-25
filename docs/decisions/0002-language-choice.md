# ADR 0002 - Elección de lenguaje y stack tecnológico

## Estado

Propuesto

---

## Contexto

El proyecto **WireGuard-WinUserUI** requiere el desarrollo de:

- Un **servicio de Windows privilegiado**
- Una **aplicación UI para usuario no administrador**
- Comunicación segura entre ambos (IPC local)
- Gestión de permisos (RBAC)
- Integración con el sistema operativo Windows:
  - servicios
  - red
  - procesos
  - seguridad
- Generación de binarios (`.exe`)
- Instalador (MSI u otro)
- Empaquetado (Chocolatey)

Además, existe un requisito adicional de entorno de desarrollo:

> **El proyecto debe poder desarrollarse desde Visual Studio Code**

Esto implica que el stack elegido debe ser compatible con un flujo de trabajo basado en:

- VS Code como editor principal
- terminal integrada
- build y test por CLI
- depuración desde VS Code
- tareas configurables (`tasks.json`)
- configuración de depuración (`launch.json`)

---

## Problema

Debemos elegir el lenguaje y stack tecnológico que:

- maximice integración con Windows
- minimice riesgos de seguridad
- permita mantenibilidad a largo plazo
- facilite desarrollo de UI + servicio + IPC
- sea adecuado para un proyecto con lógica de seguridad (roles, validación, privilegios)
- pueda desarrollarse de forma razonable desde **Visual Studio Code**

---

## Alternativas consideradas

### 1. C# / .NET (Windows-first)

#### Ventajas
- Integración nativa con Windows:
  - Windows Services
  - Named Pipes
  - gestión de procesos
  - ACLs y seguridad
- Soporte oficial y maduro
- Framework moderno (.NET 6/7/8)
- Buen soporte para:
  - IPC
  - serialización
  - logging
- Ecosistema sólido
- Fácil generación de `.exe`
- Buen soporte para MSI/instaladores
- Muy adecuado para RBAC y lógica empresarial
- Compatible con desarrollo desde VS Code mediante:
  - extensión C#
  - `dotnet build`
  - `dotnet run`
  - `dotnet test`
  - tareas y launch configurations

#### Desventajas
- Ligero overhead frente a lenguajes de más bajo nivel
- Dependencia del runtime (.NET), aunque puede publicarse self-contained
- El desarrollo de UI Windows nativa (especialmente WPF) es más cómodo en Visual Studio completo que en VS Code
- El diseñador visual de interfaces no es una fortaleza de VS Code

---

### 2. PowerShell

#### Ventajas
- Rápido para prototipos
- Integración con Windows
- Ideal para scripting y automatización
- Muy cómodo en VS Code

#### Desventajas
- Mala base para aplicaciones complejas
- UI limitada
- Difícil mantener lógica compleja (RBAC, validación)
- Problemas de escalabilidad y mantenibilidad
- Poco adecuado para producto final

---

### 3. C++ (WinAPI)

#### Ventajas
- Máximo control
- Alto rendimiento
- Sin runtime adicional

#### Desventajas
- Complejidad muy alta
- Mayor riesgo de bugs de seguridad
- Desarrollo más lento
- UI más compleja de implementar
- Flujo de trabajo más pesado en VS Code para este tipo de aplicación
- Overkill para este proyecto

---

### 4. Go

#### Ventajas
- Binarios estáticos
- Buen soporte para concurrencia
- Código relativamente simple
- Flujo CLI cómodo en VS Code

#### Desventajas
- Integración con Windows menos natural
- UI limitada o poco idiomática en Windows
- IPC más manual
- Ecosistema menos alineado con Windows desktop apps

---

### 5. Electron / Node.js

#### Ventajas
- UI rápida de desarrollar
- multiplataforma
- VS Code encaja muy bien como entorno de desarrollo

#### Desventajas
- Consumo de recursos elevado
- Mala integración con servicios Windows
- Seguridad más compleja
- No adecuado para lógica privilegiada sensible
- Introduce demasiadas capas para un proyecto Windows-first

---

## Criterios de decisión

Los criterios clave para este proyecto son:

1. Seguridad
2. Integración con Windows
3. Mantenibilidad
4. Facilidad de implementar RBAC
5. Soporte de IPC seguro
6. Facilidad para crear servicio Windows
7. Capacidad de crear UI robusta
8. Soporte de packaging (MSI, Chocolatey)
9. Compatibilidad razonable con un flujo de trabajo en **Visual Studio Code**

---

## Decisión propuesta

Se selecciona:

> **C# con .NET (versión 8 o superior, preferiblemente)**

---

## Justificación

C#/.NET permite:

- Implementar fácilmente un **servicio Windows robusto**
- Crear una **UI nativa de Windows**
- Usar **Named Pipes** para IPC seguro y eficiente
- Integrar correctamente con el sistema de seguridad de Windows
- Implementar lógica compleja (RBAC, validaciones) de forma mantenible
- Generar binarios `.exe` fácilmente
- Integrarse con pipelines de build modernos
- Trabajar desde **Visual Studio Code** con un flujo basado en CLI y depuración configurada
- Escalar el proyecto sin rehacer la base tecnológica

El requisito de usar VS Code **no invalida** el uso de C#/.NET.  
Sí obliga, en cambio, a evitar dependencias innecesarias en herramientas exclusivas de Visual Studio.

---

## Decisiones derivadas

Si se adopta C#/.NET:

### Backend
- Worker Service o Windows Service en .NET

### UI
Hay que tomar una decisión adicional, porque aquí sí afecta el requisito de VS Code.

Opciones:

#### Opción A - WPF
**Ventajas**
- madura
- nativa
- muy adecuada para apps de escritorio Windows

**Desventajas**
- mejor experiencia de desarrollo en Visual Studio completo
- en VS Code la edición es más manual
- menor comodidad para diseño visual

#### Opción B - WinUI
**Ventajas**
- stack más moderno
- alineado con ecosistema Windows reciente

**Desventajas**
- tooling más dependiente de entorno Microsoft
- puede complicar más el flujo si se quiere trabajar solo desde VS Code

#### Opción C - Avalonia
**Ventajas**
- muy usable desde VS Code
- enfoque moderno
- tooling razonable por CLI
- menos dependencia del ecosistema Visual Studio completo

**Desventajas**
- no es la UI nativa clásica de Windows
- integración con algunos patrones Windows puede ser menos directa

### IPC
- Named Pipes (preferido)

### Persistencia
- SQLite o JSON protegido

### Logging
- `Microsoft.Extensions.Logging`

---

## Recomendación refinada

Dado que existe el requisito explícito de trabajar desde **Visual Studio Code**, la recomendación queda refinada así:

- **Lenguaje principal:** C#
- **Framework base:** .NET 8+
- **Servicio backend:** Windows Service en .NET
- **IPC:** Named Pipes
- **Scripts auxiliares:** PowerShell
- **UI:** decidir entre:
  - **WPF**, si se prioriza integración Windows clásica y se acepta una experiencia menos cómoda en VS Code
  - **Avalonia**, si se prioriza productividad real desde VS Code y una experiencia de desarrollo más fluida

---

## Alternativa descartada explícitamente

PowerShell se descarta como base principal por:

- baja mantenibilidad
- falta de estructura para RBAC
- dificultad en UI
- riesgo de derivar en código difícil de mantener

También se descarta usar Electron/Node.js como stack principal por:

- exceso de capas
- peor encaje con servicio Windows privilegiado
- coste innecesario en consumo y complejidad

---

## Riesgos

- Dependencia de .NET runtime (mitigable con publicación self-contained)
- Si se elige WPF, la experiencia en VS Code será menos cómoda que en Visual Studio completo
- Si se elige Avalonia, habrá que validar bien la experiencia de integración con el resto de requisitos Windows-first

---

## Consecuencias

### Positivas
- Arquitectura sólida y mantenible
- Integración fuerte con Windows
- Base adecuada para seguridad y control de accesos
- Posibilidad real de trabajar desde VS Code

### Negativas
- Mayor complejidad inicial que un script
- La decisión de framework UI requiere un ADR específico adicional
- Puede haber fricción si se insiste en usar exclusivamente VS Code para tareas que Visual Studio resuelve mejor

---

## Siguiente decisión requerida

Debe abrirse un ADR adicional para decidir el framework de UI:

> **ADR 0003 - Elección del framework de interfaz gráfica**

Ese ADR debe comparar al menos:

- WPF
- WinUI
- Avalonia

con especial peso en:

- desarrollo desde VS Code
- integración con Windows
- mantenibilidad
- facilidad de depuración
- coste de implementación

---

## Conclusión

El requisito de desarrollar desde Visual Studio Code no cambia la conclusión principal:

> **C#/.NET sigue siendo la mejor base para el proyecto**

Sin embargo, sí introduce una decisión adicional importante:

> **la elección del framework UI debe considerar explícitamente la productividad real en VS Code**