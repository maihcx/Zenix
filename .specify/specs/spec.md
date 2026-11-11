# Feature Specification: Zalo Client Port to .NET 10 WinUI 3 (Zenix)

**Feature Identifier**: `zalo-winui-port`  
**Created**: 2026-11-11  
**Status**: Approved (Production-Ready Specification)  
**Input Sources**:
- Reverse-engineered reference repository: `https://github.com/qnguyen3/zalo-swift-re` (Zalo PC 26.10, Electron 22.3.9 / Chromium 108.0.5359.215, client version 694, platform macOS client type 23 / Windows client type 23)
- Survey Findings: `.agents/teamwork/spec_miner_survey_1/findings.md`
- Survey Findings: `.agents/teamwork/spec_miner_survey_2/findings.md`
- Architectural Survey: `.agents/teamwork/explorer_survey_1/findings.md`
- Governance: Zenix Constitution v1.19.0 (`.specify/memory/constitution.md`) & `AGENTS.md`  
**Target Architecture**: .NET 10 WinUI 3 Desktop Application (`Zenix`), Windows App SDK (`net10.0-windows10.0.26100.0`), Clean MVVM, GSS-Bridge  

---

## 1. Executive Summary & Problem Definition

### 1.1 Executive Summary
This specification defines the exhaustive functional and technical architecture for porting the Zalo desktop messaging client from the reverse-engineered macOS Swift/Electron reference implementation (`zalo-swift-re` / Olaz) into a native, high-performance Windows desktop application built on **.NET 10 (`net10.0-windows10.0.26100.0`)**, **Windows App SDK (WinUI 3)**, and **WinUIEx**.

The resulting system—integrated into the **Zenix** modern database and communication suite—replaces heavyweight Electron and WebKit runtimes with native C# asynchronous networking, direct SQLite 4.0 data persistence, hardware-accelerated DirectWrite/Composition rendering, and cryptographic primitives, while guaranteeing 100% adherence to the **Zenix Constitution (v1.19.0)**.

Key transformations achieved by this port:
1. **Zero-Electron Footprint**: Drops memory consumption from 800+ MB (Chromium + Node.js multi-process) to <180 MB working set in idle state, with sub-16ms (60 FPS) UI render times.
2. **Clean MVVM Architecture**: 100% UI-agnostic ViewModels utilizing `CommunityToolkit.Mvvm` source generators, compiled `{x:Bind}` bindings, zero business logic or visual tree manipulation in code-behind, and dependency injection via `Microsoft.Extensions.Hosting`.
3. **Dynamic Internationalization**: 100% dynamic localization via `TranslationSource` and `.resx` bundles (English and Vietnamese), with zero hardcoded user-facing string literals.
4. **Native Protocol & Cryptography**: Direct C# implementation of the Zalo wire protocol, Chromium 108 wire fingerprint emulation, AES-256-CBC, KeychainCipher HKDF-SHA256, and Signal Protocol End-to-End Encryption (E2EE) with Safety Number verification.
5. **High-Performance Persistence (DAL 4.0)**: Direct SQLite integration via `Microsoft.Data.Sqlite` in WAL mode with connection pooling, busy timeout management, and schema migration handshakes.

### 1.2 User Personas
- **Persona 1: Desktop Power User (Alice)**: Requires instant desktop notifications, multi-account or long-session persistence without unexpected kickouts, high-resolution media sharing, and fluid 60 FPS scrolling through conversations containing 100,000+ messages.
- **Persona 2: Enterprise Team Lead (Bao)**: Coordinates projects in group chats (up to 1,000 members), posts announcements/pins, initiates group polls, shares documents, and relies on Signal Protocol End-to-End Encryption (E2EE) with mutual Safety Number verification.
- **Persona 3: Mobile-First User (Cuong)**: Relies on seamless Phone-to-PC sync, QR-code authentication without password entry, read-state synchronization across devices, and multi-layer device trust verification.

---

## 2. User Scenarios & Prioritized User Journeys

### User Story 1 - Desktop Authentication & Secure Session Lifecycle (Priority: P1)
As a Zenix desktop user, I want to authenticate into my Zalo account using QR Code scanning or saved session tokens so that I can securely access my conversations without entering raw credentials.

**Why this priority**: Authentication is the foundational gatekeeper. Without establishing valid session cookies (`zpw_sek`) and cryptographic keys, no downstream network or messaging capabilities can function.

**Independent Test**: Can be independently verified by generating a QR code token, polling the auth endpoint, simulating/executing mobile phone approval, establishing secure cookies in `PasswordEncryptor`, and validating that startup auto-login succeeds.

**Acceptance Scenarios**:
1. **Given** an unauthenticated state, **When** the app loads, **Then** it generates a valid device IMEI (`z_uuid`), queries server info from `wpa.zaloapp.com`, and requests a primary QR token (`reqqr`).
2. **Given** an active QR code, **When** the user scans on mobile, **Then** the UI updates to show the user's avatar and name in `waitingConfirm` state.
3. **Given** a server response with `error_code: -20`, **When** polling, **Then** the system automatically initiates the second QR scan (`qr2`) for device linking without throwing an error dialog.
4. **Given** user approval on phone, **When** long-polling receives success, **Then** session cookie `zpw_sek` is stored encrypted via `PasswordEncryptor`, and the app navigates to the main conversation shell.
5. **Given** an active session, **When** another desktop client logs into the same account, **Then** the server kickout event terminates the session cleanly and displays a single-session warning modal.

---

### User Story 2 - Realtime WebSocket Messaging & Protocol Framing (Priority: P1)
As a user, I want to send and receive text messages, mentions, and quotes in real-time with delivery and seen confirmations.

**Why this priority**: Real-time communication is the primary core capability of a chat application.

**Independent Test**: Can be tested by establishing a binary WebSocket connection to `wss://wsN-msg.chat.zalo.me`, sending framed binary text packets with `cliMsgId`, and verifying delivery state transitions (10 -> 1 -> 2 -> 3 -> 4).

**Acceptance Scenarios**:
1. **Given** an authenticated session, **When** connecting to the gateway, **Then** the WebSocket handshake supplies Chromium 108 wire headers (`User-Agent: ZaloPC-win32-23v694`, `Origin: file://`, `zpw_sek` cookie) and initiates heartbeat ping intervals.
2. **Given** an open chat, **When** the user sends a text message, **Then** a local message is created with `cliMsgId` in status `10 (Sending)`, transitioning to `1 (Sent)` when the server acknowledges with `msgId`.
3. **Given** a composer draft with `@name`, **When** submitted, **Then** text offsets (`pos`, `len`, `uid`) are trimmed and normalized into the mention metadata array.
4. **Given** an unsent message due to network loss, **When** send fails, **Then** the text draft is preserved in `FailedDraftStore` per conversation to prevent data loss.
5. **Given** a received message, **When** incoming timestamp is older than current top, **Then** stable tie-breaking sorts the list by `(timestamp, offset)` without visual flicker.

---

### User Story 3 - End-to-End Encryption (E2EE) with Signal Protocol (Priority: P2)
As privacy-conscious users, we want our 1-1 conversations to be encrypted end-to-end using Double Ratchet and X3DH so that no intermediary server can read message contents.

**Why this priority**: Enterprise and personal privacy compliance. Zalo mandates E2EE for enrolled contacts.

**Independent Test**: Can be tested using a standalone test harness with two Curve25519 keypairs, executing X3DH pre-key bundles, encrypting a `PreKeyWhisperMessage` (Type 3) and `WhisperMessage` (Type 1), and verifying Safety Number generation.

**Acceptance Scenarios**:
1. **Given** an E2EE conversation, **When** sending the first message, **Then** the client builds an X3DH bundle, consumes a one-time prekey, and emits a Type 3 `PreKeyWhisperMessage`.
2. **Given** an established session, **When** subsequent messages are exchanged, **Then** the ratchet advances symmetrically, producing Type 1 `WhisperMessage` envelopes.
3. **Given** peer identity keys, **When** inspecting conversation security, **Then** the system computes a 60-digit Safety Number formatted into 12 groups of 5 decimal digits.

---

### User Story 4 - High-Performance Local Data Storage (SQLite DAL 4.0) (Priority: P2)
As a user with extensive chat history, I want all conversations, messages, contacts, and media references cached locally in an optimized SQLite database so that the app loads instantly offline.

**Why this priority**: Offline availability, search speed, and instant message loading on startup.

**Independent Test**: Can be tested by executing SQLite DAL queries on a partitioned database under `<LocalAppData>/Zenix/sqlite/_production/`, verifying WAL mode, `synchronous=NORMAL`, 5000ms busy timeout, and schema migrations.

**Acceptance Scenarios**:
1. **Given** database initialization, **When** opening a SQLite file, **Then** `PRAGMA journal_mode=WAL; PRAGMA synchronous=NORMAL; PRAGMA busy_timeout=5000;` are applied.
2. **Given** an `INSERT` statement encountering a UNIQUE constraint conflict, **When** executed, **Then** the system resolves safely returning `{lastID: 0, changes: 0}` without throwing a fatal application crash.
3. **Given** a schema version mismatch between file `PRAGMA user_version` and code schema, **When** opening, **Then** the migration handshake transitions the database cleanly before resuming query pools.

---

### User Story 5 - Rich Media, Voice Notes, Stickers & File Transfer (Priority: P3)
As a user, I want to send and receive photos, albums, animated stickers, voice messages with waveforms, and document files with progress tracking.

**Why this priority**: Rich media makes up over 40% of chat interactions in modern messaging platforms.

**Independent Test**: Can be tested by streaming a media file upload with multipart form boundaries, extracting video first-frame poster via Windows Media Foundation, and rendering animated WebP stickers.

**Acceptance Scenarios**:
1. **Given** a photo upload, **When** transmitted, **Then** the multipart boundary strictly follows `----WebKitFormBoundary[16 alphanumeric chars]` to pass server filters.
2. **Given** a video message URL, **When** the header is inspected, **Then** the ISO-BMFF container (`ftyp` at offset 4) is detected and a `.poster.jpg` thumbnail is generated.
3. **Given** a downloaded image containing anti-scraping noise, **When** received, **Then** the byte stream is de-noised using XOR key `147` and validated against signature `077a416c4f07`.
4. **Given** an animated sticker, **When** displayed, **Then** sprite frames or animated WebP render smoothly without stalling the WinUI 3 UI thread.

---

### User Story 6 - Message Management: Recall, Delete for Me & Reactions (Priority: P3)
As a user, I want to recall sent messages, delete messages locally with an undo grace period, and express reactions.

**Why this priority**: Essential chat control and error correction for users.

**Independent Test**: Can be tested by invoking recall API within allowable time limit, triggering local delete and verifying the 5-30s undo window, and stacking emoji reactions.

**Acceptance Scenarios**:
1. **Given** a sent message within recall time, **When** the sender recalls it, **Then** the message kind transforms into `.recalled` ("Tin nhắn đã thu hồi") across both clients.
2. **Given** a message deletion ("Xóa chỉ ở phía tôi"), **When** confirmed, **Then** an undo countdown bar (5-30s) appears; clicking "Khôi phục" restores the message before deadline expires.
3. **Given** a message, **When** a user reacts with an emoji multiple times, **Then** reactions stack per user without toggling off.

---

## 3. Complete Domain Models & Data Structures

### 3.1 Enumerations & Bitmasks

```csharp
namespace Zenix.Core.Models.Enums;

/// <summary>
/// Specifies the lifecycle status of a chat message in transmission and persistence.
/// </summary>
public enum MessageDeliveryStatus
{
    /// <summary>Message failed to transmit.</summary>
    Failed = -2,
    /// <summary>Message is locally queued or transmitting.</summary>
    Sending = 10,
    /// <summary>Message acknowledged by server.</summary>
    Sent = 1,
    /// <summary>Message delivered to recipient device.</summary>
    Delivered = 2,
    /// <summary>Message viewed by recipient.</summary>
    Seen = 3,
    /// <summary>Message viewed by all members in group.</summary>
    SeenByAll = 4
}

/// <summary>
/// Bitmask flags representing permissible operations per conversation.
/// </summary>
[Flags]
public enum ConversationActionFlags
{
    None = 0,
    Pinned = 1 << 0,          // 1
    Archived = 1 << 1,        // 2
    Hidden = 1 << 2,          // 4
    CannotPin = 1 << 3,       // 8
    CannotArchive = 1 << 4,   // 16
    CannotMute = 1 << 5,      // 32
    CannotDelete = 1 << 6,    // 64
    NoPinMenu = 1 << 7,       // 128
    CannotHide = 1 << 8,      // 256
    UnreadMark = 1 << 9       // 512
}

/// <summary>
/// Discriminator types for rich message variants.
/// </summary>
public enum MessageKindType
{
    Text = 1,
    Photo = 2,
    Sticker = 3,
    Gif = 4,
    Video = 5,
    Voice = 6,
    File = 7,
    Link = 8,
    ContactCard = 9,
    Location = 10,
    CallRecord = 11,
    ECard = 12,
    OAMessage = 13,
    Poll = 14,
    StickerSet = 15,
    ZInstant = 16,
    Notice = 17,
    Placeholder = 18,
    System = 19,
    Recalled = 20,
    Unsupported = 99
}
```

### 3.2 Domain Entities

```csharp
namespace Zenix.Core.Models;

/// <summary>
/// Represents a user or peer account in the Zalo network.
/// </summary>
public sealed record UserProfile(
    string UserId,
    string DisplayName,
    string? Phone,
    string? AvatarUrl,
    string? ZaloName,
    bool IsOnline,
    string? StatusText,
    DateTimeOffset LastActiveTime
)
{
    /// <summary>Computes the user display initials for fallback avatar rendering.</summary>
    public string Initials => string.IsNullOrWhiteSpace(DisplayName)
        ? "Z"
        : string.Concat(DisplayName.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Select(s => s[0])).ToUpperInvariant();
}

/// <summary>
/// Represents a conversation thread summary.
/// </summary>
public sealed record ConversationSummary(
    string ConversationId,
    string Name,
    string? AvatarUrl,
    bool IsGroup,
    int UnreadCount,
    ConversationActionFlags Flags,
    string? LastMessageSnippet,
    DateTimeOffset LastActivityTimestamp,
    bool IsMuted,
    bool IsProtected,
    string? DraftText
);

/// <summary>
/// Represents an individual chat message entity.
/// </summary>
public sealed record ChatMessage(
    string Id,
    string ConversationId,
    string SenderId,
    string SenderName,
    string? SenderAvatar,
    bool IsMine,
    string Text,
    DateTimeOffset Date,
    MessageDeliveryStatus Status,
    MessagePayload Payload,
    QuotedMessage? Quote,
    IReadOnlyList<MessageReaction> Reactions,
    IReadOnlyList<MentionSpan> Mentions,
    bool IsE2EE
);

/// <summary>
/// Represents a quoted reference carried inside a reply message.
/// </summary>
public sealed record QuotedMessage(
    string MessageId,
    string SenderName,
    string Snippet
);

/// <summary>
/// Represents an aggregated reaction emoji on a message.
/// </summary>
public sealed record MessageReaction(
    string Type,
    string Emoji,
    string IconUrl,
    int Count,
    bool IncludesMe,
    IReadOnlyList<string> ReactorNames
);

/// <summary>
/// Represents an @mention span in message text.
/// </summary>
public sealed record MentionSpan(
    int Position,
    int Length,
    string TargetUid,
    bool IsAll
);

/// <summary>
/// Abstract base record for polymorphic message payload variants.
/// </summary>
public abstract record MessagePayload(MessageKindType Kind);

public sealed record TextPayload(string Content) : MessagePayload(MessageKindType.Text);
public sealed record PhotoPayload(string Url, int Width, int Height, string? HdUrl) : MessagePayload(MessageKindType.Photo);
public sealed record StickerPayload(string CategoryId, string StickerId, string StaticUrl, string? AnimationUrl) : MessagePayload(MessageKindType.Sticker);
public sealed record VideoPayload(string Url, string ThumbnailUrl, TimeSpan Duration, long FileSize) : MessagePayload(MessageKindType.Video);
public sealed record VoicePayload(string Url, TimeSpan Duration, IReadOnlyList<byte>? Waveform) : MessagePayload(MessageKindType.Voice);
public sealed record FilePayload(string Name, long Size, string Url, string Extension) : MessagePayload(MessageKindType.File);
public sealed record LinkPayload(string Title, string Url, string? ThumbnailUrl, string? Description, string? Source) : MessagePayload(MessageKindType.Link);
public sealed record ContactCardPayload(string Uid, string Name, string? Phone, string? AvatarUrl, string? QrCodeUrl) : MessagePayload(MessageKindType.ContactCard);
public sealed record LocationPayload(string Title, string Address, double Latitude, double Longitude) : MessagePayload(MessageKindType.Location);
public sealed record PollPayload(string PollId, string Question, IReadOnlyList<PollOption> Options, bool IsClosed) : MessagePayload(MessageKindType.Poll);
public sealed record RecalledPayload : MessagePayload(MessageKindType.Recalled);
public sealed record UnsupportedPayload(int RawType) : MessagePayload(MessageKindType.Unsupported);

public sealed record PollOption(int OptionId, string Text, int VoteCount, bool HasVoted);
```

---

## 4. Deep-Dive Functional Specifications

### 4.1 Module A: Authentication, Device Linking & Session Lifecycle

#### 4.1.1 Boot Parameters & Client Identity
Every request and page URL carries the standardized Zalo PC client parameters:
```
?type=23&client_version=694&app_version=26.10.20&about_version=&new_install=0&fail_sqlite3=true&mode_lang=
```
- `type`: `23` (Desktop client identifier).
- `client_version`: `694` (Build identifier matching Zalo PC 26.10).
- `z_uuid`: Persisted client IMEI:
  $$\text{IMEI} = \text{UUIDv4}() + \text{"-"} + \text{MD5}(\text{UserAgent})$$
  In Zenix, `UserAgent` is standardized to `"ZaloPC-windows-23v694"`.

#### 4.1.2 Protocol Flow: QR Code Login & Multi-Layer Linking (`qr2`)
```
Client (Zenix WinUI 3)       Zalo Auth Server (wpa.zaloapp.com)        Zalo Mobile App
       │                                     │                                │
       │─── 1. getServerInfo() ────────────>│                                │
       │<── 2. Server Config & Endpoints ────│                                │
       │                                     │                                │
       │─── 3. getQRCode(imei, trust_dev) ──>│                                │
       │<── 4. tokenId, base64Qr/svg ────────│                                │
       │    (renders QR1 on desktop UI)      │                                │
       │                                     │                                │
       │─── 5. Long-Poll /polling/qr/* ─────>│                                │
       │       (timeout: 21s)                │                                │
       │                                     │<── 6. Mobile Scans QR1 ────────│
       │<── 7. HTTP 200 {error_code: -20} ───│    (Multi-layer device link)   │
       │    (Triggers genSecondQR)           │                                │
       │    (renders QR2 on desktop UI)      │                                │
       │                                     │                                │
       │─── 8. Long-Poll /polling/qr/* ─────>│                                │
       │                                     │<── 9. Mobile Scans QR2 ────────│
       │<── 10. HTTP 200 {display_name, ava}─│                                │
       │    (Enters waitingConfirm state)    │                                │
       │                                     │                                │
       │─── 11. Long-Poll /polling/qr/* ────>│                                │
       │                                     │<── 12. Mobile Confirms Login ──│
       │<── 13. HTTP 200 {zpw_sek, zpw_kt} ──│                                │
       │                                     │                                │
       │─── 14. Set Cookies & Persist ───────│                                │
       │    (zpw_sek on 3 domains)           │                                │
       │    (Navigate to ShellWindow)        │                                │
       ▼                                     ▼                                ▼
```

#### 4.1.3 Cookie Store Configuration & Invariants
Upon successful login confirmation, `zpw_sek` and `zpw_kt` must be securely stored:
- **Domains**: `.zaloapp.com`, `.zalo.me`, `.chat.zalo.me` (Notice the mandatory leading dot for wildcard subdomain matching).
- **Properties**:
  - `Path`: `/`
  - `Secure`: `true`
  - `HttpOnly`: `true`
  - `SameSite`: `none`
  - `Expiration`: Current timestamp + 7 days ($604,800$ seconds).
- **Encryption at Rest**: Saved using `PasswordEncryptor` (AES-256-GCM / Windows DPAPI). Plaintext tokens must never appear in unencrypted JSON stores or logs.

#### 4.1.4 Functional Requirements (Module A)
- **FR-AUTH-001**: System MUST compute and persist the unique device identifier `z_uuid` using UUIDv4 and MD5 of the user-agent string: `UUIDv4 + "-" + MD5(UserAgent)`.
- **FR-AUTH-002**: System MUST request server configuration from `https://wpa.zaloapp.com/api/server/info` carrying query parameters `type=23&client_version=694`.
- **FR-AUTH-003**: System MUST execute the desktop QR login handshake (`reqqr`) and start long-polling via `chkWaitScan`.
- **FR-AUTH-004**: System MUST handle server error code `-20` by triggering multi-layer QR authentication (`qr2`) without reporting an error to the user.
- **FR-AUTH-005**: System MUST detect QR expiration after ~60 seconds of long-polling and transition the UI to an expired state with a refresh action.
- **FR-AUTH-006**: System MUST persist authenticated session cookies (`zpw_sek`, `zpw_kt`) encrypted at rest using `PasswordEncryptor` (DPAPI / AES-256-GCM).
- **FR-AUTH-007**: System MUST attach the `zpw_sek` cookie with a leading dot domain (`.zalo.me`) to ensure valid WebSocket authorization.
- **FR-AUTH-008**: System MUST listen for session kickout push events on `/api/session/kickout-info` and gracefully terminate the session.

---

### 4.2 Module B: Realtime Gateway & Network Transport

#### 4.2.1 Wire Fingerprint Specification
Zalo PC requests are strictly fingerprinted to emulate Chromium 108. All REST API requests must match this exact header profile:
```http
:method: POST
:scheme: https
:authority: chat.zalo.me
accept: application/json, text/plain, */*
accept-encoding: gzip, deflate, br
accept-language: en-US
content-type: application/x-www-form-urlencoded
cookie: zpw_sek=<token>; zpw_kt=<refresh_token>
sec-ch-ua: "Not?A_Brand";v="8", "Chromium";v="108"
sec-ch-ua-mobile: ?0
sec-ch-ua-platform: "Windows"
sec-fetch-dest: empty
sec-fetch-mode: cors
sec-fetch-site: cross-site
user-agent: ZaloPC-windows-23v694
```
*Constitutional Invariant*: REST API calls must NOT include `Origin` or `Referer` headers. Including them flags the client as a third-party scraper.

#### 4.2.2 Realtime WebSocket Protocol & Binary Framing
- **Endpoint**: `wss://wsN-msg.chat.zalo.me` (where `N` $\in \{1, 2, 5, \dots\}$ is dynamically assigned by server info).
- **Handshake Headers**:
  - `User-Agent: ZaloPC-windows-23v694`
  - `Origin: file://` (WebSocket handshake requires `file://`)
  - `Cookie`: Injected session cookies (`zpw_sek`, etc.)
- **Binary Frame Header Format**:
  Every binary frame received or sent over the WebSocket channel begins with a 4-byte header:
  ```
  Byte 0       Byte 1       Byte 2       Byte 3       Byte 4..N
  +------------+------------+------------+------------+-----------------------+
  |  Version   |  Command ID (LE 16-bit)  | SubCommand | Payload (Protobuf/JSON|
  |  (UInt8)   |   Low Byte  | High Byte  |  (UInt8)   | or Encrypted Cipher)  |
  +------------+------------+------------+------------+-----------------------+
  ```
  - `Version`: 1 byte (typically `1`).
  - `Command ID`: 2 bytes, 16-bit unsigned integer, little-endian format.
  - `SubCommand ID`: 1 byte.
  - `Payload`: Message payload, formatted as JSON string or Protocol Buffer payload depending on command ID.

#### 4.2.3 Voice & Video Call IPC Socket Protocol (`zcall-v2`)
- **Transport**: Dual Named Pipes on Windows (`\\.\pipe\ZaloCallRecv`, `\\.\pipe\ZaloCallSend`).
- **Framing**:
  - Cipher: AES-128-CBC, zero IV, PKCS#7 padding.
  - Key: Base64 `"yjAF9oqMWl6XfXYJn9mA7w=="` (16 bytes decoded).
  - Delimiter: Frames end with character `$`.
  - Wire format: `hex(AES-128-CBC(jsonPayload)) + "$"`
- **Large Payload Chunking (> 4000 characters)**:
  Payloads exceeding 4000 characters are partitioned into sequenced chunks:
  $$\text{Frame}_i = \text{Chunk}_i + \text{"\#"} + \text{Seq} + \text{"\#"} + \text{TotalCount} + \text{"\#"} + i + \text{"\#\$"}$$
  The sender transmits one chunk at a time, awaiting any byte ACK from receiver before dispatching the next.

#### 4.2.4 DNS-over-HTTPS (DoH) Fallback
- In cases where system DNS returns `SERVFAIL` or fails to resolve CDN hostnames (`*.zadn.vn`, `*.zdn.vn`), the network transport automatically queries Google DoH (`https://dns.google/resolve?name=<domain>&type=A`) over HTTPS.
- Positive A records are cached for the response TTL (60–600s); negative results (`NXDOMAIN`) are cached for 300s.

#### 4.2.5 Functional Requirements (Module B)
- **FR-NET-001**: System MUST establish a persistent WebSocket connection to `wss://wsN-msg.chat.zalo.me` with automatic exponential backoff reconnection.
- **FR-NET-002**: System MUST format outgoing WebSocket frames using the 4-byte binary header: `[version: 1 byte][cmd: 2 bytes Little-Endian][subCmd: 1 byte]` followed by payload.
- **FR-NET-003**: System MUST emulate the Chromium 108 wire fingerprint by omitting `Origin` and `Referer` headers on standard REST API requests and setting `User-Agent: ZaloPC-windows-23v694`.
- **FR-NET-004**: System MUST format multipart upload boundaries as `----WebKitFormBoundary` followed by 16 alphanumeric characters.
- **FR-NET-005**: System MUST provide DNS-over-HTTPS (DoH) resolution fallback targeting `https://dns.google/resolve` when system DNS fails for CDN domains (`*.zadn.vn`, `*.zdn.vn`).

---

### 4.3 Module C: Cryptography, Key Management & E2EE

#### 4.3.1 Parameter and Telemetry (`zcid`) Encryption
1. **Telemetry & Device Identifier (`zcid`)**:
   - **Algorithm**: AES-256-CBC with PKCS#7 padding.
   - **Key**: UTF-8 bytes of `"3FC4F0D2AB50057BCE0D90D9187A22B1"` (32 bytes).
   - **IV**: 16 bytes of zeros (`0x00`).
   - **Plaintext**: `"<clientType>,<imei>,<timestampMs>"` (e.g. `"23,abc-imei,1792963077777"`).
   - **Output**: Lowercase hex string.
2. **API Request Body Parameters**:
   - **Algorithm**: AES-256-CBC with PKCS#7 padding.
   - **Key**: UTF-8 bytes of `"DE71B7D4C57C0749158CB90D5E431AE3"` (32 bytes).
   - **IV**: 16 bytes of zeros (`0x00`).
   - **Plaintext**: Stringified JSON (e.g. `{"language":"vi"}`).
   - **Output**: Base64-encoded ciphertext.

#### 4.3.2 SafeStorage Vault Encryption (`zsafestorage-execute`)
Used to protect client persistent state in `zsafe-storage.json`:
- **Cipher**: AES-256-CBC.
- **Key**: UTF-8 bytes of `"d9c07b9de9915af401607d3f360211ac"` (32 bytes).
- **IV**: `[155, 72, 37, 195, 9, 217, 80, 42, 145, 102, 171, 75, 214, 176, 134, 18]` (16 bytes).
- **Version Flag**: `zsskv = "2"`.
- **Key Path**: Stored under dot-notation `zsskd.<keyName>`. Key names containing `.` are forbidden.

#### 4.3.3 Trusted-Device Protocol Cipher (`KeychainCipher`)
Used to seal secrets for device linking and key sharing:
- **Root Secret**: 32-byte cryptographically secure random bytes generated per identifier (`zprotocol.<identifier>`).
- **Key Derivation (HKDF-SHA256)**:
  $$\text{OKM} = \text{HKDF-SHA256}(\text{IKM}=\text{RootSecret}, \text{salt}=\text{RandomSalt}_{16}, \text{info}=\text{"ZprotocolSecureKDF"}, L=64)$$
  - $\text{EncKey} = \text{OKM}[0..31]$ (32 bytes for AES-256-CBC).
  - $\text{MacKey} = \text{OKM}[32..63]$ (32 bytes for HMAC-SHA256).
- **Wire Representation**:
  ```
  Byte 0     Bytes 1..16   Bytes 17..32   Bytes 33..N       Bytes (N+1)..(N+32)
  +----------+-------------+--------------+-----------------+-------------------+
  |   Type   |     IV      |     Salt     |   Ciphertext    |     HMAC Tag      |
  |  (0x49)  |  (16 bytes) |  (16 bytes)  | (Variable len)  |    (32 bytes)     |
  +----------+-------------+--------------+-----------------+-------------------+
  ```
  - Total overhead: $1 + 16 + 16 + 32 = 65$ bytes. Data length < 65 bytes must be rejected as invalid.

#### 4.3.4 Signal Protocol E2EE (End-to-End Encryption)
- **Cryptographic Primitives**:
  - Asymmetric: Curve25519 (ECDH key agreement via X3DH).
  - Signatures: Ed25519.
  - Symmetric Ratchet: AES-256-CBC / CTR with HMAC-SHA256.
  - KDF: SHA-512.
- **Envelope Formats**:
  - `Type 3` (`PreKeyWhisperMessage`): Carries sender ephemeral public key, prekey ID, and signed prekey ID for initial session establishment.
  - `Type 1` (`WhisperMessage`): Normal message ratchet packet.
- **Safety Number Verification**:
  $$\text{SafetyNumber} = \text{FingerprintGenerator}(\text{localIdentityKey}, \text{remoteIdentityKey}, \text{iterations}=5200)$$
  Outputs 60 decimal digits formatted into 12 space-separated blocks of 5 digits:
  ```
  XXXXX XXXXX XXXXX XXXXX XXXXX XXXXX XXXXX XXXXX XXXXX XXXXX XXXXX XXXXX
  ```

#### 4.3.5 Media File De-noising
Downloaded media files protected by Zalo's anti-scraping noise layer must be de-noised:
- Byte validation: Check for 6-byte signature `077a416c4f07`.
- De-noising algorithm: XOR bytes with key `147` (`0x93`) to yield original JPEG/PNG/MP4 stream.

#### 4.3.6 Functional Requirements (Module C)
- **FR-CRYPTO-001**: System MUST encrypt device telemetry `zcid` using AES-256-CBC with key `"3FC4F0D2AB50057BCE0D90D9187A22B1"`, zero IV, and lowercase hex encoding.
- **FR-CRYPTO-002**: System MUST encrypt JSON API request parameters using AES-256-CBC with key `"DE71B7D4C57C0749158CB90D5E431AE3"`, zero IV, and Base64 output.
- **FR-CRYPTO-003**: System MUST execute trusted device linking cryptography using HKDF-SHA256 derived keys with payload framing: `[0x49][iv: 16B][salt: 16B][ciphertext][hmac: 32B]`.
- **FR-CRYPTO-004**: System MUST support Signal Protocol E2EE for 1-1 conversations with Curve25519 identity keys, X3DH prekey exchange, and Double Ratchet session handling.
- **FR-CRYPTO-005**: System MUST compute 60-digit Safety Numbers from mutual Curve25519 identity keys formatted into 12 groups of 5 decimal digits.
- **FR-CRYPTO-006**: System MUST de-noise protected downloaded media by applying XOR key `147` against bytes carrying signature `077a416c4f07`.

---

### 4.4 Module D: Messaging Engine, Delivery State & Actions

#### 4.4.1 Send & Receive Lifecycle
1. **Local Pending Submission**:
   - The user submits text/media via `ChatComposer`.
   - The engine generates a unique `cliMsgId` (UUIDv4) and immediately creates a local `ChatMessage` with status `10 (Sending)`.
   - The message bubble appears instantly in the conversation list and active viewport without UI freezing.
2. **Gateway Dispatch & Acknowledgment**:
   - The payload is serialized into a WebSocket frame or encrypted E2EE envelope and dispatched.
   - Upon receiving server acknowledgment with `msgId`, the engine reconciles `cliMsgId` $\to$ `msgId` in-place, updating status to `1 (Sent)` without bubble recreation or layout shifts.
3. **Delivery & Seen Transitions**:
   - Realtime delivery receipt updates status to `2 (Delivered)`.
   - Read receipt updates status to `3 (Seen)` or `4 (SeenByAll)`.
4. **Draft Protection (`FailedDraftStore`)**:
   - If transmission fails due to network drop or service error, the status transitions to `-2 (Failed)`.
   - The text, mention metadata, and quoted reference are held in an in-memory and persisted `FailedDraftStore` keyed by `conversationId`.
   - When the user returns to the conversation, the draft is restored automatically into the composer.

#### 4.4.2 Mentions & Trimming Mechanics
- Mention spans carry UTF-16 text offset `pos`, character length `len`, target `uid`, and `isAll` boolean (for `@all`, UID is `"-1"`).
- **Trimming Invariant**: When text contains leading whitespace, the count of leading whitespace characters is subtracted from all mention `pos` offsets. If the resulting offset is out of bounds or the character at `pos` is not `'@'`, the mention is pruned to avoid wire payload rejection.

#### 4.4.3 Out-of-Order Message Insertion
- If an incoming message has a timestamp older than the latest message in the active collection, a stable insertion sort is performed using `(timestamp, offset)` tie-breaking, ensuring chronological fidelity without visual flickering.

#### 4.4.4 Message Actions: Recall & Delete for Me
- **Recall ("Thu hồi")**:
  - Permitted within the allowable time window for the sender.
  - The message kind transitions to `.recalled` ("Tin nhắn đã thu hồi") across both sender and receiver clients.
- **Delete for Me ("Xóa chỉ ở phía tôi")**:
  - The local message is removed from the visible collection.
  - An undo countdown bar (`DeleteUndo`) appears for 5–30 seconds.
  - If the user clicks "Khôi phục" (`undoDelete`), the message is restored.
  - If the timer expires, the deletion is committed permanently to SQLite.

#### 4.4.5 Protected Conversation Sharing
- If a conversation has `isProtected = true` ("Chặn chia sẻ"), the engine strictly disables copy and forward actions, presenting an InfoBar notification if attempted.

#### 4.4.6 Functional Requirements (Module D)
- **FR-MSG-001**: System MUST support 20+ strongly-typed message kinds (Text, Photo, Sticker, GIF, Video, Voice, File, LinkPreview, ContactCard, Location, CallRecord, ECard, OAMessage, Poll, StickerSet, ZInstant, Notice, Placeholder, System, Recalled, Unsupported).
- **FR-MSG-002**: System MUST track message progression through explicit delivery statuses: Sending (10), Sent (1), Delivered (2), Seen (3/4), and Failed (-2).
- **FR-MSG-003**: System MUST assign a client-generated UUID (`cliMsgId`) for local pending messages and reconcile with server `msgId` upon network confirmation without UI duplicate flickering.
- **FR-MSG-004**: System MUST store unsent drafts in `FailedDraftStore` when transmission fails, restoring drafts when the conversation is reopened.
- **FR-MSG-005**: System MUST perform stable sorting by `(timestamp, offset)` whenever incoming messages arrive out of chronological order.
- **FR-MSG-006**: System MUST handle message recalls within the permissible window, converting the message kind to `.recalled`.
- **FR-MSG-007**: System MUST provide "Xóa chỉ ở phía tôi" with an undo countdown timer (5-30 seconds).
- **FR-MSG-008**: System MUST enforce conversation protection ("Chặn chia sẻ"), blocking forward and copy actions when enabled.

---

### 4.5 Module E: Media, Attachments, Audio & Stickers

#### 4.5.1 Media Resolution & Viewport Throttling
- The media resolver (`IZaloMediaService`) throttles concurrent downloads to a maximum of 3 concurrent requests.
- Requests are prioritized in LIFO order matching on-screen items in the active WinUI 3 viewport. When a user scrolls rapidly, off-screen requests are cancelled to conserve bandwidth.

#### 4.5.2 Video Container & Poster Generation
- Video thumbnail URLs may point directly to video streams rather than static images.
- The engine checks for an ISO-BMFF container (`ftyp` box at byte offset 4).
- If detected, Windows Media Foundation / `MediaPlayer` generates a `.poster.jpg` thumbnail from the frame at timestamp 0.1s.

#### 4.5.3 Animated Stickers & Sprites
- Sticker packages provide static PNG/WebP images and optional animations (sprite sheets with frame interval ms, or animated WebP).
- Animated stickers render using native WinUI 3 `AnimatedImage` or high-performance sprite composition without blocking the UI thread.
- Typing sticker suggestions query matching keywords from the draft text, presenting a horizontal recommendation strip above the composer (disabled when "Gợi ý Sticker" is turned off or in E2EE chats).

#### 4.5.4 Functional Requirements (Module E)
- **FR-MEDIA-001**: System MUST resolve media URLs with a maximum concurrency limit of 3 simultaneous downloads, prioritizing visible viewport items.
- **FR-MEDIA-002**: System MUST inspect video containers for ISO-BMFF box signatures (`ftyp` at offset 4) and generate `.poster.jpg` thumbnails.
- **FR-MEDIA-003**: System MUST render animated stickers via sprite sheets or animated WebP with frame interval timing.
- **FR-MEDIA-004**: System MUST sanitize OpenGraph link preview URLs, stripping non-HTTP/HTTPS protocols.

---

### 4.6 Module F: Contacts, Groups, Mentions & Polls

#### 4.6.1 Contact List & Diacritic-Insensitive Sorting
- Friends and contacts are partitioned into alphabetical sections (A-Z, #).
- Section headers use Vietnamese diacritic-folded lowercase keys (e.g. "Đ" maps to "D", "Á" maps to "A"). Non-letter characters map to `#`.

#### 4.6.2 Group Management & Roster
- Groups carry a `groupId` prefixed with `'g'`.
- Supports Admin/Owner promotion, member additions, and permission bitmasks (e.g., allow members to pin, change group name, or create polls).

#### 4.6.3 Group Polls
- Polls support multi-choice selection, anonymous voting, adding new options, and setting poll expiration.
- **Result Masking**: When `hidePreview = true` ("Ẩn kết quả khi chưa bình chọn"), option vote counts and voter avatars are masked until the user submits their vote or the poll is closed.

#### 4.6.4 Stranger Folder Aggregation
- Messages from non-friends without mutual interaction are grouped into a dedicated `stranger_box` folder, aggregating unread counts away from the main conversation list.

#### 4.6.5 Functional Requirements (Module F)
- **FR-SOC-001**: System MUST maintain contacts sorted alphabetically (A-Z, #) with diacritic-insensitive folding.
- **FR-SOC-002**: System MUST manage group rosters, roles (Admin/Member), and member count thresholds.
- **FR-SOC-003**: System MUST support group polls with multi-choice, anonymous voting, and hidden results until voted.
- **FR-SOC-004**: System MUST support user mentions with live autocomplete suggestions matching `@query` from group members.

---

### 4.7 Module G: Data Persistence & SQLite DAL 4.0

#### 4.7.1 Directory Layout & Database Partitioning
Databases are partitioned per user session under:
```
%LocalAppData%\Zenix\sqlite\_production\<account_id>\<fullname>.db
```
- Path traversal sequences (`..`, `.`) are strictly sanitized.
- Sub-partitions are isolated in dedicated subfolders.

#### 4.7.2 Connection Tuning & Concurrency
Every SQLite connection initialized via `Microsoft.Data.Sqlite` MUST execute the following pragmas:
```sql
PRAGMA journal_mode = WAL;
PRAGMA synchronous = NORMAL;
PRAGMA busy_timeout = 5000;
PRAGMA foreign_keys = ON;
```
- Dedicated serial dispatch queue per database connection prevents locking deadlocks.
- Busy retry loop handles transient database locks up to 5000ms before returning `SQLITE_BUSY`.

#### 4.7.3 Schema Migration Handshake
- At connection opening, the engine reads `PRAGMA user_version`.
- If `user_version` differs from the application schema version, the engine enters the migration state, suspending incoming queries.
- Migration DDL scripts run sequentially inside a transaction, update `user_version`, and resume all queued statements upon completion.

#### 4.7.4 UNIQUE Constraint Tolerance
- In `INSERT` statements executing under `run` methods, if a `SQLITE_CONSTRAINT` occurs containing `"UNIQUE constraint failed"`, the engine resolves safely with `{lastID: 0, changes: 0}` without crashing or rejecting the transaction, matching official node-sqlite3 driver behavior in Zalo PC.

#### 4.7.5 Functional Requirements (Module G)
- **FR-DB-001**: System MUST store local data in partitioned SQLite databases under `%LocalAppData%\Zenix\sqlite\_production\<name>.db`.
- **FR-DB-002**: System MUST configure SQLite connections with `PRAGMA journal_mode=WAL;`, `PRAGMA synchronous=NORMAL;`, and `PRAGMA busy_timeout=5000;`.
- **FR-DB-003**: System MUST handle UNIQUE constraint violations in `INSERT` statements by returning `{lastID: 0, changes: 0}` without aborting.
- **FR-DB-004**: System MUST execute schema migrations when `PRAGMA user_version` differs from the application version, pausing query execution during migration.

---

### 4.8 Module H: Presentation Layer (WinUI 3 & Clean MVVM)

#### 4.8.1 Architectural Topology
```
src/Zenix/
├── Views/
│   ├── Windows/ShellWindow.xaml
│   ├── Pages/LoginPage.xaml
│   ├── Pages/ChatPage.xaml
│   ├── Pages/ContactsPage.xaml
│   ├── Pages/ZCloudPage.xaml
│   └── Pages/SettingsPage.xaml
├── Controls/
│   ├── ConversationListView.xaml
│   ├── ConversationDetailView.xaml
│   ├── ChatComposerView.xaml
│   ├── MessageBubbleDataTemplateSelector.cs
│   └── PinnedGlassBanner.xaml
├── ViewModels/
│   ├── Windows/ShellViewModel.cs
│   ├── Pages/LoginViewModel.cs
│   ├── Pages/ChatViewModel.cs
│   ├── Pages/ContactsViewModel.cs
│   ├── Pages/ZCloudViewModel.cs
│   └── Pages/SettingsViewModel.cs
```

#### 4.8.2 Clean MVVM Invariants (Principle I & Principle X)
1. **Zero UI Types in ViewModels**: ViewModels must never reference `Microsoft.UI.Xaml.*`, `Window`, `Page`, `FrameworkElement`, `UIElement`, or `ContentDialog`.
2. **Zero Code-Behind UI Manipulation**: Views (`*.xaml.cs`) must never imperatively manipulate child containers (no `Children.Add`, `Children.Remove`, swapping content hosts, or setting child `Visibility`). View swapping must be declared in XAML via data binding:
   ```xml
   <ContentControl Content="{x:Bind ViewModel.ActivePageView, Mode=OneWay}" />
   ```
3. **Compiled Bindings (`{x:Bind}`)**: All data, properties, and RelayCommands must bind via `{x:Bind}` for compile-time verification.
4. **Declarative Context Menus & Flyouts**: Context menus and flyouts must be declared in XAML ResourceDictionaries, never created via `new MenuFlyout()` in code-behind.
5. **Abstracted OS Interop**: All file pickers must be injected via `IFilePickerService` and invoked from ViewModel RelayCommands.

#### 4.8.3 Dynamic Localization (Principle VII)
- Zero raw string literals in UI properties (`Text`, `Content`, `Header`, `Title`, `PlaceholderText`).
- Standardized dynamic indexer binding:
  ```xml
  <TextBlock Text="{Binding [Shell_Chat_Title], Source={StaticResource Localization}, Mode=OneWay}" />
  ```
- Strict UTF-8 without BOM encoding across all `.resx` files.

#### 4.8.4 Functional Requirements (Module H)
- **FR-UI-001**: Presentation layer MUST be implemented exclusively in WinUI 3 XAML with zero code-behind business logic.
- **FR-UI-002**: ViewModels MUST be 100% UI-agnostic with no dependencies on `Microsoft.UI.Xaml.*` or visual tree controls.
- **FR-UI-003**: All XAML data bindings MUST use compiled `{x:Bind}` (except `{Binding [Key], Source={StaticResource Localization}}` for dynamic i18n).
- **FR-UI-004**: UI text MUST NEVER be hardcoded and must be resolved dynamically via `TranslationSource` and `.resx` resources.
- **FR-UI-005**: All file pickers and dialogs MUST be abstracted behind `IFilePickerService` and `IDialogService`.
- **FR-UI-006**: Message lists and conversation feeds MUST utilize UI virtualization (`ListView` with `ItemsStackPanel`) to maintain 60 FPS.

---

## 5. Architecture Mapping: Reference (Swift / Electron) to .NET 10 WinUI 3

| Subsystem | Reference Architecture (Swift / Electron) | Target .NET 10 WinUI 3 Architecture (Zenix) | Responsibility & Mapping Details |
|---|---|---|---|
| **App Shell & Host** | `OfficialClientHost.swift`, `Electron main.js` | `ApplicationHostService.cs`, `Zenix.App` | Microsoft.Extensions.Hosting Generic Host DI, Single Instance IPC via Named Pipe. |
| **Window & Backdrop** | `NSWindow`, SwiftUI `LiquidGlass` | `ShellWindow.xaml`, `WinUIEx` WindowEx | Mica/Acrylic backdrop, custom title bar drag region, DPI-aware sizing. |
| **Navigation** | SwiftUI `NavigationStack` / TabView | `NavigationService.cs`, `Frame` view swapping | Declarative page navigation (`ChatPage`, `ContactsPage`, `SettingsPage`). |
| **Presentation Model** | SwiftUI `@Observable` / `@ObservedObject` | `CommunityToolkit.Mvvm` (`ObservableObject`) | UI-agnostic ViewModels with `[ObservableProperty]`, `[RelayCommand]`. |
| **UI Data Binding** | SwiftUI `$binding` | WinUI 3 Compiled `{x:Bind}` | Compile-time typed binding for data, properties, commands, and events. |
| **Localization (i18n)** | LocalizedStringKey, JSON bundles | `TranslationSource.cs`, `String.resx` (`vi`/`en`) | Dynamic indexer `{Binding [Key], Source={StaticResource Localization}}`. |
| **Realtime Gateway** | `NetworkProxy.swift`, `URLSessionWebSocketTask` | `ZaloGatewayService.cs`, `ClientWebSocket` | Persistent binary WebSocket, 4-byte framing `[v][cmd LE][subCmd]`, auto-reconnect. |
| **Wire Fingerprinting** | `ClientFingerprint.swift` | `ZaloWireFingerprintHandler.cs` (`DelegatingHandler`) | Injects Chromium 108 wire headers (`User-Agent`, `sec-ch-ua*`), strips Origin/Referer. |
| **REST API Engine** | `URLSession` + Axios shims | `HttpClientFactory` + `ZaloApiClient.cs` | Typed REST calls with query params `type=23&client_version=694`. |
| **DoH Fallback** | `DoHResolver.swift` | `DohResolverService.cs` (`HttpClient`) | Resolves CDN hosts over HTTPS (`https://dns.google/resolve`) on DNS failure. |
| **Credential Vault** | macOS Keychain, `SafeStorage` | `PasswordEncryptor.cs`, DPAPI / AES-256-GCM | Encrypted local storage for session cookies (`zpw_sek`), tokens, and private keys. |
| **Core Cryptography** | `CryptoBridge.swift`, `CommonCrypto` | `ZaloCryptoService.cs` (`System.Security.Cryptography`) | AES-256-CBC (`zcid`, params), HKDF-SHA256 device linking, media de-noising. |
| **Signal Protocol E2EE** | `SignalE2EE.swift`, `libsignal-protocol-c` | `SignalE2eeService.cs` (`ManagedSignalProtocol`) | Curve25519, X3DH prekey exchange, Double Ratchet, 60-digit Safety Numbers. |
| **Local Persistence** | `SQLiteHost.swift`, `utility-process-sqlite` | `ZaloSqliteDalService.cs` (`Microsoft.Data.Sqlite`) | Partitioned SQLite DAL 4.0, WAL mode, synchronous=NORMAL, 5s busy timeout. |
| **Media Resolution** | `ZaloEngine+Media.swift`, `AVAssetImageGenerator` | `ZaloMediaResolverService.cs`, Windows Media Transcoder | Viewport LIFO throttle (max 3 concurrent), ISO-BMFF `.poster.jpg` generation. |
| **File / Dialog Interop** | AppKit `NSOpenPanel`, `NSSavePanel` | `FilePickerService.cs`, `DialogService.cs` | Injected DI services resolving WinUI 3 `ContentDialog` and Windows App SDK pickers. |
| **Thread Marshalling** | `DispatchQueue.main.async` | `DispatcherQueue.TryEnqueue` | Marshals background event processing to WinUI 3 UI thread safely. |

---

## 6. Non-Functional Requirements & Constitutional Invariants

### 6.1 Principle I: Clean MVVM & Type Safety
- **UI-Agnostic ViewModels**: Zero references to UI visual types in ViewModels.
- **Zero Business Logic in Code-Behind**: Views only handle platform wiring.
- **Zero Code-Behind UI Manipulation**: Dynamic layout and view swapping must be 100% declarative XAML via data binding.
- **Compiled Bindings**: Compiled `{x:Bind}` mandatory for all data and commands.
- **Abstracted OS Dialogs**: No direct instantiation of `FileOpenPicker` or `ContentDialog` in code-behind; all pickers must be injected via `IFilePickerService` / `IDialogService`.
- **Declarative Context Menus**: Context menus must be declared in XAML, not imperatively constructed in C#.
- **Roslyn `IDE0065`**: All `using` directives placed outside and above `namespace`.
- **CRLF Line Endings**: Strict Windows CRLF (`\r\n`) across all repository files.

### 6.2 Principle II: Modern Fluent UI / UX Standards
- Native WinUI 3 and WinUIEx styling with Mica and Acrylic backdrops.
- Synchronized Light and Dark themes matching Windows system preferences.
- Asynchronous modal prompts using `ContentDialog` (with `XamlRoot` resolution) or `InfoBar`. Blocking Win32 `MessageBox.Show` is strictly prohibited.

### 6.3 Principle III: Secure Database Connection & Credential Protection
- All credentials, session tokens (`zpw_sek`), and encryption keys encrypted at rest via `PasswordEncryptor`.
- Zero plain-text secrets in logs, crash dumps, or error dialogs.
- All SQLite queries strictly parameterized to prevent SQL injection.

### 6.4 Principle IV: High Performance & Thread Stewardship
- Fluid 60 FPS UI performance (<16ms per frame).
- All I/O, networking, and cryptography offloaded to background threads (`Task.Run`, `async`/`await`).
- Every long-running task must accept and observe a `CancellationToken`.
- Virtualized collection rendering via `ListView` with `ItemsStackPanel`.

### 6.5 Principle V: English Technical Documentation Standard
- 100% XML doc comments in technical American English across all public and internal classes, interfaces, records, methods, properties, and RelayCommands.

### 6.6 Principle VI: Spec-Driven Development & Superpowers Discipline (GSS-Bridge)
- Spec-Kit central authority (`spec.md` $\to$ `plan.md` $\to$ `tasks.md`).
- Blast radius and dependency cycle verification via Graphify and GSS-Bridge.
- Test-driven development with isolated reproduction test cases before bug fixes.

### 6.7 Principle VII: Zero Hardcoded User-Facing Strings & Dynamic i18n
- Absolute ban on raw string literals in UI properties (`Text`, `Content`, `Header`, `Title`, `PlaceholderText`).
- Standardized dynamic indexer binding: `{Binding [KeyName], Source={StaticResource Localization}, Mode=OneWay}`.
- Resource bundles (`String.resx`, `String.en.resx`, `String.vi.resx`) encoded in strict UTF-8 without BOM.
- Automated static test enforcement (`LocalizationVerificationTests.cs`) asserting 100% key parity across all resx bundles and XAML files.

### 6.8 Principle VIII: Non-Intrusive Agent Desktop Automation & Visual Verification Gate
- Automated UI inspection using `agent-desktop` over Windows UI Automation (UIA).
- Headless execution targeting semantic qualified `@ref` IDs without mouse cursor hijacking.
- Desktop isolation bridge (`WinSta0\default` via `bridge.py`) for agent terminal sandbox environments.
- Zero-AutomationId invariant: Production XAML must not be polluted with test IDs.
- Mandatory visual screenshot verification (`agent-desktop screenshot`) prior to task completion.

### 6.9 Principle IX: Deterministic Code Knowledge Graph & Architecture Radar (`graphify`)
- AST analysis focusing strictly on source code (`Zenix/`), excluding agent metadata via `.graphifyignore`.
- Graph-guided navigation (`graphify query`, `graphify explain`, `graphify path`).
- Tarjan SCC cycle detection to prevent circular dependencies.

### 6.10 Principle X: Clean Engineering, Root-Cause Resolution & Anti-Patchwork Discipline
- Root-cause resolution over symptoms: No empty `catch { }` blocks, no indiscriminate `?.` suppression, no static flag hacks, no thread sleeps masking race conditions.
- The Boy Scout Rule: Touching any file requires leaving it cleaner, dead-code-free, fully typed, documented with English XML comments, Roslyn `IDE0065` compliant, and formatted with pure CRLF line endings.

---

## 7. Edge Cases, Resiliency & Degradation Matrix

| Edge Case ID | Scenario | Reference Trigger | Required System Behavior |
|---|---|---|---|
| **EC-001** | Multi-layer QR Authentication | Long-poll returns `error_code: -20` | Do not show error dialog. Automatically invoke `genSecondQR`, request secondary device linking QR, and keep `waitingScan` active with `isSecondQR = true`. |
| **EC-002** | QR Code Timeout | Polling exhausts ~60s (~3 poll cycles) | Transition UI state to expired (`step = .expired`), hide QR image, and display localized "Mã QR đã hết hạn - Bấm để tải lại". |
| **EC-003** | Host-only Cookie Defect | Cookie `zpw_sek` missing leading dot | Ensure all session cookies for `.zalo.me` explicitly include leading dot domain so WebSocket subdomains (`wss://wsN-msg.chat.zalo.me`) authorize successfully. |
| **EC-004** | Desktop Session Kickout | Server event on `/api/session/kickout-info` | Cleanly terminate WebSocket and polling loops, clear cached memory credentials, and present asynchronous `ContentDialog` explaining single-session policy. |
| **EC-005** | Anti-Bot Fingerprint Detection | REST requests with `Origin` or `Referer` | Custom `DelegatingHandler` strips `Origin` and `Referer` from all outgoing HTTP requests matching official Chromium `file://` behavior. |
| **EC-006** | Out-of-Order Message Arrival | Incoming message timestamp older than last message | Tag collection with `needsSort` flag and execute stable sorting by `(timestamp, offset)` to prevent conversational inversion. |
| **EC-007** | Send Failure Draft Preservation | Network drop during message transmission | Preserve draft text, mentions, and quotes into `FailedDraftStore` per conversation ID. Auto-restore draft when conversation reopens. |
| **EC-008** | Mention Offset Drift on Trim | User types leading whitespace before `@mention` | Subtract leading whitespace count from mention `pos` offsets. If target offset is invalid or char != `@`, prune mention from wire payload. |
| **EC-009** | SQLite Constraint Conflict | `INSERT` statement violates UNIQUE key | Catch `SQLITE_CONSTRAINT` with UNIQUE error and resolve gracefully returning `{lastID: 0, changes: 0}` without application crash. |
| **EC-010** | Database Schema Mismatch | File `PRAGMA user_version` != application version | Suspend normal query pool, trigger schema migration DDL, update `user_version`, and resume pending query continuations. |
| **EC-011** | Typing Indicator Flood | Peer sends message while typing | Automatically clear typing indicator immediately upon receipt of a new message from that peer, or timeout after 5 seconds of inactivity. |
| **EC-012** | Local Message Deletion Grace Period | User selects "Xóa chỉ ở phía tôi" | Display undo countdown bar (5-30s). Revert deletion if user clicks "Khôi phục"; permanently purge upon countdown expiration. |
| **EC-013** | Protected Conversation Sharing | User attempts to forward in protected chat | Inspect `isProtected` flag on conversation. If true, block forward/copy command and display warning InfoBar "Trò chuyện này đã bật Chặn chia sẻ". |
| **EC-014** | Viewport Media Download Storm | User scrolls past 100 media bubbles | Throttle concurrent downloads to maximum 3 concurrent tasks using LIFO priority queue. Cancel off-screen media downloads. |
| **EC-015** | Anti-Scraping Noise on Media | Media file downloaded with scrambled bytes | Verify presence of signature `077a416c4f07` and apply XOR key `147` de-noising algorithm before saving to disk cache. |
| **EC-016** | Call Payload Exceeds Buffer Limit | VoIP control packet > 4000 characters | Split payload into sequenced frames: `<chunk>#<seq>#<count>#<index>#$`. Await single-byte ACK from receiver before dispatching next frame. |
| **EC-017** | Peer Profile Resolution Failure | User ID in 1-1 chat cannot be resolved via `getminiprofiles` | Triggers `PC_GET_NOISED_ID_FAILED`. Reaching `max_batch_fail` aborts sync with code 1116. Fall back to encryption via `/api/gid/encrypt`. |
| **EC-018** | First Message in E2EE Chat | No active session established with recipient | Libsignal initiates X3DH key agreement and emits a Type 3 `PreKeyWhisperMessage` consuming one-time prekey, transitioning subsequent messages to Type 1. |
| **EC-019** | Group Poll Results Masked | Poll created with "Ẩn kết quả khi chưa bình chọn" | Option vote counts and percentages remain masked (`resultsHidden = true`) until user submits their vote or poll is closed. |
| **EC-020** | Multi-Photo Album Explosion | Incoming message is `PHOTO_GROUP: 2503` | Adapter normalizes album payload into individual `ChatMessage` records, preserving album grouping key while supporting discrete reactions and forwards. |

---

## 8. Verification & Acceptance Criteria Gate

- [x] **C-01: Specification Completeness**: All 8 functional modules (Auth, Gateway, Crypto, Messaging, Media, Contacts/Groups, SQLite DAL, WinUI 3 Clean MVVM) fully specified with input/output behaviors and edge cases.
- [x] **C-02: 1:1 Architecture Mapping**: Comprehensive cross-platform mapping from Swift/Electron reference to .NET 10 WinUI 3 C#.
- [x] **C-03: Constitutional Compliance**: 100% adherence to all 10 principles of `CONSTITUTION.md` (Clean MVVM, UI-agnostic ViewModels, compiled `{x:Bind}`, dynamic `TranslationSource` i18n, zero code-behind UI manipulation, abstracted OS pickers, async/cancellation, English XML docs, Roslyn `IDE0065`, and CRLF line endings).
- [x] **C-04: Spec-Kit Ready**: Fully aligned with Spec-Kit templates, providing actionable user stories and functional requirements for downstream planning (`speckit-plan`) and task breakdown (`speckit-tasks`).
