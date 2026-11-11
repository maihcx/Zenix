# Project: Wave 2 — Storage & Cryptography Core

## Architecture
Wave 2 delivers the foundational Storage and Cryptographic engines of Zenix's Zalo port:
1. **Storage Layer**:
   - SQLite DAL 4.0 (`ZaloSqliteDalService`) using `Microsoft.Data.Sqlite` targeting `%LocalAppData%\Zenix\sqlite\_production\<account>\chat_storage.db` and supporting in-memory mode (`:memory:`).
   - Mandatory Pragmas: `PRAGMA journal_mode = WAL; PRAGMA synchronous = NORMAL; PRAGMA busy_timeout = 5000; PRAGMA cache_size = -8000; PRAGMA foreign_keys = ON;`.
   - Concurrency: Write serialization via `SemaphoreSlim(1,1)` and concurrent non-blocking read connection leases.
   - Safe UNIQUE Conflict Handling: Catches UNIQUE constraint violations (`SqliteErrorCode == 19`) returning `{ LastInsertedId = 0, RowsAffected = 0 }` without fatal exceptions.
   - Migrator: `ZaloSqliteMigrator` managing `PRAGMA user_version = 400` executing `Schema_v400.sql` (tables: `conversations`, `messages`, `contacts`, `group_profiles`/`groups`, `group_members`, `drafts`, `keychain_keys`, `sync_checkpoints`).
   - Indexes: `idx_messages_conv_ts (conversation_id, timestamp DESC, sequence_offset DESC)`, `idx_messages_cli_id`, `idx_conv_updated`, `idx_contacts_sort`.
   - Repositories:
     - `ConversationRepository`: thread metadata, unread count, pin/archive flags, ordered by `last_activity_at DESC`.
     - `MessageRepository`: paginated retrieval (50 messages $\le 1.5\text{ ms}$, stable tie-breaking `timestamp DESC, sequence_offset DESC`), delivery status updates (10 -> 1 -> 2 -> 3), recall mutation to `RecalledPayload`.
     - `FailedDraftStore`: draft persistence, UTF-16 mention offset shifting, notice tracking.
     - `ContactRepository` & `GroupRepository`: alphabetical section groupings (`#`, `A-Z`), group profile and member roles (`Member=0`, `Admin=1`, `Creator=2`).
     - `ZaloCredentialStorage`: Windows DPAPI (`ProtectedData`) / AES-256-GCM, `.zalo.me` domain scoping, log scrubbing (`[REDACTED]`).
     - `ZaloStorageService`: unified facade coordinating all repositories and DAL.

2. **Cryptography Layer**:
   - Primitives: `ZaloCryptoService` with hardware-accelerated AES-256-CBC:
     - `EncryptZcid`: key `"3FC4F0D2AB50057BCE0D90D9187A22B1"`, zero IV 16B, PKCS7, lowercase hex.
     - `EncryptApiParams` & `DecryptApiParams`: key `"DE71B7D4C57C0749158CB90D5E431AE3"`, zero IV 16B, PKCS7, Base64.
   - Keychain Envelope: `ZaloCryptoService.Keychain.cs`:
     - Layout: `[0x49: 1B][IV: 16B][Salt: 16B][Ciphertext: NB][HMAC: 32B]` (min length 65 bytes).
     - HKDF-SHA256 (`info = "ZaloDeviceLink"`, fallback `"ZprotocolSecureKDF"`) generating 64 bytes (32B AES key + 32B HMAC key).
     - Constant-time verification: `CryptographicOperations.FixedTimeEquals`.
   - Media De-noising: `MediaDenoiseHelper.cs`:
     - Strips anti-scraping signature `077a416c4f07` and applies byte-wise XOR 147 (`0x93`).
   - Signal Protocol E2EE:
     - `ISignalE2eeService.cs`: Service contract complying with `ContractsArchitectureTests`.
     - `Curve25519Helper.cs`: Montgomery ladder key agreements ($u = 9$, RFC 7748 clamping).
     - `SignalX3dhService.cs`: Identity, SignedPreKey (signed by identity key), One-Time Prekey bundles, master shared secret $SK = \text{HKDF}(\text{DH}_1 \,||\, \text{DH}_2 \,||\, \text{DH}_3 \,[||\, \text{DH}_4], \text{salt}=0_{32}, \text{info}=\text{"ZaloText"})$.
     - `DoubleRatchetSessionState.cs`: Symmetric chains, rootKey, skipped message keys table with TTL (max 2000 keys).
     - `SignalDoubleRatchetService.cs`: Symmetric KDF chains (`0x01` messageKey, `0x02` nextChainKey), asymmetric DH ratchet (`info = "ZaloRatchet"`), message key expansion (`info = "ZaloMessageKeys"`).
     - `SafetyNumberFormatter.cs`: 5200 iterations SHA-512, 12 groups of 5 decimal digits.
     - `SignalE2eeService.cs`: Protobuf `WhisperMessage` (Type 1) and `PreKeyWhisperMessage` (Type 3) wire envelopes (`0x33` version, 8-byte HMAC tag).

## Feature Inventory
| # | Feature | Description | Milestone | Source |
|---|---------|-------------|-----------|--------|
| 1 | SQLite DAL 4.0 Core & Pragmas | `ZaloSqliteDalService` with WAL, pragmas, Semaphore lease pool, UNIQUE conflict handling | M2-A (T-008) | tasks.md § T-008 |
| 2 | SQLite Schema Migrator (v400) | `ZaloSqliteMigrator`, schema v400 DDL, indexes, `user_version = 400` | M2-A (T-009) | tasks.md § T-009 |
| 3 | Conversation & Message Repositories | `ConversationRepository`, `MessageRepository` (sort `ts DESC, sequence_offset DESC`, pagination $\le 1.5\text{ ms}$), `FailedDraftStore` | M2-A (T-010) | tasks.md § T-010 |
| 4 | Contact & Group Repositories | `ContactRepository`, `GroupRepository`, alphabet sectioning (`#`, `A-Z`), member role mappings | M2-A (T-011) | tasks.md § T-011 |
| 5 | Credential Storage & Token DPAPI | `ZaloCredentialStorage` with DPAPI / AES-256-GCM, domain scoping `.zalo.me`, zero plain text | M2-A (T-012) | tasks.md § T-012 |
| 6 | ZaloStorageService Facade | `ZaloStorageService` facade coordinating all repositories and DAL | M2-A (T-013) | tasks.md § T-013 |
| 7 | AES-256-CBC & zcid Hex Primitives | `ZaloCryptoService` (`EncryptZcid`, `EncryptApiParams`, `DecryptApiParams`), fixed keys, zero IV, PKCS7 | M2-B (T-014) | tasks.md § T-014 |
| 8 | Keychain Envelope & Media De-noising | HKDF-SHA256, constant-time HMAC, Keychain envelope `[0x49]`, XOR 147 media de-noising | M2-B (T-015) | tasks.md § T-015 |
| 9 | Curve25519 & X3DH Pre-Key Service | `SignalX3dhService`, Curve25519 DH agreements, PreKey bundle, master shared secret | M2-B (T-016) | tasks.md § T-016 |
| 10 | Double Ratchet Engine | `SignalDoubleRatchetService`, symmetric/asymmetric ratcheting, skipped message keys table | M2-B (T-017) | tasks.md § T-017 |
| 11 | Signal E2EE & Safety Number Formatter | `SignalE2eeService`, Whisper envelopes, 60-digit Safety Number generator | M2-B (T-018) | tasks.md § T-018 |

## Milestones
| # | Name | Scope | Dependencies | Status |
|---|------|-------|-------------|--------|
| M2-Survey | Exploration & Spec Mining | Deep survey of existing codebase, contracts, specs, and zalo-swift-re reference implementations | none | DONE |
| M2-A | Storage Infrastructure & Repositories | T-008 to T-013: SQLite DAL, Migrator, Repositories, Credential Storage, Facade | M2-Survey | DONE |
| M2-B | Core Cryptography & Signal E2EE | T-014 to T-018: AES primitives, Keychain HKDF, XOR 147, X3DH, Double Ratchet, E2EE Service | M2-Survey | DONE |
| M2-C | Verification & GATE_02 Compliance | GATE 2A, GATE 2B, full regression (83/83 Wave 1 tests), clean build | M2-A, M2-B | DONE |

## Interface Contracts
- `IZaloStorageService.cs` in `Zenix/Core/Zalo/Contracts/IZaloStorageService.cs` (includes `IConversationRepository`, `IMessageRepository`, `IContactRepository`).
- `IZaloCryptoService.cs` in `Zenix/Core/Zalo/Contracts/IZaloCryptoService.cs`.
- `ISignalE2eeService.cs` in `Zenix/Core/Zalo/Contracts/ISignalE2eeService.cs` (new contract to add).

## Code Layout & Ownership Boundaries
- **Storage Subsystem (Owned exclusively by Worker Storage)**:
  - `Zenix/Core/Zalo/Storage/ZaloSqliteDalService.cs`
  - `Zenix/Core/Zalo/Storage/SqliteExecutionResult.cs`
  - `Zenix/Core/Zalo/Storage/ZaloSqliteMigrator.cs`
  - `Zenix/Core/Zalo/Storage/Scripts/Schema_v400.sql`
  - `Zenix/Core/Zalo/Storage/Repositories/ConversationRepository.cs`
  - `Zenix/Core/Zalo/Storage/Repositories/MessageRepository.cs`
  - `Zenix/Core/Zalo/Storage/Repositories/FailedDraftStore.cs`
  - `Zenix/Core/Zalo/Storage/Repositories/ContactRepository.cs`
  - `Zenix/Core/Zalo/Storage/Repositories/GroupRepository.cs`
  - `Zenix/Core/Zalo/Storage/ZaloCredentialStorage.cs`
  - `Zenix/Core/Zalo/Storage/ZaloStorageService.cs`
  - `tests/Zenix.Tests/Zalo/Storage/ZaloSqliteDalTests.cs`
  - `tests/Zenix.Tests/Zalo/Storage/SqliteMigrationTests.cs`
  - `tests/Zenix.Tests/Zalo/Storage/MessageRepositoryTests.cs`
  - `tests/Zenix.Tests/Zalo/Storage/ContactRepositoryTests.cs`
  - `tests/Zenix.Tests/Zalo/Storage/CredentialStorageTests.cs`
  - `tests/Zenix.Tests/Zalo/Storage/ZaloStorageServiceTests.cs`

- **Cryptography Subsystem (Owned exclusively by Worker Crypto)**:
  - `Zenix/Core/Zalo/Contracts/ISignalE2eeService.cs`
  - `Zenix/Core/Zalo/Cryptography/ZaloCryptoService.cs`
  - `Zenix/Core/Zalo/Cryptography/ZaloCryptoService.Keychain.cs`
  - `Zenix/Core/Zalo/Cryptography/MediaDenoiseHelper.cs`
  - `Zenix/Core/Zalo/Cryptography/Curve25519Helper.cs`
  - `Zenix/Core/Zalo/Cryptography/SignalX3dhService.cs`
  - `Zenix/Core/Zalo/Cryptography/DoubleRatchetSessionState.cs`
  - `Zenix/Core/Zalo/Cryptography/SignalDoubleRatchetService.cs`
  - `Zenix/Core/Zalo/Cryptography/SafetyNumberFormatter.cs`
  - `Zenix/Core/Zalo/Cryptography/SignalE2eeService.cs`
  - `tests/Zenix.Tests/Zalo/Crypto/ZaloCryptoTests.cs`
  - `tests/Zenix.Tests/Zalo/Crypto/SignalX3dhTests.cs`
  - `tests/Zenix.Tests/Zalo/Crypto/DoubleRatchetTests.cs`
  - `tests/Zenix.Tests/Zalo/Crypto/SignalE2eeTests.cs`
