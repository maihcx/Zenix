# Remediation Explorer Dispatch: Double Ratchet State-Poisoning & Lookahead Fix Strategy

## Context
During Wave 2 verification, `challenger_wave2_2` uncovered two critical issues in `SignalDoubleRatchetService` and `SignalE2eeService`:
1. **Critical: Session Poisoning via Pre-Authentication State Mutation**:
   In `SignalE2eeService.DecryptWhisperWire`, calling `SignalDoubleRatchetService.Decrypt` steps the DH ratchet (`StepDhRatchet`) and advances symmetric counters *before* verifying the wire HMAC tag. A forged wire packet with an untrusted ephemeral key causes the receiver to mutate `RootKey`, `ReceivingChainKey`, `SendingChainKey`, and `LocalEphemeralKeyPair`. When HMAC verification subsequently fails, the session is not rolled back, causing permanent session poisoning and desynchronization.
2. **High: Unbounded Lookahead in `StepDhRatchet`**:
   `StepDhRatchet` lacks `MaxMessageKeys` (2000) bounds check on `remotePreviousCounter`, allowing memory exhaustion DoS via unbounded skipped key allocations.
3. **Line Endings**:
   `StorageStressChallengerTests.cs` and `AdversarialCryptoTests.cs` currently have LF line endings; need CRLF normalization.

## Objective
Analyze the code and propose the exact remediation strategy:
1. Examine `challenger_wave2_2/handoff.md`, `Zenix/Core/Zalo/Cryptography/SignalDoubleRatchetService.cs`, `DoubleRatchetSessionState.cs`, and `SignalE2eeService.cs`.
2. Propose how `SignalDoubleRatchetService` and `SignalE2eeService` can:
   - Provide an atomic / staged state transition: e.g. state clone or staging candidate state, deriving candidate message keys and verifying the wire HMAC *before* committing mutations to the live `DoubleRatchetSessionState`.
   - Ensure that if wire HMAC validation fails, the live session state remains completely untouched.
   - Enforce `MaxMessageKeys` (2000) check in `StepDhRatchet`:
     `if (remotePreviousCounter - session.ReceiveMessageCounter > MaxMessageKeys) throw new InvalidOperationException($"Over messages into the future: {MaxMessageKeys}");`
3. Propose tests in `AdversarialCryptoTests.cs` to prove:
   - Forged ephemeral packet fails HMAC, and subsequent legitimate message from sender still decrypts successfully (zero session poisoning!).
   - Large `previousCounter` (> 2000) in `StepDhRatchet` is immediately rejected before loop execution.
4. Do NOT implement the code; recommend the detailed fix strategy for the worker.

## Deliverables
Write your remediation strategy to:
`d:\DEV\WinUI\Zenix\.agents\teamwork\explorer_wave2_remediation\handoff.md`
Notify the orchestrator via `send_message`.


## 2026-11-11T09:22:10Z
Read d:\DEV\WinUI\Zenix\.agents\teamwork\explorer_wave2_remediation\DISPATCH.md and d:\DEV\WinUI\Zenix\.agents\teamwork\challenger_wave2_2\handoff.md.
Working directory: d:\DEV\WinUI\Zenix
State folder: d:\DEV\WinUI\Zenix\.agents\teamwork\explorer_wave2_remediation
Original request: d:\DEV\WinUI\Zenix\.agents\teamwork\ORIGINAL_REQUEST.md

Analyze the Double Ratchet state poisoning vulnerability and unbounded previousCounter lookahead in StepDhRatchet. Recommend an exact atomic / staged remediation strategy for SignalDoubleRatchetService and SignalE2eeService. Write handoff.md and notify the orchestrator.
