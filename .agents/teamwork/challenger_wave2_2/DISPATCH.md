# Challenger Dispatch: Cryptography & Signal Protocol Adversarial Challenger (Wave 2)

## Context
Wave 2 Cryptography and Signal Protocol subsystem is implemented. You are the empirical challenger tasked with adversarially probing the cryptographic engine against tampering, malicious payloads, and state corruption.

## Objectives & Adversarial Scenarios
1. Read `ORIGINAL_REQUEST.md`, `PROJECT.md`, and `worker_wave2_crypto/handoff.md`.
2. Empirically verify:
   - Tampered HMAC rejection: flip 1 bit in Keychain envelope or WhisperMessage wire format; assert constant-time HMAC check fails and aborts before decryption.
   - Truncated or malformed envelope headers: test envelopes < 65 bytes or with magic byte != 0x49; assert immediate safe rejection.
   - Out-of-order packet arrival with gap jumps: simulate packet arrival sequences (e.g. 1, 2, 5, 3, 4, 10, 6, 7, 8, 9) and verify all decrypt successfully via skipped message keys; verify that jumps > 2000 messages throw `MessageCounterError` and do not cause memory exhaustion.
   - Media de-noising edge cases: verify that buffers starting with `077a416c4f07` are restored via XOR 147, while buffers shorter than 6 bytes or without the signature are returned completely untouched.
   - Safety Number symmetry and resistance: verify that swapped identity keys produce the identical 60-digit number.
3. Run or write empirical adversarial tests.
4. Conclude with a clear verdict in `handoff.md`:
   - `Verdict: APPROVE` or `Verdict: REQUEST_CHANGES`.

## Deliverables
Write your challenger report to:
`d:\DEV\WinUI\Zenix\.agents\teamwork\challenger_wave2_2\handoff.md`
Notify the orchestrator via `send_message`.


## 2026-11-11T09:09:00Z
Read d:\DEV\WinUI\Zenix\.agents\teamwork\challenger_wave2_2\DISPATCH.md and execute your adversarial verification on the Cryptography & Signal subsystem.
Working directory: d:\DEV\WinUI\Zenix
State folder: d:\DEV\WinUI\Zenix\.agents\teamwork\challenger_wave2_2
Original request: d:\DEV\WinUI\Zenix\.agents\teamwork\ORIGINAL_REQUEST.md

Adversarially probe HMAC tampering, malformed envelopes, out-of-order packet sequences with gaps > 2000, media de-noising edge cases, and safety number symmetry. Deliver your verdict (APPROVE / REQUEST_CHANGES) in handoff.md and notify the orchestrator.
