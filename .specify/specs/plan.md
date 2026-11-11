# Implementation Plan: Zalo Client Port to .NET 10 WinUI 3

**Feature Identifier**: `zalo-winui-port`  
**Milestone**: R2 (speckit-plan)  
**Date**: 2026-11-11  
**Status**: Approved (Production-Ready Architecture)  
**Spec Reference**: [.specify/specs/spec.md](spec.md)  
**Research Reference**: [.specify/specs/research.md](research.md)  
**Data Model Reference**: [.specify/specs/data-model.md](data-model.md)  
**Contracts Reference**: [.specify/specs/contracts/service-interfaces.md](contracts/service-interfaces.md)  

---

## 1. Executive Summary & Objective

This plan defines the comprehensive engineering execution roadmap to port the reverse-engineered Zalo desktop messaging client (`zalo-swift-re` / Olaz) into the **Zenix** .NET 10 WinUI 3 modern desktop database and communication platform.

The implementation completely replaces the legacy Electron/WebKit runtime with native, high-performance C# .NET 10, Windows App SDK (WinUI 3), WinUIEx, and CommunityToolkit.Mvvm. The design strictly enforces all 10 core principles of the **Zenix Constitution (v1.19.0)**:
1. **Clean MVVM & Type Safety**: Pure UI-agnostic ViewModels, compiled `{x:Bind}` bindings, zero business logic or visual tree manipulation in code-behind.
2. **Modern Fluent UI**: WinUI 3 native controls, Mica backdrop, glitch-free Light/Dark theme sync, and asynchronous `ContentDialog`/`InfoBar`.
3. **Secure Credential Protection**: AES-256-GCM / DPAPI credential encryption via `PasswordEncryptor`. Zero secrets in logs.
4. **High Performance & Thread Stewardship**: Sub-16ms (60 FPS) UI frame budget, non-blocking asynchronous I/O, `CancellationToken` support on all async methods, and UI marshalling via `DispatcherQueue.TryEnqueue`.
5. **English Technical Documentation**: 100% XML doc comments in technical American English.
6. **Spec-Driven Development & Superpowers Discipline (GSS-Bridge)**: Spec-Kit $\rightarrow$ Graphify $\rightarrow$ Superpowers TDD Red-Green-Refactor $\rightarrow$ Isolated wave worktrees.
7. **Zero Raw Literals & Dynamic Localization (i18n)**: All UI strings dynamically bound to `TranslationSource.Instance` from UTF-8 `.resx` files.
8. **Agent Desktop Automation & Visual Verification Gate**: Non-intrusive headless testing over Windows UI Automation via `agent-desktop` CLI/MCP with live screenshot verification.
9. **Deterministic Code Knowledge Graph (Graphify)**: Source-only AST knowledge graph navigation and circular dependency detection via `graphify diagnose`.
10. **Clean Engineering & Zero-Patchwork Discipline**: Root-cause resolution over symptom patching, Boy Scout rule, and 100% CRLF line endings.

---

## 2. Target Architecture & Technology Stack

| Component | Technology / Library | Version / Target | Architectural Role |
|---|---|---|---|
| **Runtime & Language** | .NET 10 / C# | `net10.0-windows10.0.26100.0`, LangVersion `preview` | Core execution platform, modern language features (records, pattern matching, spans). |
| **Presentation Framework** | Windows App SDK / WinUI 3 | `Microsoft.WindowsAppSDK` 1.6+ | Native Fluent UI, hardware-accelerated DirectWrite/Composition rendering. |
| **Window Management** | WinUIEx | 2.5+ | Modern Mica/Acrylic window backdrops, persistence of window size/position, system theme synchronization. |
| **MVVM Architecture** | CommunityToolkit.Mvvm | 8.4+ | Source generators (`[ObservableProperty]`, `[RelayCommand]`), decoupled messaging (`WeakReferenceMessenger`). |
| **Dependency Injection** | Microsoft.Extensions.Hosting | 10.0+ | Generic Host lifecycle, constructor injection, singleton/transient service lifetimes. |
| **Local Persistence** | Microsoft.Data.Sqlite | 10.0+ | SQLite DAL 4.0, WAL journal mode, 5000ms busy timeout, connection pooling, schema migrations. |
| **Network & REST** | System.Net.Http | .NET 10 BCL | `IHttpClientFactory` configured with `ZaloWireFingerprintHandler` (Chromium wire headers, stripped Origin/Referer). |
| **Realtime Gateway** | System.Net.WebSockets | .NET 10 BCL | `ClientWebSocket` actor with 4-byte binary framing `[v: 1B][cmd: 2B LE][subCmd: 1B]`, heartbeat ping loop. |
| **Cryptography** | System.Security.Cryptography | .NET 10 BCL | Hardware-accelerated AES-256-CBC, HKDF-SHA256, HMAC-SHA256, Curve25519 Double Ratchet (Signal Protocol). |
| **Automated UI Testing** | agent-desktop CLI | Vercel Labs | Non-intrusive UI Automation inspection, semantic `@ref` actions, live desktop bridge, visual screenshots. |

---

## 3. Constitutional Invariants Compliance Checklist

| Principle | Invariant & Rule | Compliance Enforcement | Verification Gate |
|---|---|---|---|
| **I. Clean MVVM & Type Safety** | ViewModels 100% UI-agnostic; zero references to `UIElement`, `Window`, or `Page`. Zero code-behind UI manipulation (`Children.Add`, swapping container content, or manual `Visibility` changes). All views use compiled `{x:Bind}`. ViewModels resolved via DI. | Roslyn analyzers check zero UI namespaces in `ViewModels/`. Views use pure XAML `<ContentControl Content="{x:Bind ...}" />`. | `dotnet build` with warnings as errors; Clean MVVM review gate. |
| **II. Modern Fluent UI** | Windows App SDK native controls, Mica backdrop, system theme sync, asynchronous `ContentDialog` and `InfoBar`. Banned Win32 `MessageBox.Show`. | `ThemeService` and `ApplicationThemeManager` sync with OS; dialogs use `IDialogService`. | Visual inspection via `agent-desktop screenshot`. |
| **III. Secure Storage** | Session tokens (`zpw_sek`), cookies, and private keys encrypted at rest via `PasswordEncryptor`. No secrets in logs. | `PasswordEncryptor` uses DPAPI/AES-256-GCM. Loggers scrub tokens and passwords. | Unit tests inspecting local SQLite and settings files. |
| **IV. Performance & Threads** | UI maintains 60 FPS (<16ms frame budget). All network/DB/crypto I/O runs asynchronously off UI thread. All async methods accept `CancellationToken`. Dispatcher marshalling via `DispatcherQueue.TryEnqueue`. | Async/await throughout; `Task.Run` for heavy crypto; `SemaphoreSlim` for SQLite write locks; virtualized `ListView`. | Frame timing assertions; cancellation token unit tests. |
| **V. English XML Docs** | 100% XML doc comments in technical American English on all classes, methods, properties, and RelayCommands. | Roslyn `CS1591` enabled in project. | Build output verification. |
| **VI. GSS-Bridge** | Spec-Kit $\rightarrow$ Graphify $\rightarrow$ Superpowers TDD $\rightarrow$ Wave worktrees. | Governed through `.specify/gss-handoff.json`. | Task completion gates. |
| **VII. Zero Raw Literals & i18n** | Zero hardcoded user-facing strings in C# or XAML. Dynamic binding `{Binding [Key], Source={StaticResource Localization}}`. UTF-8 without BOM `.resx`. | `TranslationSource.Instance` binds strings dynamically; zero orphans across `String.resx`, `String.en.resx`, `String.vi.resx`. | `LocalizationVerificationTests.cs` automated test suite. |
| **VIII. Agent Desktop** | Non-intrusive UI Automation testing via `agent-desktop` CLI/MCP with live desktop bridge (`bridge.py`) and screenshots. Zero-AutomationId invariant. | UI actions target qualified `@ref` IDs; screenshots captured before completion. | Mandatory visual screenshot artifact verification. |
| **IX. Code Graph (Graphify)** | Source-only AST knowledge graph navigation. Circular dependency detection via `graphify diagnose`. | `graphify-out/graph.json` maintained and synchronized (`graphify update .`). | `graphify diagnose` returns zero circular cycles. |
| **X. Clean Engineering** | Zero-patchwork invariant, root-cause resolution, Boy Scout rule, 100% Windows CRLF (`\r\n`) line endings. | Git `.gitattributes` and `.editorconfig` enforce CRLF; code review checks root causes. | Line ending linting and git status check. |

---

## 4. Comprehensive Project Layout & Directory Structure

```text
d:\DEV\WinUI\Zenix\
├── .specify\
│   ├── memory\
│   │   └── constitution.md             # Zenix Constitution v1.19.0
│   ├── specs\
│   │   ├── spec.md                     # Feature Specification (Milestone 1)
│   │   ├── plan.md                     # Implementation Plan (Milestone 2 - this document)
│   │   ├── research.md                 # Technical Decision Records (Milestone 2)
│   │   ├── data-model.md               # Domain Models & SQLite DDL (Milestone 2)
│   │   ├── contracts\                  # C# Interface Contracts (Milestone 2)
│   │   │   ├── IZaloClient.cs
│   │   │   ├── IZaloAuthService.cs
│   │   │   ├── IZaloGatewayService.cs
│   │   │   ├── IZaloCryptoService.cs
│   │   │   ├── IZaloMessageService.cs
│   │   │   ├── IZaloMediaService.cs
│   │   │   ├── IZaloContactService.cs
│   │   │   ├── IZaloStorageService.cs
│   │   │   ├── IChatViewModel.cs
│   │   │   ├── IConversationListViewModel.cs
│   │   │   ├── IFilePickerService.cs
│   │   │   ├── IDialogService.cs
│   │   │   └── service-interfaces.md
│   │   ├── quickstart.md               # Quickstart & Verification Guide (Milestone 2)
│   │   └── tasks.md                    # Topologically Ordered Tasks (Milestone 3)
│   └── gss-handoff.json                # GSS-Bridge Execution Manifest (Milestone 4)
├── Zenix\
│   ├── Core\
│   │   └── Zalo\
│   │       ├── Models\                 # Domain Models (UserProfile, ChatMessage, Group)
│   │       ├── Contracts\              # Service Interfaces
│   │       ├── Cryptography\           # AES, HKDF, Signal Protocol, Media Denoising
│   │       ├── Network\                # ZaloWireFingerprintHandler, ClientWebSocket Gateway
│   │       ├── Storage\                # SQLite DAL 4.0, Repositories, Migrator
│   │       └── Services\               # ZaloAuthService, ZaloMessageService, ZaloContactService
│   ├── ViewModels\
│   │   └── Zalo\                       # ZaloChatViewModel, ZaloConversationListViewModel
│   ├── Views\
│   │   └── Zalo\                       # ZaloChatPage.xaml, ZaloConversationListView.xaml
│   └── Strings\                        # String.resx, String.en.resx, String.vi.resx
└── tests\
    └── Zenix.Tests\
        └── Zalo\                       # Unit & Integration Test Suites
            ├── Crypto\                 # AES, HKDF, Signal Protocol tests
            ├── Gateway\                # WebSocket framing and connection tests
            ├── Storage\                # SQLite DAL 4.0 WAL and schema migration tests
            └── ViewModels\             # Chat and ConversationList ViewModel tests
```

---

## 5. Architectural Sequence & Component Flow

### 5.1 Realtime Inbound Message Flow
```
┌──────────────────────┐
│  Zalo Message Server │
└──────────────────────┘
           │
           │ Binary WebSocket Frame [v: 1B][cmd: 2B LE][subCmd: 1B][Payload: NB]
           ▼
┌────────────────────────────────────────────────────────┐
│ IZaloGatewayService (Background Reader Loop)           │
│ - Decodes 4-byte header via ReadOnlySpan<byte>         │
│ - Emits FrameReceived event                            │
└────────────────────────────────────────────────────────┘
           │
           │ Parsed Frame Event
           ▼
┌────────────────────────────────────────────────────────┐
│ IZaloMessageService                                    │
│ - Deserializes JSON or Signal E2EE envelope            │
│ - Decrypts payload if E2EE via IZaloCryptoService      │
│ - Persists message into SQLite via IMessageRepository  │
│ - Dispatches MessageReceived event                     │
└────────────────────────────────────────────────────────┘
           │
           │ MessageReceived Event
           ▼
┌────────────────────────────────────────────────────────┐
│ ZaloChatViewModel (UI-Agnostic)                        │
│ - Marshals update to UI thread via DispatcherQueue     │
│ - Appends ChatMessage to ObservableCollection<ChatMessage> │
└────────────────────────────────────────────────────────┘
           │
           │ Compiled {x:Bind} Data Binding
           ▼
┌────────────────────────────────────────────────────────┐
│ ZaloChatPage.xaml (WinUI 3 View)                       │
│ - ListView virtualized render at 60 FPS                │
└────────────────────────────────────────────────────────┘
```

---

## 6. Milestone Schedule & Dependency Gates

```
Milestone R1: Specification (speckit-specify) [COMPLETED]
    └── Output: .specify/specs/spec.md

Milestone R2: Planning & Design (speckit-plan) [CURRENT]
    ├── Task 2.1: Phase 0 Technical Research (.specify/specs/research.md)
    ├── Task 2.2: Phase 1 Domain Data Model (.specify/specs/data-model.md)
    ├── Task 2.3: Phase 1 Interface Contracts (.specify/specs/contracts/)
    ├── Task 2.4: Phase 1 Implementation Plan (.specify/specs/plan.md)
    └── Task 2.5: Phase 1 Quickstart Guide (.specify/specs/quickstart.md)
    └── GATE: Full Constitutional compliance; Roslyn IDE0065 clean; 100% CRLF.

Milestone R3: Task Decomposition (speckit-tasks) [UPCOMING]
    ├── Topological dependency ordering: Models -> Services -> ViewModels -> Views
    ├── Identification of parallelizable tasks ([P])
    └── Definition of verification test gates for each task.

Milestone R4: GSS-Bridge Handoff & Wave Execution (speckit-gss-bridge) [UPCOMING]
    ├── Author .specify/gss-handoff.json with execution waves
    ├── Graphify blast-radius scoping
    └── Superpowers TDD Red-Green-Refactor discipline enforcement.
```

---

## 7. Performance Budgets & SLA Verification Matrix

| Metric | Target SLA | Measuring Tool | Invalidation Condition |
|---|---|---|---|
| **UI Frame Render Budget** | $\le 16.6\text{ ms}$ (60 FPS) | Windows Performance Recorder / WinUI Diagnostics | Any frame render time $> 33\text{ ms}$ (frame drop). |
| **Application Idle Memory** | $\le 180\text{ MB}$ Working Set | Task Manager / Process Memory Profiler | Memory footprint $> 250\text{ MB}$ at idle. |
| **Startup to Interactive** | $\le 400\text{ ms}$ | BenchmarkDotNet / Startup Timers | Cold startup $> 800\text{ ms}$. |
| **Local SQLite Query Latency** | $\le 1.5\text{ ms}$ for 50 messages | Stopwatch / SQLite Execution Logs | Query time $> 5.0\text{ ms}$. |
| **Binary Framing Allocation** | 0 bytes heap allocation per frame header | DotMemory / GC Allocations | Any allocation during header slice parsing. |
| **Safety Number Generation** | $\le 2.0\text{ ms}$ | BenchmarkDotNet | Calculation $> 5.0\text{ ms}$. |
