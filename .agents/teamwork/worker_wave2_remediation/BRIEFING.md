# BRIEFING — 2026-11-11T09:35:00Z

## Mission
Implement atomic Double Ratchet state transitions (Clone/CommitTo), pre-authentication wire HMAC validation, MaxMessageKeys lookahead guard in StepDhRatchet, update AdversarialCryptoTests.cs, and normalize CRLF line endings.

## 🔒 My Identity
- Archetype: worker
- Roles: implementer, qa, specialist
- Working directory: d:\DEV\WinUI\Zenix
- State folder: d:\DEV\WinUI\Zenix\.agents\teamwork\worker_wave2_remediation
- Original parent: 8f2b359c-f3ff-43a7-ad0b-7cd2933ec630
- Milestone: Wave 2 Remediation: Double Ratchet State-Poisoning & Lookahead Fix

## 🔒 Key Constraints
- DO NOT CHEAT. All implementations must be genuine.
- Zero hardcoded test results, facade implementations, or circumventing tests.
- Constant-time HMAC comparison via CryptographicOperations.FixedTimeEquals.
- Secure memory zeroing via CryptographicOperations.ZeroMemory.
- Roslyn IDE0065 clean: all using directives outside and above namespace.
- Windows CRLF line endings on all touched files.
- Zero UI dependencies.

## Current Parent
- Conversation ID: 8f2b359c-f3ff-43a7-ad0b-7cd2933ec630
- Updated: not yet

## Task Summary
- **What to build**: Double Ratchet transactional Clone/CommitTo, wire HMAC pre-verification, bounded lookahead in StepDhRatchet, AdversarialCryptoTests positive assertions, CRLF normalization
- **Success criteria**: dotnet build Zenix.sln 0 errors/0 warnings, all 29 Crypto tests pass, all 162 solution tests pass, 100% CRLF
- **Interface contracts**: .specify/specs/contracts/service-interfaces.md
- **Code layout**: Zenix/Core/Zalo/Cryptography, tests/Zenix.Tests/Zalo/

## Key Decisions Made
- Use transactional Clone/CommitTo pattern for candidate DoubleRatchetSessionState
- Enforce Encrypt-then-MAC discipline by splitting DeriveMessageKeys from DecryptCiphertext
- Bound remotePreviousCounter in StepDhRatchet against MaxMessageKeys (2000)

## Artifact Index
- d:\DEV\WinUI\Zenix\.agents\teamwork\worker_wave2_remediation\DISPATCH.md — Dispatch instructions
- d:\DEV\WinUI\Zenix\.agents\teamwork\worker_wave2_remediation\progress.md — Liveness heartbeat
- d:\DEV\WinUI\Zenix\.agents\teamwork\worker_wave2_remediation\handoff.md — Final deliverable report

## Change Tracker
- **Files modified**: None yet
- **Build status**: Untested
- **Pending issues**: None

## Quality Status
- **Build/test result**: Untested
- **Lint status**: Clean
- **Tests added/modified**: AdversarialCryptoTests.cs

## Loaded Skills
- None
