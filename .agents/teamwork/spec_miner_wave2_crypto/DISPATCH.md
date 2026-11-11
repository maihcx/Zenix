# Task Assignment: Cryptography Core & Signal Protocol E2EE Spec Mining (Wave 2)

## Context
Wave 2 covers Cryptography Core & Signal Protocol E2EE (Tasks T-014 through T-018, heading towards GATE 2B).

## Objective
Extract authoritative requirements, cryptographic test vectors, mathematical algorithms, and wire formats from specifications and reference implementations:
1. Examine `.specify/specs/spec.md`, `.specify/specs/tasks.md` (T-014 to T-018), and `.specify/specs/contracts/service-interfaces.md`.
2. Inspect the reference repository `d:\DEV\WinUI\zalo-swift-re` (especially `RuntimeTests.swift`, crypto implementations in Swift, Signal protocol modules, test vectors):
   - Fixed keys, IVs, padding, and output formats for `EncryptZcid` (key `"3FC4F0D2AB50057BCE0D90D9187A22B1"`, zero IV 16B, PKCS7, lowercase hex) and `EncryptApiParams` (key `"DE71B7D4C57C0749158CB90D5E431AE3"`, zero IV, PKCS7, standard Base64).
   - Trusted device linking Keychain envelope packing/unpacking format: `[version: 1B = 0x49][iv: 16B][salt: 16B][ciphertext: NB][hmac: 32B]`, HKDF-SHA256 (`info = "ZaloDeviceLink"`), constant-time HMAC-SHA256 verification via `CryptographicOperations.FixedTimeEquals`.
   - Media De-noising: byte-wise XOR 147 against payload with anti-scraping signature `077a416c4f07`.
   - Curve25519 & X3DH Pre-Key Service: Montgomery ladder key agreements, identity keys, signed prekeys with Curve25519 signatures, batches of one-time prekeys, master shared secret `SK = KDF(DH1 || DH2 || DH3 || DH4)`.
   - Double Ratchet cryptographic engine: symmetric KDF chains (send/receive) + asymmetric DH ratchet steps, skipped message keys table with TTL expiration for out-of-order packet decryption.
   - Signal E2EE Service: Type 3 (`PreKeyWhisperMessage`) and Type 1 (`WhisperMessage`) envelopes, 60-digit Safety Number calculation (sort user identity keys, SHA-512 iteration loop, 32-bit big-endian modulo 100,000, 12 groups of 5 decimal digits).
   - GATE 2B verification criteria and exact test vectors.

## Inputs
- `ORIGINAL_REQUEST.md`: `d:\DEV\WinUI\Zenix\.agents\teamwork\ORIGINAL_REQUEST.md`
- `PROJECT.md`: `d:\DEV\WinUI\Zenix\.agents\teamwork\orchestrator\PROJECT.md`
- Specs: `.specify/specs/spec.md`, `.specify/specs/tasks.md`, `.specify/specs/contracts/service-interfaces.md`
- Reference implementation: `d:\DEV\WinUI\zalo-swift-re` (including test vectors)

## Deliverables
Write your comprehensive cryptographic specification report to:
`d:\DEV\WinUI\Zenix\.agents\teamwork\spec_miner_wave2_crypto\handoff.md`
Update your `progress.md` with liveness timestamps.
Once finished, send a brief message to the orchestrator: "I'm done, check my report".


## 2026-11-11T08:03:28Z
From: 8f2b359c-f3ff-43a7-ad0b-7cd2933ec630
Read d:\DEV\WinUI\Zenix\.agents\teamwork\spec_miner_wave2_crypto\DISPATCH.md and execute your cryptographic specification mining.
Working directory: d:\DEV\WinUI\Zenix
State folder: d:\DEV\WinUI\Zenix\.agents\teamwork\spec_miner_wave2_crypto
Original request: d:\DEV\WinUI\Zenix\.agents\teamwork\ORIGINAL_REQUEST.md

Extract authoritative requirements, cryptographic test vectors, and reference implementation details from .specify/specs/ and d:\DEV\WinUI\zalo-swift-re for Tasks T-014 through T-018 (AES-256-CBC, Keychain HKDF/HMAC envelope, XOR 147 media de-noising, Curve25519/X3DH pre-keys, Double Ratchet, 60-digit Safety Number). Write your handoff to d:\DEV\WinUI\Zenix\.agents\teamwork\spec_miner_wave2_crypto\handoff.md and update progress.md. When complete, send a brief message to the orchestrator.
