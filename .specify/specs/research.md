# Research Document: Zalo Client Port to .NET 10 WinUI 3

**Feature Identifier**: `zalo-winui-port`  
**Phase**: Phase 0 Research & Technical Decision Records (TDR)  
**Date**: 2026-11-11  
**Status**: Approved (Production-Ready Architecture)  
**Spec Reference**: `.specify/specs/spec.md`  

---

## 1. Executive Summary & Research Scope

This document details the architectural decisions, protocol reverse-engineering findings, cryptographic implementations, and runtime performance trade-offs for porting the Zalo desktop messaging client from the macOS Swift/Electron reference implementation (`zalo-swift-re` / Olaz) into **Zenix**—a native .NET 10 WinUI 3 desktop application running Windows App SDK (`net10.0-windows10.0.26100.0`).

All decisions strictly enforce the **Zenix Constitution (v1.19.0)**, ensuring clean MVVM separation, UI-agnostic ViewModels, compiled `{x:Bind}` data binding, dynamic indexer localization (`TranslationSource`), zero code-behind visual tree manipulation, abstracted OS interop services (`IFilePickerService`, `IDialogService`), and asynchronous thread stewardship (`DispatcherQueue`, `CancellationToken`).

---

## 2. Architectural Trade-Off Analysis

### 2.1 Protocol Execution Environment: Pure C# vs Headless WebView2 vs IPC Sidecar

| Dimension | Option A: Pure Native C# (.NET 10) [Selected] | Option B: Headless WebView2 Automation | Option C: Node.js / Electron IPC Sidecar |
|---|---|---|---|
| **Memory Footprint** | **~120–180 MB** working set at idle; <280 MB under heavy load. | **~450–700 MB** (dedicated Chromium renderer + Edge WebView2 processes). | **~600–900 MB** (Node.js runtime + Electron multi-process IPC). |
| **Startup Time** | **<400 ms** to interactive UI; instant local SQLite cache read. | **1.8–3.2 s** (browser engine initialization & script injection). | **2.5–4.5 s** (process fork, pipe negotiation, script loading). |
| **Architecture Purity** | **100% Clean MVVM**; strongly-typed C# domain models and contracts. | Hybrid scraping/DOM bridge; fragile to upstream DOM mutations. | Dual-language codebase; heavy serialization over named pipes/sockets. |
| **UI Thread Impact** | **Zero UI stalls**; async background threads marshalled via `DispatcherQueue`. | WebView2 message pump contention during high message volume. | Inter-process context switching and buffer copying overhead. |
| **Security & Auditing** | All cryptography and protocols in managed code; zero native bloat. | Unauditable minified JavaScript executed in browser context. | External process vulnerability surface; node_modules attack vectors. |
| **Constitutional Compliance** | **100% PASS** on all 10 invariants. | **FAIL** (violates zero code-behind and performance mandates). | **FAIL** (violates single-assembly and performance invariants). |

**Decision**: **Option A (Pure Native C# .NET 10)** is selected. It delivers sub-16ms (60 FPS) rendering, minimal RAM usage, robust type safety, and complete alignment with the Zenix Constitution.

---

## 3. Technical Decision Records (TDR)

### TDR-01: Network Transport & Realtime WebSocket Gateway
- **Context**: Zalo PC communicates via REST HTTP endpoints (`https://wpa.zaloapp.com`, `https://*.chat.zalo.me`) and a persistent duplex WebSocket connection (`wss://wsN-msg.chat.zalo.me`). The backend performs fingerprinting to detect third-party bots and non-matching clients.
- **Wire Fingerprint Emulation**:
  - `User-Agent: ZaloPC-win32-23v694`
  - `sec-ch-ua: "Not?A_Brand";v="8", "Chromium";v="108"`
  - `sec-ch-ua-platform: "Windows"`
  - `sec-ch-ua-mobile: ?0`
  - Client query parameters on every API URL: `zpw_ver=694&zpw_type=23&app_version=26.10.20&fail_sqlite3=true`
- **Origin & Referer Handling**:
  - **REST API Calls**: The custom `DelegatingHandler` (`ZaloWireFingerprintHandler`) **strictly strips both `Origin` and `Referer` headers** from outgoing HTTP requests. Official Chromium `file://` renderers emit neither header; including them immediately flags the request as a bot.
  - **WebSocket Handshake**: Requires `Origin: file://` and `User-Agent: ZaloPC-win32-23v694`, alongside cookies extracted from the active session (`zpw_sek`).
- **Binary WebSocket Framing**:
  - Realtime packets use a 4-byte header:
    $$\text{Header} = [\text{version}: \text{1 byte}] + [\text{command}: \text{2 bytes, Little-Endian}] + [\text{subCommand}: \text{1 byte}]$$
  - Followed by $N$ bytes of JSON or binary message payload.
  - Header parsing uses zero-allocation `ReadOnlySpan<byte>` slicing:
    ```csharp
    byte version = buffer[0];
    ushort command = BinaryPrimitives.ReadUInt16LittleEndian(buffer.Slice(1, 2));
    byte subCommand = buffer[3];
    ReadOnlySpan<byte> payload = buffer.Slice(4);
    ```
- **Connection Management & Reconnection**:
  - Dedicated reader loop on background thread with `ClientWebSocket`.
  - Heartbeat ping interval every 25–30 seconds.
  - Exponential backoff jitter: $1\text{s}, 2\text{s}, 4\text{s}, 8\text{s}, 16\text{s}$, capped at $30\text{s}$.
- **DNS-over-HTTPS (DoH) Fallback**:
  - When CDN host resolution (`*.zadn.vn`, `*.zdn.vn`) fails via local system DNS (SERVFAIL / timeout), the client executes DoH queries to `https://dns.google/resolve?name={host}&type=A` or `https://cloudflare-dns.com/dns-query?name={host}&type=A`.
  - Positive resolution records are cached for DNS TTL ($60\text{s}–600\text{s}$). Negative responses (NXDOMAIN) are cached for $300\text{s}$.

---

### TDR-02: Cryptographic Primitives & Trusted Device Linking

Zalo employs multiple cryptographic tiers for telemetry, API signatures, credential storage, and device linking. All are implemented via native `System.Security.Cryptography` without external dependencies:

```
┌────────────────────────────────────────────────────────────────────────┐
│                        Zenix Zalo Cryptography                         │
├─────────────────────┬────────────────────┬─────────────────────────────┤
│ Target              │ Algorithm          │ Key / IV Parameters         │
├─────────────────────┼────────────────────┼─────────────────────────────┤
│ zcid Parameter      │ AES-256-CBC        │ Key: "3FC4F0D2AB50057BCE..."│
│                     │ PKCS#7 Padding     │ IV: 16 zero bytes           │
│                     │ Hex lowercase      │ Output: 64-char hex string  │
├─────────────────────┼────────────────────┼─────────────────────────────┤
│ API Params          │ AES-256-CBC        │ Key: "DE71B7D4C57C074915..."│
│                     │ PKCS#7 Padding     │ IV: 16 zero bytes           │
│                     │ Base64             │ Output: Base64 string       │
├─────────────────────┼────────────────────┼─────────────────────────────┤
│ SafeStorage Vault   │ AES-128/256-CBC    │ Key: "d9c07b9de9915af401..."│
│                     │ Fixed IV           │ IV: [155, 72, 37, 195, ...] │
├─────────────────────┼────────────────────┼─────────────────────────────┤
│ Device Linking      │ HKDF-SHA256        │ Envelope:                   │
│ Keychain Cipher     │ + AES-256-CBC      │ [0x49: 1B][IV: 16B]         │
│                     │ + HMAC-SHA256      │ [Salt: 16B][Ciphertext: NB] │
│                     │ Constant-time MAC  │ [HMAC: 32B]                 │
├─────────────────────┼────────────────────┼─────────────────────────────┤
│ Media De-noising    │ XOR 147            │ Signature: 077a416c4f07     │
│                     │ Span-based SIMD    │ Strips noise from CDN media │
└─────────────────────┴────────────────────┴─────────────────────────────┘
```

#### Detailed Device Linking Envelope Specification:
1. **Serialization**:
   - `Type Byte`: `0x49` (Magic byte).
   - `IV`: 16 cryptographically random bytes (`RandomNumberGenerator.GetBytes(16)`).
   - `Salt`: 16 cryptographically random bytes.
   - `EncKey`: Derived via `HKDF.DeriveKey(HashAlgorithmName.SHA256, rootKey, 32, salt, "encKey"u8)`.
   - `MacKey`: Derived via `HKDF.DeriveKey(HashAlgorithmName.SHA256, rootKey, 32, salt, "macKey"u8)`.
   - `Ciphertext`: AES-256-CBC encrypted payload with PKCS#7 padding.
   - `HMAC`: `HMACSHA256(MacKey, [Type || IV || Salt || Ciphertext])`.
2. **Deserialization & Validation**:
   - Verify payload length $\ge 65$ bytes (`1 + 16 + 16 + 0 + 32`).
   - Recompute HMAC over `[Type || IV || Salt || Ciphertext]`.
   - Validate HMAC in constant time via `CryptographicOperations.FixedTimeEquals(computedHmac, packetHmac)` **before** invoking AES decryption, preventing padding-oracle attacks.

---

### TDR-03: Signal Protocol End-to-End Encryption (E2EE)

- **Context**: 1-1 private chats in Zalo are protected by a fork of the Signal Protocol (`libsignal-protocol`), leveraging Curve25519 key pairs, Extended Triple Diffie-Hellman (X3DH), and the Double Ratchet Algorithm.
- **Key Hierarchy**:
  - `Identity Key`: Long-term Ed25519/Curve25519 key pair tied to the user's account.
  - `Signed PreKey`: Medium-term key signed by the Identity Key, rotated periodically.
  - `One-Time PreKeys`: Pool of ephemeral keys published to the Zalo key directory and consumed during X3DH handshakes.
- **Message Types**:
  - `Type 3 (PreKeyWhisperMessage)`: Emitted on the very first message sent to an E2EE contact. Includes ephemeral key, one-time prekey ID, and encrypted ratchet payload.
  - `Type 1 (WhisperMessage)`: Emitted on subsequent messages once the symmetric ratchet is established.
- **Safety Number Verification**:
  - Computed from both parties' Curve25519 identity public keys:
    $$\text{Fingerprint} = \text{SHA512}(\text{Min}(K_A, K_B) \,||\, \text{Max}(K_A, K_B))$$
  - Formatted into 60 decimal digits divided into 12 blocks of 5 digits:
    `12345 67890 12345 67890 12345 67890 12345 67890 12345 67890 12345 67890`
  - Displayed in the UI for out-of-band verification.

---

### TDR-04: Local Data Persistence (SQLite DAL 4.0 Architecture)

- **Context**: Official Zalo PC 26.10 stores conversations, messages, and contact directories in dedicated SQLite databases under `%LocalAppData%\Zenix\sqlite\_production\<account_id>\chat_storage.db`.
- **Database Engine**: Direct `Microsoft.Data.Sqlite` integration with connection pooling and serial write queue.
- **Pragma Configuration**:
  Applied immediately upon opening every SQLite connection:
  ```sql
  PRAGMA journal_mode = WAL;
  PRAGMA synchronous = NORMAL;
  PRAGMA busy_timeout = 5000;
  PRAGMA temp_store = MEMORY;
  PRAGMA foreign_keys = ON;
  PRAGMA cache_size = -8000; -- 8 MB cache
  ```
- **Constraint Tolerance**:
  - When inserting messages or contacts, SQLite may encounter unique constraint violations due to race conditions between WebSocket delivery frames and REST synchronization.
  - Catch `SqliteException` where `SqliteErrorCode == 19` (`SQLITE_CONSTRAINT`), verify message contains "UNIQUE constraint failed", and safely return `{ LastInsertedId = 0, RowsAffected = 0 }`.
- **Schema Migration Handshake**:
  - Managed via `PRAGMA user_version`. Current target is `user_version = 400`.
  - On connection initialization, read `user_version`. If less than 400, execute transactional migration scripts sequentially (`V390_To_V400.sql`), updating `user_version` on successful commit.

---

### TDR-05: Clean MVVM UI & Presentation Strategy (WinUI 3 & WinUIEx)

- **Context**: WinUI 3 desktop applications must adhere to Fluent Design principles while strictly obeying the Clean MVVM separation of `CONSTITUTION.md`.
- **Key Architectural Directives**:
  1. **UI-Agnostic ViewModels**: ViewModels must never reference `Microsoft.UI.Xaml.*`, `UIElement`, `Window`, or `Page`. All UI state is expressed through primitive types, immutable domain records, and observable collections.
  2. **Zero Code-Behind UI Manipulation**:
     - Banned: `Children.Add`, `Children.Remove`, manual host swapping (`InlineHost.Content = null`), and imperative `Visibility` manipulation in `*.xaml.cs`.
     - Required: Pure XAML data binding via compiled `{x:Bind}`, `<ContentControl Content="{x:Bind ViewModel.CurrentView}" />` with typed `DataTemplate` selectors.
  3. **Compiled Binding `{x:Bind}`**: All properties and commands must use `{x:Bind}` for compile-time type safety. Reflection `{Binding}` is restricted solely to dynamic indexer localization (`{Binding [Key], Source={StaticResource Localization}}`).
  4. **Dynamic Localization (i18n)**:
     - Zero raw string literals in XAML (`Text="..."`, `Content="..."`, `Title="..."`).
     - Dynamic binding to `TranslationSource.Instance` supporting instant language switching between English and Vietnamese without restarting the application.
  5. **Abstracted OS Services**:
     - System file and folder pickers are abstracted behind `IFilePickerService`.
     - Confirmation dialogs and alerts are abstracted behind `IDialogService`.
  6. **UI Thread Marshalling**:
     - Background updates (WebSocket messages, upload progress) are marshalled to observable collections via `DispatcherQueue.TryEnqueue`.

---

## 4. Risk Analysis & Mitigation Matrix

| Risk ID | Risk Description | Severity | Probability | Mitigation Strategy |
|---|---|---|---|---|
| **RSK-01** | **Zalo Wire Fingerprint Invalidation**: Server updates anti-bot heuristics or checks new headers, causing HTTP 403 or WebSocket disconnection. | High | Low | Isolate all wire headers into `ZaloWireFingerprintHandler`. Maintain configurable header presets; support remote configuration updates. |
| **RSK-02** | **SQLite Database Locking**: Concurrent reads/writes during bulk historical sync cause `SQLITE_BUSY` errors. | High | Medium | Enforce WAL mode, `busy_timeout = 5000`, single-writer semaphore (`SemaphoreSlim(1,1)`), and batched transactions for bulk inserts. |
| **RSK-03** | **Signal Protocol Ratchet Desynchronization**: Dropped or out-of-order E2EE messages corrupt the symmetric ratchet state. | Critical | Low | Maintain ratchet checkpoint snapshots in SQLite before and after ratchet advances. Implement session reset protocol if decryption fails. |
| **RSK-04** | **Large Media Memory Spikes**: High-resolution image/video transfers cause Large Object Heap (LOH) fragmentation. | Medium | Medium | Stream media transfers directly to disk using `ArrayPool<byte>.Shared`; utilize LIFO viewport-based image thumbnail generation. |
| **RSK-05** | **Multi-Device Kickout Event**: Simultaneous login on another desktop client causes abrupt session termination. | Medium | High | Listen for server kickout frames; gracefully disconnect WebSocket, clear ephemeral tokens, and present localized single-session dialog. |
| **RSK-06** | **DNS Poisoning or CDN Blocks**: Local ISP DNS blocks `*.zadn.vn` CDN domains. | Medium | Medium | Automatic DNS-over-HTTPS (DoH) resolver fallback querying Google (`8.8.8.8`) and Cloudflare (`1.1.1.1`) over HTTPS. |
