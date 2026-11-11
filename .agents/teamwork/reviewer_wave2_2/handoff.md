# Quality Review & Adversarial Challenge Report: Cryptography & Signal Protocol Subsystem (Tasks T-014..T-018)

**Reviewer / Critic**: `reviewer_wave2_2`  
**Date**: 2026-11-11T09:16:30Z  
**Milestone**: Wave 2: Storage & Cryptography Core (GATE 2B)  
**State Folder**: `d:\DEV\WinUI\Zenix\.agents\teamwork\reviewer_wave2_2`  
**Verdict**: **APPROVE**

---

## 1. Observation

### 1.1 Source Files Inspected
Independent visual and static analysis was performed on all 10 source files and 4 test files:

1. `Zenix/Core/Zalo/Contracts/ISignalE2eeService.cs` (110 lines):
   - Defines `ISignalE2eeService`, `SignalMessageEnvelope` (with `IsPreKeyMessage`), and `SignalPreKeyBundle`.
   - 100% of asynchronous methods accept `CancellationToken cancellationToken = default`.
   - All `using` directives are outside/above `namespace` (Roslyn `IDE0065`).
   - Zero UI references (`Microsoft.UI.Xaml.*`, `UIElement`, `Window`).
   - Passes all 7 architectural reflection tests in `ContractsArchitectureTests.cs`.

2. `Zenix/Core/Zalo/Cryptography/ZaloCryptoService.cs` (100 lines):
   - Implements `IZaloCryptoService`.
   - `EncryptZcid`: AES-256-CBC, fixed key `"3FC4F0D2AB50057BCE0D90D9187A22B1"`, zero IV (16B), lowercase hex output.
   - `EncryptApiParams`: AES-256-CBC, fixed key `"DE71B7D4C57C0749158CB90D5E431AE3"`, zero IV (16B), standard Base64 output.
   - `DecryptApiParams`: AES-256-CBC decryption back to UTF-8 JSON plaintext.
   - Genuine cryptographic primitives utilizing `System.Security.Cryptography.Aes` with hardware acceleration.

3. `Zenix/Core/Zalo/Cryptography/ZaloCryptoService.Keychain.cs` (136 lines):
   - Wire layout: `[0x49: 1B][IV: 16B][Salt: 16B][Ciphertext: NB][HMAC: 32B]` (min length: 65 bytes).
   - Key derivation: HKDF-SHA256 (64 bytes derived keys: 32B AES key + 32B HMAC key) with primary info `"ZaloDeviceLink"` and fallback info `"ZprotocolSecureKDF"`.
   - Pre-decryption constant-time HMAC verification strictly enforced: `CryptographicOperations.FixedTimeEquals(computedHmac, envelopeHmac)` called at lines 104 and 113 before `aes.CreateDecryptor()`.
   - Secret key zeroing: `CryptographicOperations.ZeroMemory(derivedKeys)` executed in `finally` blocks at lines 68 and 133.

4. `Zenix/Core/Zalo/Cryptography/MediaDenoiseHelper.cs` (52 lines):
   - Inspects 6-byte prefix `0x07, 0x7A, 0x41, 0x6C, 0x4F, 0x07` (`077a416c4f07`).
   - Strips prefix and reverses byte-wise XOR 147 (`0x93`) to recover clean media streams (JPEG, PNG, MP4).
   - Buffers without signature or shorter than 6 bytes are returned untouched without out-of-bounds exceptions.

5. `Zenix/Core/Zalo/Cryptography/Curve25519Helper.cs` (484 lines):
   - Implements genuine Montgomery ladder scalar multiplication on Curve25519 ($u = 9$, RFC 7748) via `ScalarMultMontgomery` using `BigInteger` arithmetic over prime $2^{255} - 19$.
   - Bit clamping per RFC 7748: `priv[0] &= 248; priv[31] &= 127; priv[31] |= 64;` (lines 119-121, 146-148).
   - Seamlessly handles both 32-byte raw and 33-byte `0x05`-prefixed public keys.
   - XEdDSA signature generation (`Sign`) and verification (`VerifySignature`) using Edwards curve arithmetic, point compression/decompression, and SHA-512 challenges.

6. `Zenix/Core/Zalo/Cryptography/SignalX3dhService.cs` (258 lines):
   - Manages user identities, 14-bit registration IDs (`RandomNumberGenerator.GetInt32(0, 16384)`), signed prekeys with 64-byte signatures, and one-time prekey pools.
   - In `InitiateHandshake`: Verifies Bob's signed prekey signature against Bob's identity key via `Curve25519Helper.VerifySignature` prior to computing DH agreements. Throws `CryptographicException` on invalid signatures.
   - Master secret derivation: $SK = \text{HKDF-SHA256}(0xFF_{32} \,||\, DH_1 \,||\, DH_2 \,||\, DH_3 \,[||\, DH_4], \text{salt}=0_{32}, \text{info}=\text{"ZaloText"})$.
   - Zero memory leakage: `CryptographicOperations.ZeroMemory` wipes `u`, `derived`, `dh1`, `dh2`, `dh3`, and `dh4` in a `finally` block (lines 247-256).

7. `Zenix/Core/Zalo/Cryptography/DoubleRatchetSessionState.cs` (126 lines):
   - Implements `IDisposable`.
   - Tracks `RootKey`, `SendingChainKey`, `ReceivingChainKey`, `LocalEphemeralKeyPair`, `RemoteEphemeralPublicKey`, counters, and `SkippedMessageKeys` store.
   - Memory zeroing: `PruneExpiredSkippedKeys` wipes expired keys with `CryptographicOperations.ZeroMemory`. `Dispose()` wipes `RootKey`, `SendingChainKey`, `ReceivingChainKey`, `LocalEphemeralKeyPair.PrivateKey`, and all entries in `SkippedMessageKeys`.

8. `Zenix/Core/Zalo/Cryptography/SignalDoubleRatchetService.cs` (319 lines):
   - Symmetric chain advancing: `messageKey = HMAC(chainKey, [0x01])`, `nextChainKey = HMAC(chainKey, [0x02])`.
   - Asymmetric DH stepping: `HKDF(ECDHE, rootKey, "ZaloRatchet")` for receiving and sending chains.
   - Message key expansion: `HKDF(messageKey, zeroes_32, "ZaloMessageKeys")` deriving 32B AES key + 32B MAC key + 16B IV.
   - DoS lookahead bound: Rejects jumps greater than `MaxMessageKeys = 2000` into the future (`InvalidOperationException`).
   - Out-of-order packet caching: Stores skipped keys with timestamps. Consumed keys are removed from the cache upon decryption, preventing packet replay attacks.
   - Memory zeroing: Wipes intermediate DH secrets, message keys, and expanded key buffers in `finally` blocks.

9. `Zenix/Core/Zalo/Cryptography/SafetyNumberFormatter.cs` (150 lines):
   - Mutual 60-digit numeric fingerprinting conforming to Signal Protocol specification.
   - Executes 5,200 rounds of SHA-512 over normalized 33-byte `0x05`-prefixed identity keys and user IDs.
   - Extracts 6 chunks of 5 bytes (40-bit big-endian integers), applies modulo 100,000, and formats as 5-digit zero-padded numbers (30 digits per party).
   - Symmetrically sorts both 30-digit strings and formats into 12 space-separated blocks of 5 decimal digits.

10. `Zenix/Core/Zalo/Cryptography/SignalE2eeService.cs` (512 lines):
    - Implements `ISignalE2eeService`.
    - Coordinates X3DH session initialization and Double Ratchet multi-turn exchanges.
    - Wire framing: Type 3 (`PreKeyWhisperMessage`) and Type 1 (`WhisperMessage`) with version byte `0x33`.
    - Wire MAC verification: Computes truncated 8-byte HMAC tag and verifies in constant time via `CryptographicOperations.FixedTimeEquals(computedTag, wireHmac)` at line 284.
    - Lightweight embedded Protobuf serializer/deserializer matching the wire format.

### 1.2 Verification Commands & Live Execution Results

1. **Compilation Check**:
   Command: `dotnet build Zenix.sln -c Debug`
   Output:
   ```text
   Build succeeded.
       0 Warning(s)
       0 Error(s)
   Time Elapsed 00:00:01.34
   ```

2. **GATE 2B Cryptography Unit Test Suite**:
   Command: `dotnet test tests/Zenix.Tests/Zenix.Tests.csproj --filter "Category=Crypto"`
   Output:
   ```text
   Passed!  - Failed:     0, Passed:    11, Skipped:     0, Total:    11, Duration: 1 s - Zenix.Tests.dll (net10.0)
   ```

3. **Entire Solution Regression Check**:
   Command: `dotnet test Zenix.sln`
   Output:
   ```text
   Passed!  - Failed:     0, Passed:   138, Skipped:     0, Total:   138, Duration: 2 s - Zenix.Tests.dll (net10.0)
   ```

4. **Line Endings Verification**:
   Command: Scanned all 14 files for bare `\n` or mixed line endings.
   Result: `AllCRLF=True` for 100% of target source and test files.

5. **Prohibited UI Types Scan**:
   Command: Scanned for `Microsoft.UI.Xaml`, `UIElement`, `Window`, `FrameworkElement`.
   Result: 0 prohibited UI occurrences in `Zenix/Core/Zalo/Cryptography/*.cs` and `ISignalE2eeService.cs`.

6. **XML Documentation Verification**:
   Command: Built with `/p:GenerateDocumentationFile=true`.
   Result: 0 warnings in all cryptography and contract files.

---

## 2. Logic Chain

1. **Parity and Authenticity Verification**:
   - `EncryptZcid(23, "abc-imei", 1792963077777L)` produces `"a1a69db6a344da4d883865c637f979495dd86112a4534347b462259bfdeb641b"`, matching `RuntimeTests.swift:24-30` byte-for-byte.
   - `EncryptApiParams("{\"language\":\"vi\"}")` produces `"V5p1S6tTwaE91W8UDCagn9vVP9wUQ3QzA04wG7xh+5E="`, matching `RuntimeTests.swift:32-43` and Node.js crypto.
   - Curve25519 public key and shared secret derivation matches RFC 7748 Section 6.1 test vectors for Alice (`8520f009...`), Bob (`de9edb7d...`), and shared secret (`4a5d9d5b...`).
   - X3DH derivation matches Zalo protocol parameters (`info = "ZaloText"`, $SK$ computed identically between Alice and Bob).

2. **Integrity Violations Check**:
   - Inspected for hardcoded test results embedded in source: **NONE found**. The implementation performs full AES encryption, BigInteger modular arithmetic, HKDF-SHA256 expansions, and SHA-512 iterations.
   - Inspected for dummy/facade implementations: **NONE found**. All classes maintain real state and compute genuine cryptographic results.
   - Inspected for shortcuts bypassing core work: **NONE found**. The code does not delegate core math to unauthorized external tools or mocks.
   - Self-certifying or fabricated logs: **NONE found**. Independently built and executed via `dotnet test` with 11/11 passing crypto tests.

3. **Security Invariant Verification**:
   - Constant-time verification: `CryptographicOperations.FixedTimeEquals` is strictly used for both Keychain envelope HMAC validation and WhisperMessage wire tag validation.
   - Zero memory leakage: Ephemeral secrets (`derivedKeys`, `messageKey`, `expandedKeys`, `dhResult`, `dh1..dh4`, `RootKey`, `SendingChainKey`, `ReceivingChainKey`, `SkippedMessageKeys`) are scrubbed using `CryptographicOperations.ZeroMemory` in `finally` blocks and `Dispose()`.
   - DoS lookahead mitigation: `MaxMessageKeys = 2000` prevents remote memory exhaustion attacks.
   - Replay protection: Once a cached skipped key is consumed, it is immediately deleted from the dictionary.

---

## 3. Adversarial Review & Stress-Testing

| Attack Vector / Failure Mode | Scenario Evaluated | Predicted / Observed Behavior | Status |
|---|---|---|---|
| **Timing attack on MAC** | Attacker probes HMAC tags byte-by-byte on Keychain envelopes and WhisperMessage payloads | `CryptographicOperations.FixedTimeEquals` compares all bytes in constant time. Pre-decryption check aborts before AES decryption. | **PASS** |
| **Packet replay attack** | Attacker resends an out-of-order packet whose message key was previously cached | Message key is removed from `SkippedMessageKeys` upon first decryption; subsequent decryption attempt throws `CryptographicException`. | **PASS** |
| **Lookahead flood (DoS)** | Attacker sends a forged packet with sequence counter jump > 2,000 | Caught by `counter - ReceiveMessageCounter > MaxMessageKeys`, immediately throws `InvalidOperationException` without allocating skipped keys. | **PASS** |
| **Malformed curve points / prefix** | Peer sends a 33-byte key with invalid prefix byte (not `0x05`) or invalid length | `CalculateAgreement` throws `ArgumentException`; `VerifySignature` returns `false` without crashing. | **PASS** |
| **Tampered SignedPreKey in X3DH** | Man-in-the-middle modifies Bob's signed prekey | `Curve25519Helper.VerifySignature` returns `false`, causing `InitiateHandshake` to throw `CryptographicException` and abort session setup. | **PASS** |
| **Memory dump extraction** | Attacker dumps memory after session disposal or handshake | `ZeroMemory` wipes all derived keys, DH shared secrets, and session chain keys. | **PASS** |
| **Safety Number symmetry** | Evaluation of `ComputeSafetyNumber(A, B)` vs `ComputeSafetyNumber(B, A)` | Parties' 30-digit fingerprints are lexicographically sorted prior to formatting; `ab == ba` holds 100%. | **PASS** |

---

## 4. Caveats

No caveats. All 138 tests across both storage and cryptography subsystems pass cleanly without errors or warnings.

---

## 5. Conclusion

The Cryptography & Signal Protocol subsystem (Tasks T-014 through T-018) is **fully compliant** with all project requirements, architectural invariants, security guidelines, and test vectors.

**Verdict: APPROVE**

---

## 6. Verification Method

To independently reproduce this verification:

1. **Build Solution (Debug)**:
   ```powershell
   dotnet build Zenix.sln -c Debug
   ```
   *Expected*: `0 Warning(s)`, `0 Error(s)`.

2. **Run Cryptographic Test Suite (GATE 2B)**:
   ```powershell
   dotnet test tests/Zenix.Tests/Zenix.Tests.csproj --filter "Category=Crypto"
   ```
   *Expected*: `Passed: 11, Failed: 0, Total: 11`.

3. **Run Entire Solution Test Suite**:
   ```powershell
   dotnet test Zenix.sln
   ```
   *Expected*: `Passed: 138, Failed: 0, Total: 138`.
