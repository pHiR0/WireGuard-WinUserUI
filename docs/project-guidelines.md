# Project Guidelines

> **Purpose:** Living reference document. **Review this file before starting any implementation.**
> Update it whenever a convention is established, a decision is made, or a pattern is adopted.

---

## Table of Contents

1. [Color Scheme](#1-color-scheme)
2. [Naming Conventions](#2-naming-conventions)
3. [IPC Protocol Rules](#3-ipc-protocol-rules)
4. [RBAC Rules](#4-rbac-rules)
5. [Testing Conventions](#5-testing-conventions)
6. [Glossary](#6-glossary)

---

## 1. Color Scheme

The UI uses a **dark professional palette** targeting network/security tooling aesthetics. The window theme is forced to `Dark` via `RequestedThemeVariant="Dark"` on the `<Window>` element so the palette remains consistent regardless of the user's system theme.

### Palette Reference

| Token | Hex Value | Usage |
|-------|-----------|-------|
| `SurfaceCard` | `#252B3A` | Tunnel cards, user cards, audit entry cards, form panels |
| `SurfaceBorder` | `#3B4261` | Card/panel borders when explicit separation is needed |
| `TextPrimary` | Managed by FluentTheme Dark | Main labels, names, headings — do NOT hardcode |
| `TextSecondary` | `#94A3B8` | Status text, metadata, timestamps, "User: / Role:" labels |
| `AccentConnected` | `#4ADE80` | Connected/service indicator dot in the header |
| `StatusRunning` | `#4ADE80` | Tunnel running status dot |
| `StatusStopped` | `#64748B` | Tunnel stopped status dot |
| `StatusPending` | `#FBBF24` | Start/stop pending status dot |
| `StatusError` | `#F87171` | Tunnel error status dot, error inline text |
| `BannerError_Bg` | `#3D1010` | Background for error/warning banners |
| `BannerError_Fg` | `#FCA5A5` | Text foreground on error banners |
| `BannerSuccess_Bg` | `#0D2B1A` | Background for success/valid banners |
| `BannerSuccess_Fg` | `#86EFAC` | Text foreground on success banners |
| `DeleteFg` | `#F87171` | Foreground for destructive action buttons (Remove, Delete) |
| `ErrorInline` | `#FB923C` | Inline error text within cards (e.g. audit error field) |

### Rules

- **Never use** `Background="#F5F5F5"` — this is near-white and blends into light-mode windows.
- **Never use** `Foreground="Gray"` — use `#94A3B8` explicitly for secondary text.
- Panel/card `Background` must always be `#252B3A`.
- Status dots/indicators must use the palette values above, not generic red/green.
- For card borders, prefer `CornerRadius="8"` over explicit border strokes unless visual
  separation is insufficient without a border color.
- Import tab validation:
  - Valid conf → `BannerSuccess_Bg` / `BannerSuccess_Fg`
  - Invalid conf → `BannerError_Bg` / `BannerError_Fg`
  - Error message → `BannerError_Bg` / `BannerError_Fg`

---

## 2. Naming Conventions

### Projects & Namespaces

| Component | Project Name | Root Namespace |
|-----------|-------------|----------------|
| Shared contracts (DTOs, IPC enums) | `WireGuard.Shared` | `WireGuard.Shared.*` |
| Windows Service backend | `WireGuard.Service` | `WireGuard.Service.*` |
| Avalonia UI frontend | `WireGuard.UI` | `WireGuard.UI.*` |
| Service unit tests | `WireGuard.Service.Tests` | (mirrors service) |
| UI unit tests | `WireGuard.UI.Tests` | (mirrors UI) |

### C# Conventions

- **ViewModels**: suffix `ViewModel`, placed in `src/UI/ViewModels/`, implement `ObservableObject` (CommunityToolkit.Mvvm).
- **Async commands**: use `[RelayCommand]` attribute. Method name uses no suffix (e.g. `LoadUsers` → generates `LoadUsersCommand`).
- **IPC command handlers**: added as a `case` in `RequestHandler.ExecuteAsync` switch. Never add command-side logic to the ViewModel.
- **Services/Interfaces**: `I<Name>` interface always in same folder as implementation.
- **DTO classes**: suffix `Dto`, placed in `src/Shared/Models/`.
- **Enums**: placed in `src/Shared/IPC/` when they cross the service/UI boundary.

### File Structure Rules

- Views (`.axaml`) go in `src/UI/Views/`.
- ViewModels go in `src/UI/ViewModels/`.
- IPC contracts (request/response/command enum) go in `src/Shared/IPC/`.
- Shared model DTOs go in `src/Shared/Models/`.
- Validators go in `src/Shared/Validation/`.

---

## 3. IPC Protocol Rules

### Framing

- **Length-prefix**: 4-byte big-endian `int32` followed by UTF-8 JSON payload.
- **Pipe name**: `WireGuardManagerPipe` (defined in `PipeClient` / service host).
- **Security**: pipe created with `FILE_FLAG_FIRST_PIPE_INSTANCE` to prevent squatting.

### Request/Response Shape

```json
// IpcRequest
{ "Command": "StartTunnel", "TunnelName": "office", "RequestId": "uuid" }

// IpcResponse (success)
{ "Success": true, "Data": { ... } }

// IpcResponse (failure)
{ "Success": false, "Error": "Unauthorized" }
```

### Command Implementation Checklist

When adding a new `IpcCommand` value, **all of the following must be updated**:

1. `src/Shared/IPC/IpcCommand.cs` — add enum member.
2. `src/Service/Auth/AuthorizationService.cs` — add `MinimumRoles` entry.
3. `src/Service/IPC/RequestHandler.cs` — add `case` in `ExecuteAsync`.
4. `src/UI/Services/IPipeClient.cs` — add interface method.
5. `src/UI/Services/PipeClient.cs` — implement the method.
6. ViewModel — wire up command and bind in XAML.
7. Tests — add at least one passing and one unauthorized test case.

### ConfContent Encoding

- WireGuard `.conf` file bytes are transmitted as **Base64** in the `ConfContent` field.
- Always encode: `Convert.ToBase64String(Encoding.UTF8.GetBytes(confText))`.
- Always decode: `Encoding.UTF8.GetString(Convert.FromBase64String(base64))`.

---

## 4. RBAC Rules

### Role Hierarchy

| Value | Role | Description |
|-------|------|-------------|
| 0 | `None` | No access — rejected at auth layer |
| 1 | `Viewer` | Read-only (list tunnels, get status) |
| 2 | `Operator` | Viewer + start/stop tunnels |
| 3 | `AdvancedOperator` | Operator + import/edit/export/restart/delete tunnels |
| 4 | `Admin` | Full access including user management and audit log |

### Invariants

- **Last-admin protection**: `JsonRoleStore` prevents the last Admin from being downgraded or removed. This check is in `SetRoleAsync` and `RemoveUserAsync`.
- **Permission matrix source of truth**: `AuthorizationService.MinimumRoles` dictionary. Never hard-code role checks elsewhere.
- **No implicit elevation**: the UI shows/hides tabs via `IsAdmin` / `IsAdvancedOperator` bindings, but the Service enforces authorization on every request regardless.

### UI Visibility Rules

| Tab / Feature | Minimum Role |
|---------------|-------------|
| Tunnels tab | Viewer (always visible when connected) |
| Import tab | AdvancedOperator |
| Users tab | Admin |
| Audit tab | Admin |
| Connect/Disconnect buttons | Operator |
| Restart/Delete/Export buttons | AdvancedOperator |

---

## 5. Testing Conventions

- **Framework**: xUnit + Moq.
- **Target**: ≥89 tests (current baseline as of Phase 2 completion).
- **Structure**:
  - `tests/Service.Tests/` — service-layer unit tests.
  - `tests/UI.Tests/` — ViewModel unit tests (no real pipe, use mock `IPipeClient`).
- **Naming**: `MethodOrScenario_Condition_ExpectedBehavior`.
- **Every new IPC command must have**: at least one success test + one unauthorized access test.
- **Do not** write integration tests that require the WireGuard service to be installed.

---

## 6. Glossary

| Term | Definition |
|------|-----------|
| **Tunnel** | A WireGuard VPN tunnel defined by a `.conf` file and managed as a Windows Service instance |
| **IPC** | Inter-Process Communication over Named Pipes between the WireGuard Manager Service and the UI |
| **RBAC** | Role-Based Access Control — the permission system governing what each user may do via the pipe |
| **ConfContent** | The raw text of a WireGuard `.conf` file, transmitted Base64-encoded over IPC |
| **Admin** | The highest RBAC role — required for user management and audit log access |
| **AdvancedOperator** | Second-highest role — allowed to import, edit, delete, export, and restart tunnels |
| **Operator** | Mid-tier role — may start and stop tunnels |
| **Viewer** | Lowest functional role — read-only access to tunnel list and status |
| **Last-admin protection** | Guard in `JsonRoleStore` preventing the last Admin account from being demoted or deleted |
| **Length-prefix framing** | IPC message format: 4-byte big-endian int32 length followed by UTF-8 JSON body |
| **PipeClient** | The UI-side service (`src/UI/Services/PipeClient.cs`) that connects to the named pipe, sends requests, and auto-reconnects on disconnect |
| **RequestHandler** | The service-side component that parses `IpcRequest` objects, checks authorization, delegates to domain services, and returns `IpcResponse` |
| **JsonRoleStore** | DPAPI-encrypted JSON file storing the username → role mapping |
| **JsonAuditLogger** | JSON Lines file appended by the service on every authorized action; supports paginated querying |
| **WireGuardConfValidator** | Shared validator (`src/Shared/Validation/WireGuardConfValidator.cs`) that parses and validates `.conf` syntax before import |
| **FluentTheme** | Avalonia's built-in theme used by the UI, forced to `Dark` variant for consistent contrast |
