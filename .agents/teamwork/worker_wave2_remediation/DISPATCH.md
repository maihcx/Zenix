# Worker Dispatch: Double Ratchet State-Poisoning & Lookahead Remediation (Wave 2, Iteration 2)

## Mandatory Integrity Warning
DO NOT CHEAT. All implementations must be genuine. DO NOT hardcode test results, create dummy/facade implementations, or circumvent the intended task. A teamwork_auditor will independently verify your work. Integrity violations WILL be detected and your work WILL be rejected.

## Context
You are the Remediation Worker for Wave 2. You will implement the atomic state transition, wire HMAC pre-verification, lookahead bounds enforcement, and CRLF line ending normalization per the architecture designed by `explorer_wave2_remediation`.

## Authoritative Inputs to Read
1. Remediation strategy: `d:\DEV\WinUI\Zenix\.agents\teamwork\explorer_wave2_remediation\handoff.md`
2. Challenger report: `d:\DEV\WinUI\Zenix\.agents\teamwork\challenger_wave2_2\handoff.md`
3. Project Plan: `d:\DEV\WinUI\Zenix\.agents\teamwork\orchestrator\PROJECT.md`
4. Original Request: `d:\DEV\WinUI\Zenix\.agents\teamwork\ORIGINAL_REQUEST.md`

## Target Implementation Files
1. `Zenix/Core/Zalo/Cryptography/DoubleRatchetSessionState.cs`:
   - Implement `DoubleRatchetSessionState Clone()` deep-copying keys and `SkippedMessageKeys`.
   - Implement `void CommitTo(DoubleRatchetSessionState target)` zeroing target's superseded keys with `CryptographicOperations.ZeroMemory`, transferring buffers to target, and detaching sensitive buffers from the candidate clone.
2. `Zenix/Core/Zalo/Cryptography/SignalDoubleRatchetService.cs`:
   - In `StepDhRatchet`, add:
     ```csharp
     if (remotePreviousCounter > session.ReceiveMessageCounter &&
         remotePreviousCounter - session.ReceiveMessageCounter > MaxMessageKeys)
     {
         throw new InvalidOperationException($"Over messages into the future: {MaxMessageKeys}");
     }
     ```
   - Refactor `Decrypt` into:
     - `public (byte[] EncKey, byte[] MacKey, byte[] Iv) DeriveMessageKeys(...)`: Advances ratchet state, caches skipped keys, derives message keys without calling AES.
     - `public byte[] DecryptCiphertext(...)`: AES-256-CBC decryption with PKCS7 padding.
     - Keep `public (byte[] Plaintext, byte[] MacKey) Decrypt(...)` as a convenience method for backward compatibility.
3. `Zenix/Core/Zalo/Cryptography/SignalE2eeService.cs`:
   - In `DecryptWhisperWire`:
     - Wrap in `lock (session)`.
     - Use `using var candidate = session.Clone()`.
     - Call `ratchetService.DeriveMessageKeys(candidate, ...)`.
     - Compute and verify truncated 8-byte wire HMAC tag using `CryptographicOperations.FixedTimeEquals` BEFORE committing state and BEFORE calling AES.
     - If HMAC matches: call `candidate.CommitTo(session)`, then decrypt ciphertext with `ratchetService.DecryptCiphertext`.
     - If HMAC fails: throws `CryptographicException`. Candidate clone is disposed and zeroed; live `session` remains 100% untouched.
   - In `DecryptAsync` (Type 3 PreKeyWhisperMessage):
     - Call `DecryptWhisperWire(innerWhisperWire, session)` *before* inserting `_sessions[sessionKey] = session`.
4. `tests/Zenix.Tests/Zalo/Crypto/AdversarialCryptoTests.cs`:
   - Update tests per Section 4.4 of `explorer_wave2_remediation/handoff.md`:
     - `Adversarial_ForgedEphemeralKeyPacket_DoesNotPoisonSessionState`: asserts forged packet fails HMAC and Alice's subsequent message still decrypts cleanly.
     - `StepDhRatchet_LargePreviousCounter_ThrowsInvalidOperationExceptionAndProtectsMemory`: asserts lookahead jump > 2000 throws `InvalidOperationException` and memory remains empty.
     - `Adversarial_TamperedPacketDoesNotAdvanceCounter_AllowsLegitimateRetransmission`: asserts retransmission after in-transit corruption succeeds.
5. Line Endings Normalization:
   - Ensure `tests/Zenix.Tests/Zalo/Storage/StorageStressChallengerTests.cs` and `tests/Zenix.Tests/Zalo/Crypto/AdversarialCryptoTests.cs` use Windows CRLF (`\r\n`).

## Technical Invariants
- Roslyn IDE0065 clean: all `using` directives outside and above namespace.
- Windows CRLF line endings on all touched files.
- Zero UI dependencies.
- Constant-time HMAC comparison via `CryptographicOperations.FixedTimeEquals`.
- Zero memory leakage via `CryptographicOperations.ZeroMemory`.
- Verification commands:
  - `dotnet build Zenix.sln -c Debug` -> Must succeed with 0 errors and 0 warnings.
  - `dotnet test tests/Zenix.Tests/Zenix.Tests.csproj --filter "Category=Crypto"` -> All 29 tests pass.
  - `dotnet test Zenix.sln` -> All 162 tests pass (0 regressions).

## Deliverables
Write your implementation report to:
`d:\DEV\WinUI\Zenix\.agents\teamwork\worker_wave2_remediation\handoff.md`
Notify the orchestrator via `send_message`.


## 2026-11-11T09:33:36Z
Read d:\DEV\WinUI\Zenix\.agents\teamwork\worker_wave2_remediation\DISPATCH.md and d:\DEV\WinUI\Zenix\.agents\teamwork\explorer_wave2_remediation\handoff.md.
Working directory: d:\DEV\WinUI\Zenix
State folder: d:\DEV\WinUI\Zenix\.agents\teamwork\worker_wave2_remediation
Original request: d:\DEV\WinUI\Zenix\.agents\teamwork\ORIGINAL_REQUEST.md

MANDATORY INTEGRITY WARNING:
DO NOT CHEAT. All implementations must be genuine. DO NOT hardcode test results, create dummy/facade implementations, or circumvent the intended task. A teamwork_auditor will independently verify your work. Integrity violations WILL be detected and your work WILL be rejected.

Implement atomic Double Ratchet state transitions (Clone/CommitTo), pre-authentication wire HMAC validation, MaxMessageKeys lookahead guard in StepDhRatchet, update AdversarialCryptoTests.cs, and normalize CRLF line endings. Verify with dotnet build and dotnet test Zenix.sln. Write handoff.md and notify the orchestrator.
