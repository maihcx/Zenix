# Service Interface Contracts: Zalo Client Port to .NET 10 WinUI 3

**Feature Identifier**: `zalo-winui-port`  
**Phase**: Phase 1 Service Contracts & DI Interfaces  
**Date**: 2026-11-11  
**Status**: Approved (Production-Ready Architecture)  
**Spec Reference**: `.specify/specs/spec.md`  

---

## 1. Overview of Contract Architecture

All contracts reside under `Zenix.Core.Zalo.Contracts` and are authored in standalone `.cs` files complying with:
- **Clean MVVM Separation**: ViewModels and Services are 100% UI-agnostic; no direct references to WinUI 3 visual elements (`UIElement`, `FrameworkElement`, `Window`).
- **Roslyn `IDE0065` Compliance**: All `using` directives are placed at the top of the file, outside and above the namespace.
- **Asynchronous Thread Stewardship**: 100% of asynchronous methods take a `CancellationToken cancellationToken = default`.
- **100% English Technical Documentation**: Every interface, method, parameter, and property is fully documented with XML comments.

---

## 2. Interface Contract Index

| Interface File | Primary Responsibility | Key Operations |
|---|---|---|
| [`IZaloClient.cs`](IZaloClient.cs) | Master Client Coordinator | Aggregates Auth, Gateway, Messages, Contacts, Media, Crypto, and Storage into a unified client session. |
| [`IZaloAuthService.cs`](IZaloAuthService.cs) | Authentication & Session Lifecycle | Client IMEI (`z_uuid`), QR code generation (`reqqr`), QR polling (`chkWaitScan`), QR2 linking (-20), session restore, kickout handling. |
| [`IZaloGatewayService.cs`](IZaloGatewayService.cs) | Realtime Gateway Transport | Duplex WebSocket (`wss://wsN-msg.chat.zalo.me`), 4-byte binary framing `[v: 1B][cmd: 2B LE][subCmd: 1B]`, heartbeats, exponential backoff. |
| [`IZaloCryptoService.cs`](IZaloCryptoService.cs) | Cryptography & Security | AES-256-CBC `zcid` hex encryption, parameter Base64 encryption, trusted-device keychain envelopes, media de-noising, 60-digit Safety Numbers. |
| [`IZaloMessageService.cs`](IZaloMessageService.cs) | Message Lifecycle & Actions | Text/media send, delivery states (10 -> 1 -> 2 -> 3), mention normalization, reactions, message recall, soft-delete undo timers, drafts. |
| [`IZaloMediaService.cs`](IZaloMediaService.cs) | Media Processing & Transfer | Chunked/multipart uploads (`WebKitFormBoundary`), CDN downloads, ISO-BMFF video poster extraction, viewport LIFO download scheduler. |
| [`IZaloContactService.cs`](IZaloContactService.cs) | Contact & Group Administration | Friend list sync, profile retrieval, group membership, member role queries, fast search. |
| [`IZaloStorageService.cs`](IZaloStorageService.cs) | SQLite DAL 4.0 Persistence | Connection pooling, WAL mode, synchronous=NORMAL, 5000ms busy timeout, constraint tolerance, `IConversationRepository`, `IMessageRepository`, `IContactRepository`. |
| [`IChatViewModel.cs`](IChatViewModel.cs) | Active Chat Viewport ViewModel | UI-agnostic message stream, draft input, quote reply, reactions, media attachment commands. Bound via `{x:Bind}`. |
| [`IConversationListViewModel.cs`](IConversationListViewModel.cs) | Sidebar Thread List ViewModel | Observable conversation summaries, unread count badge, filtering/search, pin/mute/archive commands. Bound via `{x:Bind}`. |
| [`IFilePickerService.cs`](IFilePickerService.cs) | Abstracted OS File Pickers | Decoupled single-file, multi-file, save, and folder pickers without WinRT / Win32 UI references. |
| [`IDialogService.cs`](IDialogService.cs) | Abstracted Dialogs & Toasts | Async alerts, confirmations (`ContentDialog` with `XamlRoot` resolution), and non-blocking InfoBar toasts. |

---

## 3. Dependency Injection Registration Map

```csharp
// Program.cs / ApplicationHostService.cs
public static void ConfigureServices(IServiceCollection services)
{
    // 1. Core Services & Gateway
    services.AddSingleton<IZaloCryptoService, ZaloCryptoService>();
    services.AddSingleton<IZaloStorageService, ZaloStorageService>();
    services.AddSingleton<IZaloAuthService, ZaloAuthService>();
    services.AddSingleton<IZaloGatewayService, ZaloGatewayService>();
    services.AddSingleton<IZaloMessageService, ZaloMessageService>();
    services.AddSingleton<IZaloMediaService, ZaloMediaService>();
    services.AddSingleton<IZaloContactService, ZaloContactService>();
    services.AddSingleton<IZaloClient, ZaloClient>();

    // 2. Abstracted Infrastructure Services
    services.AddSingleton<IFilePickerService, WinUIFilePickerService>();
    services.AddSingleton<IDialogService, WinUIDialogService>();

    // 3. ViewModels (Clean MVVM)
    services.AddTransient<IConversationListViewModel, ZaloConversationListViewModel>();
    services.AddTransient<IChatViewModel, ZaloChatViewModel>();
}
```
