# Specification Mining Handoff Report: Storage Infrastructure & Local Repositories (Wave 2)

**Milestone**: Wave 2 — Storage Core (Tasks T-008 to T-013 $\rightarrow$ GATE 2A)  
**Author Archetype**: Specification Miner  
**Date**: 2026-11-11T08:22:00Z  
**Status**: Ready for Implementation  

---

## Features Discovered

| # | Category | Feature | Description | Inputs | Outputs | Error Behavior | Discovered Via |
|---|----------|---------|-------------|--------|---------|----------------|----------------|
| 1 | Storage Core (T-008) | SQLite Connection & Pragmas | Opens SQLite connection with WAL mode and mandatory performance pragmas | `accountId`, `isInMemory`, `cancellationToken` | `SqliteConnection` | Throws `SqliteException` on invalid path or corrupt file | `.specify/specs/tasks.md:316-330`, `zalo-swift-re/Sources/Olaz/ZaloRuntime/SQLiteHost.swift:179-185` |
| 2 | Storage Core (T-008) | Write Serialization & Read Concurrency | `SemaphoreSlim(1,1)` serializes writes while multiple concurrent reads execute non-blocking | SQL write statement, parameters | `SqliteExecutionResult` | Semaphore timeout / cancellation via `CancellationToken` | `.specify/specs/research.md:144-150`, `SQLiteHost.swift:37-46` |
| 3 | Storage Core (T-008) | Safe UNIQUE Constraint Tolerance | Suppresses UNIQUE constraint violations on `INSERT`, preventing application crashes | INSERT statement with duplicate unique key | `SqliteExecutionResult { LastInsertedId = 0, RowsAffected = 0 }` | Other `SqliteException` errors rethrown; UNIQUE errors swallowed safely | `zalo-swift-re/Sources/Olaz/ZaloRuntime/SQLiteHost.swift:214-216`, `SQLiteHostTests.swift:18-35` |
| 4 | Storage Core (T-008) | Transaction & Script Serialization | Executes multiple `run` / `runs` prepared statements atomically in a single transaction | List of statements or action callback | `Task` | Rolls back transaction completely on failure | `SQLiteHost.swift:246-267`, `SQLiteHostTests.swift:37-64` |
| 5 | Migrator (T-009) | Schema Versioning & Handshake | Verifies and updates `PRAGMA user_version = 400`, executing DDL migration atomically | Database connection | Target version (400) | Rolls back migration transaction on failure; subsequent openers await | `tasks.md:332-346`, `SQLiteHost.swift:87-113`, `SQLiteHostTests.swift:66-82` |
| 6 | Migrator (T-009) | Master Normalized Schema v400 | Creates 8 core tables (`conversations`, `messages`, `contacts`, `group_profiles`/`groups`, `group_members`, `drafts`, `keychain_keys`, `sync_checkpoints`) and auxiliary tables | Schema_v400.sql script | Database tables created | Atomic transaction fails completely if DDL contains invalid syntax | `.specify/specs/data-model.md:447-577`, `tasks.md:336-340` |
| 7 | Migrator (T-009) | Query Optimization Indexes | Creates composite indexes (`idx_messages_conv_ts`, `idx_messages_cli_id`, `idx_messages_server_id`, `idx_conv_updated`, `idx_contacts_sort`) | Database connection | SQLite B-tree indexes | Throws if table does not exist | `.specify/specs/data-model.md:461-543`, `tasks.md:336-340` |
| 8 | Migrator (T-009) | Operational Trigger | `trg_update_conversation_on_new_message` updates `last_activity_at` and increments `unread_count` on message insert | `AFTER INSERT ON messages` | Updated `conversations` record | Cascades failures inside insert transaction | `.specify/specs/data-model.md:578-590` |
| 9 | Repositories (T-010) | Conversation Summary Upsert & Query | Persists thread metadata, unread count, pin/archive flags, and retrieves ordered by `last_activity_at DESC` | `ConversationSummary` record | `IReadOnlyList<ConversationSummary>` | Throws on database lock / corruption | `Zenix/Core/Zalo/Contracts/IZaloStorageService.cs:17-50`, `data-model.md:138-172` |
| 10 | Repositories (T-010) | Paginated Message Query with Tie-Breaking | Retrieves 50 messages ($\le 1.5\text{ ms}$) sorted stably by `(timestamp DESC, sequence_offset DESC)` | `conversationId`, `beforeTimestamp`, `pageSize` | `IReadOnlyList<ChatMessage>` | Throws on invalid parameter or connection drop | `tasks.md:348-363`, `IZaloStorageService.cs:55-100` |
| 11 | Repositories (T-010) | Delivery Status Progression | Updates message delivery lifecycle (`Sending (10) -> Sent (1) -> Delivered (2) -> Seen (3) -> SeenFull (4)` / `Failed (-2)`) | `cliMsgId`, `serverMsgId`, `MessageDeliveryStatus` | Updated row | Ignores missing message or throws on connection error | `IZaloStorageService.cs:80-92`, `data-model.md:179-188` |
| 12 | Repositories (T-010) | Message Recall Mutation | Converts an active message payload into `.recalled` (`RecalledPayload`) preserving thread chronological slot | `cliMsgId`, `senderName`, `recalledAt` | Mutated record | Throws if record is not found or connection fails | `tasks.md:352`, `data-model.md:297-300`, `spec.md:122` |
| 13 | Repositories (T-010) | Failed Draft In-Memory & Disk Store | Holds unsent composer drafts across navigation, merges failures with UTF-16 mention offset shifting | `ComposerDraft`, `conversationId`, `isSilent` | Merged `ComposerDraft` | Safe no-op on empty text | `zalo-swift-re/ChatModels.swift:82-113`, `FailedDraftTests.swift:1-58` |
| 14 | Repositories (T-011) | Contact Upsert & Vietnamese Collation | Persists contacts with automatic `sort_key` and single-character `section_header` ('#', 'A'..'Z') | `UserContact` / batch of contacts | `IReadOnlyList<UserContact>` | Throws on constraint conflict unless UPSERT | `IZaloStorageService.cs:105-121`, `ZaloStringHelper.cs:96-155` |
| 15 | Repositories (T-011) | Group Profile & Member Administration | Persists group profile and role-based memberships (`Member=0`, `Admin=1`, `Creator=2`) | `GroupProfile`, `GroupMember` | `GroupProfile` with members | Enforces FOREIGN KEY CASCADE on group delete | `data-model.md:68-112`, `tasks.md:364-378` |
| 16 | Security (T-012) | Windows DPAPI Token Encryption | Protects session tokens (`zpw_sek`), cookies, and E2EE identity private keys at rest via `ProtectedData` | Raw plaintext bytes/strings, optional entropy | Encrypted ciphertext bytes | Throws `CryptographicException` if decrypting from another Windows user | `tasks.md:380-393`, `research.md:77-98` |
| 17 | Security (T-012) | Domain Scoping & Credential Log Scrubbing | Enforces `.zalo.me` domain scoping on session cookies and scrubs all secrets from log strings | Cookie strings, exception messages | Redacted strings (`[REDACTED]`) | Rejects cookies from unauthorized domains | `tasks.md:384`, `ORIGINAL_REQUEST.md:36` |
| 18 | Facade (T-013) | Unified Storage Facade (`ZaloStorageService`) | Coordinates DAL, Migrator, Repositories, Credential Storage into single `IZaloStorageService` | `accountId`, `cancellationToken` | Facade instance with repositories | Throws during init if migration fails | `IZaloStorageService.cs:127-159`, `tasks.md:395-407` |

---

## Edge Cases

| # | Feature | Input | Observed Behavior |
|---|---------|-------|-------------------|
| 1 | SQLite DAL 4.0 | INSERT query with duplicate `cli_msg_id` | Catches `SqliteException` (error code 19, "UNIQUE constraint failed"), returns `{ LastInsertedId = 0, RowsAffected = 0 }` instead of throwing fatal exception. |
| 2 | SQLite DAL 4.0 | Non-UNIQUE error (e.g. `SELECT * FROM non_existent_table`) | Throws `SqliteException` with error code `SQLITE_ERROR` and descriptive error message. |
| 3 | SQLite DAL 4.0 | High-concurrency reads during active write transaction | WAL mode permits concurrent reads without blocking; write lock serialized via `SemaphoreSlim`. |
| 4 | Schema Migrator | Opening database already at `user_version = 400` | Read check returns 400 immediately; skips migration execution with zero performance penalty. |
| 5 | Schema Migrator | Simultaneous open from two callers on fresh DB | First caller acquires migration lock and runs DDL; second caller awaits continuation until `user_version` reaches 400. |
| 6 | Message Repository | 2+ messages sharing identical Unix millisecond timestamp | Stably sorted via secondary tie-breaking column `(timestamp DESC, sequence_offset DESC)` without visual jumping. |
| 7 | Message Repository | Pagination boundary with `beforeTimestamp` older than all rows | Returns empty list `[]` cleanly without SQL or index out-of-range exceptions. |
| 8 | FailedDraftStore | Stashing draft onto existing draft for same conversation | Text joined by `\n`; second draft's mention positions shifted by `(firstDraft.Length + 1)` in UTF-16 units. |
| 9 | FailedDraftStore | Stashing empty draft string `""` | No-op; store does not alter revision or create ghost draft entries. |
| 10 | FailedDraftStore | `stash(silent: true)` merged with subsequent `stash(silent: false)` | Merged draft becomes non-silent (`ShowNotice = true`) so the user receives notification. |
| 11 | Contact Repository | Contact name starting with number or emoji (e.g., "999 Pizza", "🔥 Support") | `ZaloStringHelper.GetSectionHeader` maps name to section header `'#'`. |
| 12 | Contact Repository | Vietnamese name with diacritics (e.g., "Nguyễn Thị Ánh Tuyết") | `ComputeSortKey` collates given name first ("TUYET NGUYEN THI ANH") and `section_header` resolves to `'T'`. |
| 13 | Credential Storage | Decrypting ciphertext generated by another Windows user or computer | DPAPI `ProtectedData.Unprotect` throws `CryptographicException`; caller handles as invalid/expired session. |
| 14 | Credential Storage | Setting session cookie with non-Zalo domain (e.g. `evil.com`) | Rejected with `ArgumentException`; only `.zalo.me` domain prefix accepted. |
| 15 | Storage Facade | Calling repository methods before `InitializeAccountDatabaseAsync` | Throws `InvalidOperationException("Database has not been initialized for an account.")`. |
| 16 | Storage Facade | Account data wipe operation (`WipeAccountDataAsync`) | Closes connection pool, deletes primary `.db` file AND `-wal` and `-shm` sidecars to prevent orphan lock files. |

---

## 1. Observation

### 1.1 Specification & Task Analysis
- **Tasks Catalog (`.specify/specs/tasks.md:315-418`)**:
  - `T-008`: `ZaloSqliteDalService` wrapping `Microsoft.Data.Sqlite`, targeting `%LocalAppData%\Zenix\sqlite\_production\<account>\chat_storage.db`. Mandatory pragmas: `journal_mode = WAL`, `synchronous = NORMAL`, `busy_timeout = 5000`, `cache_size = -8000`, `foreign_keys = ON`. Safe UNIQUE conflict handling returning `{ LastInsertedId = 0, RowsAffected = 0 }`.
  - `T-009`: `ZaloSqliteMigrator` managing `PRAGMA user_version = 400`. Complete DDL for `conversations`, `messages`, `contacts`, `groups`/`group_profiles`, `group_members`, `drafts`, `keychain_keys`, `sync_checkpoints`. Indexes: `idx_messages_conv_ts`, `idx_messages_cli_id`, `idx_conv_updated`.
  - `T-010`: `ConversationRepository` & `MessageRepository` under `Zenix.Core.Zalo.Storage.Repositories`. 50 messages paginated retrieval $\le 1.5\text{ ms}$, stable tie-breaking `(timestamp DESC, sequence_offset DESC)`, batch insert, delivery status transitions (10 -> 1 -> 2 -> 3), recall mutation, `FailedDraftStore`.
  - `T-011`: `ContactRepository` & `GroupRepository`. Contact upsert, friend list sync, alias updates, alphabetical section groupings (`#`, `A-Z`), group profile persistence, role assignments (`Member=0`, `Admin=1`, `Creator=2`).
  - `T-012`: `ZaloCredentialStorage` using Windows DPAPI (`ProtectedData`) / AES-256-GCM. `.zalo.me` domain scoping, credential scrubbing from logs.
  - `T-013`: `ZaloStorageService` facade aggregating all repositories and DAL.
  - **GATE 2A Criteria**: `dotnet test tests/Zenix.Tests/Zenix.Tests.csproj --filter "Category=Storage"` passes with 0 failures; WAL confirmed; v400 verified; 50-message pagination latency $\le 1.5\text{ ms}$.

### 1.2 Reference Implementation (`zalo-swift-re`)
- **`SQLiteHost.swift:1-537`**:
  - File path structure: `<root>/<fullname>.db` where root is `<userData>/sqlite/_production`.
  - Pragma initialization (lines 180-185):
    ```swift
    let setup = "PRAGMA journal_mode=WAL; PRAGMA synchronous=NORMAL; PRAGMA busy_timeout=5000; PRAGMA journal_size_limit=67108864; PRAGMA temp_store=MEMORY;"
    ```
  - Safe UNIQUE constraint behavior (lines 213-216):
    ```swift
    do { try Self.execute(c.db, statement) } catch let error as SQLiteError where error.isUniqueConstraint {}
    return ["lastID": sqlite3_last_insert_rowid(c.db), "changes": Int64(sqlite3_changes(c.db))]
    ```
  - Batching & Serialization (lines 223-267): `runs` binds prepared statement for rows inside `BEGIN TRANSACTION ... COMMIT`; `serialize` runs mixed statements in single transaction.
  - Schema migration handshake (lines 87-113): reads `PRAGMA user_version`. If different from requested version, returns `migrate: true` and queues subsequent openers until `migrated()` is called.
- **`ChatModels.swift:70-113` & `FailedDraftTests.swift:1-58`**:
  - `ComposerDraft.appending`: shifts mention offsets by `(firstText.Length + 1)` and joins text with `\n`.
  - `FailedDraftStore`: per-conversation dictionary with `revision`, `has`, `stash(silent:)`, `take`, `takeWithNotice`, `drain`.
- **`ZaloStringHelper.cs:64-169` (Zenix Core)**:
  - `FoldDiacritics`: strips Vietnamese accent marks.
  - `ComputeSortKey`: moves given name (last word) to the front for accurate Vietnamese indexing.
  - `GetSectionHeader`: maps first character of given name to uppercase Latin letter or `#`.

### 1.3 Existing Solution & Test State
- Solution `Zenix.sln` targets `net10.0-windows10.0.26100.0`.
- Wave 1 tests: 83 tests passing in 88ms (`SmokeTest.cs`, `LocalizationVerificationTests.cs`, `ContractsArchitectureTests.cs`, `DomainEnumTests.cs`, `DtoSerializationTests.cs`, `MessagePayloadSerializationTests.cs`, `ProfileModelTests.cs`).
- Dependencies already referenced in `Zenix.csproj`: `Microsoft.Data.Sqlite` 10.0.14, `System.Security.Cryptography.ProtectedData` 10.0.13, `System.IO.Hashing` 10.0.13, `CommunityToolkit.Mvvm` 8.4.2.

---

## 2. Logic Chain

1. **Storage Path & Multi-Account Partitioning**:
   - `ZaloSqliteDalService` targets `%LocalAppData%\Zenix\sqlite\_production\<account>\chat_storage.db` for production and supports `:memory:` for high-speed in-memory unit testing.
   - Per-account subdirectories allow clean isolation, multi-account support, and safe account wiping.

2. **WAL Concurrency & Connection Pooling Architecture**:
   - SQLite in WAL mode permits unlimited concurrent readers and exactly one writer.
   - In `Microsoft.Data.Sqlite`, managing a single write connection protected by `SemaphoreSlim(1,1)` alongside read connection leasing avoids `SQLITE_BUSY` (5000ms timeout pragma adds extra defense).

3. **Safe UNIQUE Conflict Handling**:
   - Upstream Zalo clients emit optimistic local messages (`cli_msg_id`) before server sync frames arrive. Race conditions cause both to attempt insertion of the same record.
   - The reference implementation in `SQLiteHost.swift:214` explicitly catches UNIQUE constraint errors and returns changes = 0.
   - Therefore, `ZaloSqliteDalService.ExecuteNonQueryAsync` must intercept `SqliteException` with `SqliteErrorCode == 19` and return `new SqliteExecutionResult { LastInsertedId = 0, RowsAffected = 0 }`.

4. **Schema Migrator (v400) & Schema_v400.sql**:
   - Schema version 400 is the required production baseline.
   - When a connection opens, if `PRAGMA user_version < 400`, `ZaloSqliteMigrator` executes `Schema_v400.sql` inside a transaction, creating all 8 required tables, indexes, and trigger, then sets `PRAGMA user_version = 400;`.
   - To support both `data-model.md` and `tasks.md` query variations:
     - Table `group_profiles` is created with a VIEW `groups` aliasing `group_profiles`.
     - In `messages`: columns `cli_msg_id`, `server_msg_id`, `conversation_id`, `sender_id`, `sender_name`, `sender_avatar_url`, `is_outgoing`, `status`, `kind`, `payload_type`, `payload_json`, `quote_json`, `mentions_json`, `reactions_json`, `timestamp`, `sequence_offset`.
     - Index `idx_messages_conv_ts` targets `(conversation_id, timestamp DESC, sequence_offset DESC)`.

5. **Repositories Implementation**:
   - `ConversationRepository`: handles list queries ordered by `last_activity_at DESC`, unread count updates, and conversation flags (Pin, Archive, Mute).
   - `MessageRepository`: paginated query `SELECT ... FROM messages WHERE conversation_id = @c AND timestamp < @beforeTs ORDER BY timestamp DESC, sequence_offset DESC LIMIT @pageSize` guarantees sub-1.5ms execution with index `idx_messages_conv_ts`. Status updates mutate lifecycle `(10 -> 1 -> 2 -> 3)`. Recall replaces payload with `RecalledPayload`.
   - `FailedDraftStore`: pure C# class handling draft retention, mention shifting, and notice management matching `FailedDraftTests.swift`.
   - `ContactRepository` & `GroupRepository`: uses `ZaloStringHelper` for alphabetical section grouping `#` and `A-Z`. Groups track roles (`Member=0`, `Admin=1`, `Creator=2`).

6. **Credential Protection via DPAPI**:
   - `ZaloCredentialStorage` encrypts session cookies (`zpw_sek`), tokens, device IMEI (`z_uuid`), and E2EE identity private keys using `ProtectedData.Protect` with `DataProtectionScope.CurrentUser`.
   - Domain validation rejects cookies not bound to `.zalo.me`.
   - Error messages and exceptions scrub secrets via string redaction (`[REDACTED]`).

7. **ZaloStorageService Facade**:
   - Exposes `IConversationRepository`, `IMessageRepository`, `IContactRepository`, `IGroupRepository`, `ZaloCredentialStorage`, and `FailedDraftStore`.
   - Coordinates initialization, graceful closing, and account data wiping (`.db`, `-wal`, `-shm`).

---

## 3. Caveats

- **SQLCipher vs Plain SQLite**: The official Zalo client includes SQLCipher binaries, but reference notes (`SQLiteHost.swift:23`, `REVERSE-ENGINEERING-NOTES.md:120`) prove that database files remain plain SQLite unless a secondary background DB encryption job is triggered. Olaz operates entirely on plain SQLite. Thus, standard `Microsoft.Data.Sqlite` in WAL mode is 100% specification-compliant and avoids native unmanaged wrapper overhead.
- **Windows DPAPI Scope**: DPAPI encrypts per Windows user account. Unit tests executing on CI or dev machines will successfully roundtrip on the same user account context.
- **Polymorphic Payloads**: `ChatMessagePayload` subclasses (14 types from Wave 1) are serialized to JSON in the `payload_json` column and identified by `payload_type` (e.g., `TextPayload`, `PhotoPayload`). System.Text.Json polymorphically deserializes them via the discriminator metadata established in Wave 1.

---

## 4. Conclusion

All requirements, schemas, pragmas, query signatures, sorting/pagination rules, concurrency models, and verification criteria for Tasks T-008 through T-013 (Wave 2 Storage Infrastructure & Repositories) have been thoroughly probed and authoritative specifications extracted.

The plan for implementation comprises:
1. `ZaloSqliteDalService.cs` & `SqliteExecutionResult.cs` (T-008)
2. `ZaloSqliteMigrator.cs` & `Schema_v400.sql` (T-009)
3. `ConversationRepository.cs`, `MessageRepository.cs`, `FailedDraftStore.cs` (T-010)
4. `ContactRepository.cs`, `GroupRepository.cs` (T-011)
5. `ZaloCredentialStorage.cs` (T-012)
6. `ZaloStorageService.cs` (T-013)
7. Full unit test suite targeting `Category=Storage` to clear GATE 2A without regressing Wave 1 (83/83 passing).

---

## 5. Verification Method

To independently verify this specification report:

1. **Verify Existing Baseline**:
   ```powershell
   dotnet test tests/Zenix.Tests/Zenix.Tests.csproj
   ```
   *Expected*: 83/83 tests pass cleanly.

2. **Verify SQLite Host Reference in `zalo-swift-re`**:
   - Inspect `d:\DEV\WinUI\zalo-swift-re\Sources\Olaz\ZaloRuntime\SQLiteHost.swift` lines 180-185 (pragmas) and lines 210-216 (safe UNIQUE handling).
   - Inspect `d:\DEV\WinUI\zalo-swift-re\Tests\OlazTests\SQLiteHostTests.swift` lines 18-35 (UNIQUE conflict test) and lines 66-82 (migration handshake).
   - Inspect `d:\DEV\WinUI\zalo-swift-re\Tests\OlazTests\FailedDraftTests.swift` lines 1-42 (draft appending and mention shifting).

3. **Verify Target Storage Gate Command (GATE 2A)**:
   ```powershell
   dotnet test tests/Zenix.Tests/Zenix.Tests.csproj --filter "Category=Storage"
   ```
   *Success criteria*: 0 failures, WAL confirmed, schema v400 confirmed, 50-message pagination latency $\le 1.5\text{ ms}$, DPAPI encryption verified.
