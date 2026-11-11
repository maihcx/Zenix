# Task Assignment: Storage Infrastructure & Local Repositories Spec Mining (Wave 2)

## Context
Wave 2 covers Storage Infrastructure & Local Repositories (Tasks T-008 through T-013, heading towards GATE 2A).

## Objective
Extract authoritative requirements, schemas, queries, and behaviors from specifications and reference implementations:
1. Examine `.specify/specs/data-model.md`, `.specify/specs/tasks.md` (T-008 to T-013), and `.specify/specs/contracts/service-interfaces.md`.
2. Inspect the reference repository `d:\DEV\WinUI\zalo-swift-re` for SQLite queries, database tables, indexes, schema versioning, sorting/pagination, Keychain/credentials, and SQLite pragmas.
3. Detail exact specifications for:
   - `ZaloSqliteDalService`: target path `%LocalAppData%\Zenix\sqlite\_production\<account>\chat_storage.db`, mandatory pragmas (`PRAGMA journal_mode = WAL`, `synchronous = NORMAL`, `busy_timeout = 5000`, `cache_size = -8000`, `foreign_keys = ON`), SemaphoreSlim connection leasing pool for write serialization & concurrent reads, safe execution of INSERT statements on UNIQUE conflict returning `{ LastInsertedId = 0, RowsAffected = 0 }`.
   - `ZaloSqliteMigrator`: `PRAGMA user_version = 400`, complete DDL for normalized tables (`conversations`, `messages`, `contacts`, `groups`, `group_members`, `drafts`, `keychain_keys`, `sync_checkpoints`) and indexes (`idx_messages_conv_ts (conv_id, timestamp DESC, offset DESC)`, `idx_messages_cli_id`, `idx_conv_updated`).
   - `ConversationRepository` & `MessageRepository`: paginated retrieval (50 messages $\le 1.5\text{ ms}$), stable tie-breaking by `(timestamp DESC, offset DESC)`, batch insert, delivery status transitions (10 -> 1 -> 2 -> 3), recall mutation to `.recalled`, draft persistence via `FailedDraftStore`.
   - `ContactRepository` & `GroupRepository`: contact upsert, friend list sync, alias updates, alphabetical section groupings (`#`, `A-Z`), group profile persistence, member role assignments (`Member`, `Admin`, `Creator`).
   - `ZaloCredentialStorage`: Windows DPAPI / AES-256-GCM via `PasswordEncryptor`, `.zalo.me` domain scoping, scrubbing credentials from error logs.
   - `ZaloStorageService`: facade coordinating all repositories and DAL.
   - GATE 2A verification criteria and test suite structure.

## Inputs
- `ORIGINAL_REQUEST.md`: `d:\DEV\WinUI\Zenix\.agents\teamwork\ORIGINAL_REQUEST.md`
- `PROJECT.md`: `d:\DEV\WinUI\Zenix\.agents\teamwork\orchestrator\PROJECT.md`
- Specs: `.specify/specs/data-model.md`, `.specify/specs/tasks.md`, `.specify/specs/contracts/service-interfaces.md`
- Reference implementation: `d:\DEV\WinUI\zalo-swift-re`

## Deliverables
Write your comprehensive specification report to:
`d:\DEV\WinUI\Zenix\.agents\teamwork\spec_miner_wave2_storage\handoff.md`
Update your `progress.md` with liveness timestamps.
Once finished, send a brief message to the orchestrator: "I'm done, check my report".


## 2026-11-11T08:03:21Z
Read d:\DEV\WinUI\Zenix\.agents\teamwork\spec_miner_wave2_storage\DISPATCH.md and execute your specification mining.
Working directory: d:\DEV\WinUI\Zenix
State folder: d:\DEV\WinUI\Zenix\.agents\teamwork\spec_miner_wave2_storage
Original request: d:\DEV\WinUI\Zenix\.agents\teamwork\ORIGINAL_REQUEST.md

Extract authoritative requirements and reference implementation details from .specify/specs/ and d:\DEV\WinUI\zalo-swift-re for Tasks T-008 through T-013 (SQLite DAL, pragmas, schema v400, repositories, DPAPI credential storage, facade). Write your handoff to d:\DEV\WinUI\Zenix\.agents\teamwork\spec_miner_wave2_storage\handoff.md and update progress.md. When complete, send a brief message to the orchestrator.
