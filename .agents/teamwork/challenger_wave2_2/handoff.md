# Handoff Report: Cryptography & Signal Protocol Subsystem Adversarial Challenge (Wave 2)

**Agent**: `challenger_wave2_2`  
**Date**: 2026-11-11T09:21:00Z  
**Milestone**: Wave 2: Storage & Cryptography Core (GATE 2B)  
**State Folder**: `d:\DEV\WinUI\Zenix\.agents\teamwork\challenger_wave2_2`  
**Verdict**: `Verdict: REQUEST_CHANGES`  

---

## Challenge Summary

**Overall risk assessment**: **CRITICAL**

While primitives in `ZaloCryptoService` (AES-256-CBC, Keychain envelopes, XOR 147 media de-noising) and `SafetyNumberFormatter` pass empirical stress tests with high fidelity, the Double Ratchet and wire authentication implementation (`SignalDoubleRatchetService` and `SignalE2eeService`) suffers from a **Critical state-poisoning flaw** and a **High denial-of-service vulnerability**:

1. **CRITICAL — Irrevocable Session Poisoning via Pre-Authentication State Mutation**: When decrypting an incoming `WhisperMessage`, `SignalE2eeService.DecryptWhisperWire` invokes `SignalDoubleRatchetService.Decrypt`, which executes asymmetric DH ratcheting (`StepDhRatchet`) and advances symmetric counters *before* verifying the truncated 8-byte HMAC tag. An attacker sending a single unauthenticated wire payload with a forged ephemeral key causes the receiver to mutate its `RootKey`, `ReceivingChainKey`, `SendingChainKey`, and `LocalEphemeralKeyPair`. When the HMAC check subsequently fails, the session is not rolled back. The receiver is permanently desynchronized from the legitimate sender.
2. **HIGH — Unbounded Lookahead in `StepDhRatchet` (Memory Exhaustion DoS)**: In `SignalDoubleRatchetService.cs:241-250`, the `while (session.ReceiveMessageCounter < remotePreviousCounter)` loop lacks the `MaxMessageKeys` (2000) boundary check that protects the sequential receiving chain. An attacker supplying `previousCounter = 1,000,000` forces the receiver to calculate 1,000,000 HMAC iterations and allocate 1,000,000 skipped keys into memory without error.
3. **MEDIUM — Retransmission Rejection on Transport Tampering**: Within a sequential chain, `ReceiveMessageCounter` is incremented before HMAC validation. If an authentic message is bit-flipped in transit, the receiver rejects it with HMAC failure, but advances its receiving counter. When the sender retransmits the authentic message (with the same counter), the receiver permanently rejects it with `CryptographicException: Message counter X is older than current receiving counter`.

---

## Challenges

### [Critical] Challenge 1: Unauthenticated DH Ratchet Stepping & State Poisoning
- **Assumption challenged**: The worker assumed that performing constant-time HMAC check on the outer wire format in `SignalE2eeService` line 284 adequately protects against tampered payloads.
- **Attack scenario**: An attacker observes or injects a single `WhisperMessage` wire payload `[0x33: 1B] || [Protobuf: NB] || [BogusHMAC: 8B]` containing a freshly generated random Curve25519 ephemeral public key and garbage HMAC. The receiver evaluates `isNewEphemeral == true` (`SignalDoubleRatchetService.cs:184-190`) and calls `StepDhRatchet`. `StepDhRatchet` overwrites `session.RootKey`, `session.ReceivingChainKey`, `session.SendingChainKey`, and regenerates `session.LocalEphemeralKeyPair`. Subsequently, line 284 throws `CryptographicException: HMAC authentication failed for WhisperMessage`. Because no state rollback mechanism exists, Bob's session keys are permanently overwritten with garbage derived from the attacker's ephemeral key.
- **Blast radius**: Complete permanent denial of service for the conversation. Neither party can ever send or receive messages in that session again.
- **Mitigation**: Double Ratchet session state mutations must be staged/transactional. `StepDhRatchet` and symmetric chain key advances must either operate on a state clone or commit only after `CryptographicOperations.FixedTimeEquals` succeeds. Furthermore, `SignalDoubleRatchetService` must separate key derivation (`DeriveMessageKeys`) from AES decryption (`DecryptWithKey`) to enforce Encrypt-then-MAC discipline.

### [High] Challenge 2: Unbounded `previousCounter` Lookahead in `StepDhRatchet`
- **Assumption challenged**: The worker assumed `MaxMessageKeys = 2000` protects against all future gap jump exhaustion attacks.
- **Attack scenario**: An attacker provides a `WhisperMessage` with `previousCounter = 100_000` during an ephemeral ratchet step. In `SignalDoubleRatchetService.cs:241-250`:
  ```csharp
  while (session.ReceiveMessageCounter < remotePreviousCounter)
  {
      var skipKey = HMACSHA256.HashData(session.ReceivingChainKey, StepOne);
      ...
      session.SkippedMessageKeys[(oldEphemeralHex, session.ReceiveMessageCounter)] = (skipKey, DateTime.UtcNow);
      session.ReceiveMessageCounter++;
  }
  ```
  The loop executes 100,000 iterations without checking `remotePreviousCounter - session.ReceiveMessageCounter > MaxMessageKeys`.
- **Blast radius**: Massive CPU hang and memory exhaustion via unbounded dictionary allocations.
- **Mitigation**: Add guard before the while loop in `StepDhRatchet`:
  ```csharp
  if (remotePreviousCounter - session.ReceiveMessageCounter > MaxMessageKeys)
  {
      throw new InvalidOperationException($"Over messages into the future: {MaxMessageKeys}");
  }
  ```

---

## 1. Observation

Direct empirical evidence was gathered by authoring and executing 10 adversarial test fixtures in `tests/Zenix.Tests/Zalo/Crypto/AdversarialCryptoTests.cs`:

1. **Keychain HMAC Rejection and Pre-Decryption Abort**:
   - `ZaloCryptoService.Keychain.cs:103-118`: Verified constant-time HMAC-SHA256 verification using `CryptographicOperations.FixedTimeEquals` executes prior to initializing `Aes.Create()` or calling `decryptor.TransformFinalBlock`.
   - Single-bit flips at offset 5 (IV), offset 20 (salt), offset [^33] (ciphertext), and offset [^1] (HMAC tag) each throw `CryptographicException: HMAC-SHA256 authentication failed for keychain envelope. Data has been tampered with or corrupted.`
   - In particular, corrupting ciphertext blocks that would otherwise cause PKCS7 padding failure still yields the HMAC exception, proving pre-decryption abort.
2. **Truncated and Malformed Headers**:
   - `ZaloCryptoService.Keychain.cs:80-88`: Envelopes of lengths 0, 1, 16, 32, and 64 bytes throw `ArgumentException: Envelope length X is less than minimum required 65 bytes.`
   - Envelopes of length 65 with version bytes `0x00`, `0x48`, `0x50`, `0xFF` throw `ArgumentException: Invalid envelope version header byte: 0xXX, expected 0x49.`
3. **Out-of-Order Packet Delivery (Complex Sequence)**:
   - Evaluated packet delivery sequence: `[1, 2, 5, 3, 4, 10, 6, 7, 8, 9, 0]`.
   - All 11 packets decrypted to exact plaintext without loss.
   - `bobSession.SkippedMessageKeys` cached skipped keys during out-of-order arrival and reached exactly `0` (empty) once all packets arrived.
   - Replay attacks on delivered packets threw `CryptographicException`.
4. **Gap Jump Boundary & Protection in Sequential Chain**:
   - On a fresh receiving chain (`ReceiveMessageCounter = 0`), counter = 2001 (diff = 2001) threw `InvalidOperationException: Over messages into the future: 2000`. Memory remained untouched (`SkippedMessageKeys.Count == 0`).
   - On an established receiving chain (`ReceiveMessageCounter = 1`), counter = 2002 (diff = 2001) threw `InvalidOperationException`.
   - Jump of 1,000,000 and `uint.MaxValue` were immediately rejected without underflow or memory allocation.
5. **Session Poisoning Reproduction (Empirical Verification)**:
   - `Adversarial_ForgedEphemeralKeyPacket_ProbesPermanentSessionPoisoning`:
     - Alice and Bob establish session.
     - Attacker delivers forged packet with random Curve25519 ephemeral key and garbage HMAC tag.
     - Bob throws `CryptographicException` on HMAC failure.
     - Alice transmits legitimate message `legitimate_message_after_attack`.
     - Bob attempts to decrypt: throws `CryptographicException` because Bob's session keys were overwritten by the attacker's ephemeral key during `StepDhRatchet`.
6. **Unbounded `StepDhRatchet` PreviousCounter Reproduction (Empirical Verification)**:
   - `StepDhRatchet_LargePreviousCounter_ProbesLookaheadBound`:
     - Bob receives a stepped DH packet with `previousCounter = 2005` (> 2000).
     - Bob's `StepDhRatchet` executed the while loop 2004 times, creating 2004 skipped keys in `bobSession.SkippedMessageKeys`.
7. **Media De-noising Edge Cases**:
   - Exact 6-byte signature `077a416c4f07` with 0-byte payload returned empty `ReadOnlyMemory<byte>` (length 0).
   - Single byte `0x00` restored to `147`.
   - All 256 byte values roundtripped through `payload ^ 147`.
   - Short buffers (0, 1, 5 bytes) and near-miss signatures (`067a416c4f07`, `077a416c4f06`, zeroes) returned 100% untouched.
8. **Safety Number Symmetry**:
   - Evaluated `ComputeSafetyNumber(userA, keyA, userB, keyB)` vs `ComputeSafetyNumber(userB, keyB, userA, keyA)`: returned identical 60-digit string in 12 groups of 5 decimal digits.
   - 32-byte raw vs 33-byte `0x05`-prefixed keys produced identical Safety Numbers.
   - 1-bit flip in identity key produced a completely distinct Safety Number (avalanche effect).

---

## 2. Logic Chain

1. **Keychain & Media Robustness**:
   - Observations 1, 2, 7, and 8 verify that `ZaloCryptoService` meets all security requirements for local data encryption, envelope integrity, media de-noising, and Safety Number formatting.
2. **Double Ratchet Vulnerability**:
   - Observation 5 directly proves that `SignalDoubleRatchetService.Decrypt` executes state-modifying operations (DH ratchet stepping and sequence counter increments) before `SignalE2eeService` verifies the wire HMAC.
   - In cryptographic protocol engineering, unauthenticated state transitions allow trivial denial-of-service and state-desynchronization attacks. Because the wire HMAC tag is not validated prior to state mutation, any unauthorized third party can poison the session state.
   - Observation 6 proves that `remotePreviousCounter` in `StepDhRatchet` is unconstrained by `MaxMessageKeys`, enabling denial of service via memory exhaustion.

---

## 3. Caveats

- Testing was performed on the managed C# cryptographic implementations running under .NET 10 on Windows x64.
- Microarchitectural timing side-channels (e.g. CPU cache line timing) were not evaluated beyond verifying `CryptographicOperations.FixedTimeEquals` usage.

---

## 4. Conclusion

**Verdict: REQUEST_CHANGES**

The Cryptography subsystem has high structural quality and passes all functional parity vectors, but must NOT be approved for merge until the following two architectural fixes are implemented:

1. **Atomic Session State Rollback / MAC-First Validation in `SignalE2eeService`**:
   - Separate message key derivation from ciphertext decryption in `SignalDoubleRatchetService`.
   - Compute candidate ratchet keys and verify the wire HMAC tag *before* committing any mutations to `DoubleRatchetSessionState`.
   - If HMAC verification fails, discard candidate keys and leave `session` completely unmodified.
2. **Enforce `MaxMessageKeys` in `StepDhRatchet`**:
   - In `SignalDoubleRatchetService.cs:241`, add `if (remotePreviousCounter - session.ReceiveMessageCounter > MaxMessageKeys) throw new InvalidOperationException(...)` to prevent unbounded memory allocation during DH ratchet stepping.

---

## 5. Verification Method

1. **Run Full Adversarial and Regression Test Suite**:
   ```powershell
   dotnet test tests/Zenix.Tests/Zenix.Tests.csproj --filter "Category=Crypto"
   ```
   *Expected Result*: All 29 tests pass, including the 10 new adversarial stress tests in `AdversarialCryptoTests.cs`.

2. **Verify Full Solution Integrity**:
   ```powershell
   dotnet test Zenix.sln
   ```
   *Expected Result*: All 162 tests across the solution pass with 0 failures and 0 warnings.
