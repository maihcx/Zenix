# Worker Dispatch: Storage Infrastructure & Local Repositories (Wave 2, Tasks T-008..T-013)

## Mandatory Integrity Warning
DO NOT CHEAT. All implementations must be genuine. DO NOT hardcode test results, create dummy/facade implementations, or circumvent the intended task. A teamwork_auditor will independently verify your work. Integrity violations WILL be detected and your work WILL be rejected.

## Context
You are the Storage Worker for Wave 2: Storage & Cryptography Core. Your mission is to implement the entire Storage infrastructure (Tasks T-008 through T-013) and its comprehensive unit test suite in `tests/Zenix.Tests/Zalo/Storage/`, targeting GATE 2A verification.

## Authoritative Inputs to Read
1. Specification report: `d:\DEV\WinUI\Zenix\.agents\teamwork\spec_miner_wave2_storage\handoff.md`
2. Baseline survey: `d:\DEV\WinUI\Zenix\.agents\teamwork\explorer_wave2_1\handoff.md`
3. Project Plan & Layout: `d:\DEV\WinUI\Zenix\.agents\teamwork\orchestrator\PROJECT.md`
4. Original Request: `d:\DEV\WinUI\Zenix\.agents\teamwork\ORIGINAL_REQUEST.md`
5. Constitution: `d:\DEV\WinUI\Zenix\.specify\memory\constitution.md`
6. Reference implementation:
   - `d:\DEV\WinUI\zalo-swift-re\Sources\Olaz\ZaloRuntime\SQLiteHost.swift`
   - `d:\DEV\WinUI\zalo-swift-re\Tests\OlazTests\SQLiteHostTests.swift`
   - `d:\DEV\WinUI\zalo-swift-re\Tests\OlazTests\FailedDraftTests.swift`

## Target Implementation Files (Strict Ownership)
You have exclusive write ownership of the following files:
1. `Zenix/Core/Zalo/Storage/SqliteExecutionResult.cs`:
   - Properties: `long LastInsertedId`, `int RowsAffected`.
2. `Zenix/Core/Zalo/Storage/ZaloSqliteDalService.cs`:
   - Connection factory targeting `%LocalAppData%\Zenix\sqlite\_production\<account>\chat_storage.db` and supporting in-memory mode (`:memory:`).
   - Mandatory pragmas on connection open: `PRAGMA journal_mode = WAL; PRAGMA synchronous = NORMAL; PRAGMA busy_timeout = 5000; PRAGMA cache_size = -8000; PRAGMA foreign_keys = ON;`.
   - Concurrency: Write serialization via `SemaphoreSlim(1,1)` and concurrent non-blocking read connection leases.
   - Safe UNIQUE conflict handling: Catches `SqliteException` with `SqliteErrorCode == 19` (UNIQUE constraint failed) returning `new SqliteExecutionResult { LastInsertedId = 0, RowsAffected = 0 }` instead of throwing fatal exceptions.
3. `Zenix/Core/Zalo/Storage/ZaloSqliteMigrator.cs`:
   - Reads `PRAGMA user_version`. If `< 400`, executes `Schema_v400.sql` inside a transaction and sets `PRAGMA user_version = 400;`.
4. `Zenix/Core/Zalo/Storage/Scripts/Schema_v400.sql`:
   - Normalized tables: `conversations`, `messages`, `contacts`, `group_profiles` (with a VIEW `groups` aliasing `group_profiles`), `group_members`, `drafts`, `keychain_keys`, `sync_checkpoints`.
   - Composite indexes: `idx_messages_conv_ts (conversation_id, timestamp DESC, sequence_offset DESC)`, `idx_messages_cli_id (cli_msg_id)`, `idx_conv_updated (last_activity_at DESC)`, `idx_contacts_sort (sort_key)`.
   - Trigger: `trg_update_conversation_on_new_message` updating `conversations.last_activity_at` and unread count.
5. `Zenix/Core/Zalo/Storage/Repositories/ConversationRepository.cs`:
   - Implements `IConversationRepository` from `IZaloStorageService.cs`. Thread metadata, unread count, pin/archive flags, ordered by `last_activity_at DESC`.
6. `Zenix/Core/Zalo/Storage/Repositories/MessageRepository.cs`:
   - Implements `IMessageRepository` from `IZaloStorageService.cs`.
   - Paginated message retrieval: 50 messages $\le 1.5\text{ ms}$, stable tie-breaking `ORDER BY timestamp DESC, sequence_offset DESC`.
   - Batch insert with parameterized statements.
   - Delivery status transitions (10 -> 1 -> 2 -> 3).
   - Message recall mutating payload to `RecalledPayload` preserving chronological slot.
7. `Zenix/Core/Zalo/Storage/Repositories/FailedDraftStore.cs`:
   - In-memory & disk draft storage. Handles draft appending, mention offset shifting (`firstText.Length + 1`), notice tracking matching `FailedDraftTests.swift`.
8. `Zenix/Core/Zalo/Storage/Repositories/ContactRepository.cs`:
   - Implements `IContactRepository` from `IZaloStorageService.cs`. Contact upsert, friend list sync, alias updates, alphabetical section groupings (`#`, `A-Z`) using `ZaloStringHelper`.
9. `Zenix/Core/Zalo/Storage/Repositories/GroupRepository.cs`:
   - Group profile persistence and member administration with roles (`Member=0`, `Admin=1`, `Creator=2`).
10. `Zenix/Core/Zalo/Storage/ZaloCredentialStorage.cs`:
    - Session cookie (`zpw_sek`), tokens, device IMEI, and E2EE identity private keys protected via Windows DPAPI (`ProtectedData.Protect` / `Unprotect`, `DataProtectionScope.CurrentUser`).
    - Enforces `.zalo.me` domain scoping on session cookies.
    - Scrubs credentials from error logs and exception messages (`[REDACTED]`).
11. `Zenix/Core/Zalo/Storage/ZaloStorageService.cs`:
    - Implements `IZaloStorageService` facade coordinating DAL, Migrator, Repositories, Credential Storage, and account data wipe (`WipeAccountDataAsync` deleting `.db`, `-wal`, `-shm`).

## Target Unit Test Files
Create the comprehensive test suite in `tests/Zenix.Tests/Zalo/Storage/` with `[Trait("Category", "Storage")]`:
- `tests/Zenix.Tests/Zalo/Storage/ZaloSqliteDalTests.cs` (WAL check, pragmas, Semaphore concurrency, UNIQUE constraint safe return)
- `tests/Zenix.Tests/Zalo/Storage/SqliteMigrationTests.cs` (Clean migration to v400, table/index verification via sqlite_master, idempotency)
- `tests/Zenix.Tests/Zalo/Storage/MessageRepositoryTests.cs` (Pagination $\le 1.5\text{ ms}$, sort stability under identical timestamp, status transitions, recall, batch inserts)
- `tests/Zenix.Tests/Zalo/Storage/ContactRepositoryTests.cs` (Contact upsert, Vietnamese collation `#` and `A-Z`, alias search, group member roles)
- `tests/Zenix.Tests/Zalo/Storage/CredentialStorageTests.cs` (DPAPI protect/unprotect, domain scoping rejection, log scrubbing)
- `tests/Zenix.Tests/Zalo/Storage/ZaloStorageServiceTests.cs` (Facade lifecycle, initialization, full repository sync, account wipe)

## Technical Invariants & Rules
- **CRLF Line Endings**: All files must use Windows CRLF (`\r\n`).
- **Roslyn IDE0065**: All `using` directives must strictly be outside and above namespace declarations.
- **Zero UI Dependencies**: Absolutely zero references to `Microsoft.UI.Xaml.*`, `UIElement`, `Window`, etc.
- **Async Methods**: 100% of asynchronous methods must accept `CancellationToken cancellationToken = default`.
- **English Documentation**: 100% of public types, constructors, methods, properties must have XML doc comments (`/// <summary>`) in technical American English.
- **Verification Commands**:
  - `dotnet build Zenix.sln -c Debug` -> Must succeed with 0 errors and 0 warnings.
  - `dotnet test tests/Zenix.Tests/Zenix.Tests.csproj --filter "Category=Storage"` -> All storage tests pass.
  - `dotnet test Zenix.sln` -> All existing 83 tests + all new storage tests must pass without any regression.

## Deliverable
Write your implementation changes report and verification results to:
`d:\DEV\WinUI\Zenix\.agents\teamwork\worker_wave2_storage\handoff.md`
Update your `progress.md` with liveness timestamps.
Once finished, send a brief message to the orchestrator: "I'm done, check my report".


## 2026-11-11T08:25:48Z
Read d:\DEV\WinUI\Zenix\.agents\teamwork\worker_wave2_storage\DISPATCH.md and implement the Storage subsystem and unit tests for Wave 2 (Tasks T-008 to T-013).
Working directory: d:\DEV\WinUI\Zenix
State folder: d:\DEV\WinUI\Zenix\.agents\teamwork\worker_wave2_storage
Original request: d:\DEV\WinUI\Zenix\.agents\teamwork\ORIGINAL_REQUEST.md
