# BRIEFING — 2026-11-11T08:22:30Z

## Mission
Extract authoritative requirements, cryptographic test vectors, mathematical algorithms, and wire formats from specifications and reference implementations for Wave 2 Tasks T-014 through T-018 (Cryptography Core & Signal Protocol E2EE).

## 🔒 My Identity
- Archetype: specification-miner
- Roles: Teamwork specialist, Cryptographic specification miner
- Working directory: d:\DEV\WinUI\Zenix
- State folder: d:\DEV\WinUI\Zenix\.agents\teamwork\spec_miner_wave2_crypto
- Original parent: 8f2b359c-f3ff-43a7-ad0b-7cd2933ec630
- Milestone: Wave 2 (Tasks T-014 through T-018 - GATE 2B)

## 🔒 Key Constraints
- Read-only: Do NOT implement anything — only discover, probe, and document.
- Prioritize authoritative sources (.specify/specs/, d:\DEV\WinUI\zalo-swift-re) over assumptions.
- Must document all discovered features and edge cases using required table formats.
- Windows CRLF line endings invariant.
- Write handoff to `d:\DEV\WinUI\Zenix\.agents\teamwork\spec_miner_wave2_crypto\handoff.md`.
- Keep heartbeat active in `progress.md`.

## Current Parent
- Conversation ID: 8f2b359c-f3ff-43a7-ad0b-7cd2933ec630
- Updated: 2026-11-11T08:22:30Z

## Task Summary
- **What to build**: Specification report and test vector dictionary for Tasks T-014 (ZaloCryptoService AES-256-CBC, Keychain HKDF/HMAC, XOR 147), T-015 (Curve25519 & X3DH Pre-Key Service), T-016 (Double Ratchet Cryptographic Engine), T-017 (Signal E2EE Service & Safety Number), T-018 (GATE 2B Cryptographic Verification Suite).
- **Success criteria**: Comprehensive handoff report with exact test vectors, key sizes, algorithms, wire formats, serialization schemas, edge cases, and verification commands.
- **Interface contracts**: `.specify/specs/contracts/service-interfaces.md`, `Zenix/Core/Zalo/Contracts/IZaloCryptoService.cs`
- **Code layout**: .specify/specs/plan.md, PROJECT.md

## Key Decisions Made
- Discovered and documented exact test vectors:
  - `EncryptZcid`: Key `"3FC4F0D2AB50057BCE0D90D9187A22B1"`, Input `"23,abc-imei,1792963077777"` -> `"a1a69db6a344da4d883865c637f979495dd86112a4534347b462259bfdeb641b"`.
  - `EncryptApiParams`: Key `"DE71B7D4C57C0749158CB90D5E431AE3"`, Input `"{\"language\":\"vi\"}"` -> `"V5p1S6tTwaE91W8UDCagn9vVP9wUQ3QzA04wG7xh+5E="`.
  - Keychain Envelope: `[0x49: 1B][IV: 16B][Salt: 16B][Ciphertext: NB][HMAC: 32B]` (65 bytes overhead), HKDF-SHA256, constant-time `FixedTimeEquals`.
  - Media Denoising: XOR 147 (`0x93`) targeting prefix signature `077a416c4f07` (`\a z A l O \a`).
  - X3DH Master Secret: `HKDF(0xFF*32 || DH1 || DH2 || DH3 [|| DH4], 0*32, "ZaloText")`.
  - Double Ratchet: `HMAC-SHA256` symmetric chains (0x01 messageKey, 0x02 nextChainKey), `HKDF(ECDHE, rootKey, "ZaloRatchet")`, message key derivation `HKDF(msgKey, 0*32, "ZaloMessageKeys")`, skipped keys store max 2000.
  - Signal E2EE Envelopes: Protobuf `WhisperMessage` and `PreKeyWhisperMessage` with wire version `0x33` and 8-byte constant-time HMAC tag.
  - 60-digit Safety Number: 5200 SHA-512 iterations, 6 5-digit chunks (40-bit big-endian % 100,000) per party, sorted and formatted as 12 groups of 5 decimal digits.

## Loaded Skills
- None explicitly assigned in task dispatch.

## Artifact Index
- DISPATCH.md — Task assignment and instructions
- BRIEFING.md — Persistent context & working memory
- progress.md — Liveness heartbeat and step tracking
- handoff.md — Comprehensive 5-component handoff report
