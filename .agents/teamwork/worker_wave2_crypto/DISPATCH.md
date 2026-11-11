# Worker Dispatch: Cryptography Core & Signal Protocol E2EE (Wave 2, Tasks T-014..T-018)

## Mandatory Integrity Warning
DO NOT CHEAT. All implementations must be genuine. DO NOT hardcode test results, create dummy/facade implementations, or circumvent the intended task. A teamwork_auditor will independently verify your work. Integrity violations WILL be detected and your work WILL be rejected.

## Context
You are the Cryptography Worker for Wave 2: Storage & Cryptography Core. Your mission is to implement the entire Cryptography and Signal Protocol subsystem (Tasks T-014 through T-018), the `ISignalE2eeService` interface contract, and the comprehensive unit test suite in `tests/Zenix.Tests/Zalo/Crypto/`, targeting GATE 2B verification.

## Authoritative Inputs to Read
1. Specification report: `d:\DEV\WinUI\Zenix\.agents\teamwork\spec_miner_wave2_crypto\handoff.md`
2. Baseline survey: `d:\DEV\WinUI\Zenix\.agents\teamwork\explorer_wave2_1\handoff.md`
3. Project Plan & Layout: `d:\DEV\WinUI\Zenix\.agents\teamwork\orchestrator\PROJECT.md`
4. Original Request: `d:\DEV\WinUI\Zenix\.agents\teamwork\ORIGINAL_REQUEST.md`
5. Constitution: `d:\DEV\WinUI\Zenix\.specify\memory\constitution.md`
6. Reference implementation:
   - `d:\DEV\WinUI\zalo-swift-re\Tests\OlazTests\RuntimeTests.swift`
   - `d:\DEV\WinUI\zalo-swift-re\Tests\OlazTests\E2EETests.swift`
   - `d:\DEV\WinUI\zalo-swift-re\Sources\Olaz\ZaloRuntime\CryptoBridge.swift`
   - `d:\DEV\WinUI\zalo-swift-re\Sources\Olaz\JSBridge\SignalE2EE.swift`
   - `d:\DEV\WinUI\zalo-swift-re\Sources\Olaz\JSModules\vendor\libsignal-protocol.js`

## Target Implementation Files (Strict Ownership)
You have exclusive write ownership of the following files:
1. `Zenix/Core/Zalo/Contracts/ISignalE2eeService.cs`:
   - New contract in `Zenix.Core.Zalo.Contracts`.
   - Must strictly comply with `ContractsArchitectureTests`:
     - Starts with `'I'`.
     - Zero UI references (`Microsoft.UI.Xaml.*`, `UIElement`, etc.).
     - 100% of asynchronous methods accept `CancellationToken cancellationToken = default`.
     - 100% XML doc comments in technical American English.
2. `Zenix/Core/Zalo/Cryptography/ZaloCryptoService.cs`:
   - Implements `IZaloCryptoService` from `Zenix.Core.Zalo.Contracts`.
   - `EncryptZcid`: AES-256-CBC, fixed key `"3FC4F0D2AB50057BCE0D90D9187A22B1"`, zero IV (16 bytes), PKCS7 padding, lowercase hexadecimal string output.
     - Verification vector: Input `"23,abc-imei,1792963077777"` -> Output `"a1a69db6a344da4d883865c637f979495dd86112a4534347b462259bfdeb641b"`.
   - `EncryptApiParams`: AES-256-CBC, fixed key `"DE71B7D4C57C0749158CB90D5E431AE3"`, zero IV (16 bytes), PKCS7 padding, standard Base64 output.
     - Verification vector: Input `"{\"language\":\"vi\"}"` -> Output `"V5p1S6tTwaE91W8UDCagn9vVP9wUQ3QzA04wG7xh+5E="`.
   - `DecryptApiParams`: Decrypts Base64 ciphertext back to UTF-8 JSON plaintext.
3. `Zenix/Core/Zalo/Cryptography/ZaloCryptoService.Keychain.cs`:
   - Partial class of `ZaloCryptoService`.
   - `EncryptKeychainData` & `DecryptKeychainData`:
     - Layout: `[0x49: 1B][IV: 16B][Salt: 16B][Ciphertext: NB][HMAC: 32B]` (min length: 65 bytes).
     - Key derivation: `HKDF.DeriveKey(HashAlgorithmName.SHA256, rootSecret, 64, salt, Encoding.UTF8.GetBytes("ZaloDeviceLink"))` -> 32B AES key + 32B HMAC key. Fallback info: `"ZprotocolSecureKDF"`.
     - HMAC authentication: HMAC-SHA256 computed over `[0x49 || IV || Salt || Ciphertext]`.
     - Constant-time verification: `CryptographicOperations.FixedTimeEquals(computedHmac, envelopeHmac)` BEFORE decryption.
4. `Zenix/Core/Zalo/Cryptography/MediaDenoiseHelper.cs`:
   - `DenoiseMedia`: Checks for 6-byte anti-scraping signature `07 7a 41 6c 4f 07`. If present, strips the 6 bytes and applies byte-wise XOR 147 (`0x93`) to the remainder. If absent or buffer < 6 bytes, returns original input unchanged.
5. `Zenix/Core/Zalo/Cryptography/Curve25519Helper.cs`:
   - Montgomery ladder scalar multiplication on Curve25519 ($u = 9$, base point).
   - RFC 7748 bit clamping: `privKey[0] &= 248; privKey[31] &= 127; privKey[31] |= 64;`.
   - Supports 32-byte raw keys and 33-byte `0x05`-prefixed keys.
6. `Zenix/Core/Zalo/Cryptography/SignalX3dhService.cs`:
   - Manages identity keys, signed prekeys (signed by identity private key using Curve25519 signature), and one-time prekeys.
   - Master shared secret derivation: $SK = \text{HKDF}(\text{DH}_1 \,||\, \text{DH}_2 \,||\, \text{DH}_3 \,[||\, \text{DH}_4], \text{salt}=0_{32}, \text{info}=\text{"ZaloText"})$.
   - Alice and Bob computing shared secret must yield 100% identical 32-byte keys.
7. `Zenix/Core/Zalo/Cryptography/DoubleRatchetSessionState.cs`:
   - Session state tracking: `rootKey`, `sendingChainKey`, `receivingChainKey`, `localEphemeralKeyPair`, `remoteEphemeralPublicKey`, `sendMessageCounter`, `receiveMessageCounter`, and `skippedMessageKeys` dictionary with TTL expiration.
8. `Zenix/Core/Zalo/Cryptography/SignalDoubleRatchetService.cs`:
   - Symmetric KDF chains: `messageKey = HMAC-SHA256(chainKey, [0x01])`, `nextChainKey = HMAC-SHA256(chainKey, [0x02])`.
   - Asymmetric DH ratchet steps: derives new rootKey and chainKey via `HKDF(ECDHE(remoteEphemeral, localEphemeralPriv), rootKey, "ZaloRatchet")`.
   - Message key expansion: `HKDF(messageKey, zeroes_32, "ZaloMessageKeys")` -> 32B AES key + 32B MAC key + 16B IV.
   - Skipped message keys table: allows decrypting packets arriving out of order (tested up to 100 steps) with maximum lookahead threshold `MAX_MESSAGE_KEYS = 2000`.
9. `Zenix/Core/Zalo/Cryptography/SafetyNumberFormatter.cs`:
   - Mutual 60-digit fingerprinting over 5200 iterations of SHA-512.
   - Extracts 40-bit big-endian chunks modulo 100,000.
   - Symmetrically sorts identity keys so `ComputeSafetyNumber(A, B) == ComputeSafetyNumber(B, A)`.
   - Formats output into 12 groups of 5 decimal digits: `XXXXX XXXXX XXXXX XXXXX XXXXX XXXXX XXXXX XXXXX XXXXX XXXXX XXXXX XXXXX`.
10. `Zenix/Core/Zalo/Cryptography/SignalE2eeService.cs`:
    - Implements `ISignalE2eeService`. Coordinates X3DH session initialization, Double Ratchet encryption/decryption, serialization/deserialization of `PreKeyWhisperMessage` (Type 3) and `WhisperMessage` (Type 1) wire envelopes (version `0x33`, 8-byte HMAC tag).
    - Exposes `ComputeSafetyNumber` via `SafetyNumberFormatter`.

## Target Unit Test Files
Create the comprehensive test suite in `tests/Zenix.Tests/Zalo/Crypto/` with `[Trait("Category", "Crypto")]`:
- `tests/Zenix.Tests/Zalo/Crypto/ZaloCryptoTests.cs`:
  - `VerifyZcidAndParamEncryption`: Asserts `EncryptZcid` produces `"a1a69db6a344da4d883865c637f979495dd86112a4534347b462259bfdeb641b"`, `EncryptApiParams` produces `"V5p1S6tTwaE91W8UDCagn9vVP9wUQ3QzA04wG7xh+5E="`, and `DecryptApiParams` roundtrips cleanly.
  - `VerifyKeychainAndDenoise`: Asserts Keychain envelope encrypt/decrypt roundtrip, rejects tampered HMAC in constant time, asserts XOR 147 media de-noising restores clean JPEG header from scrambled test buffer, and ignores non-scrambled data.
- `tests/Zenix.Tests/Zalo/Crypto/SignalX3dhTests.cs`:
  - Asserts Curve25519 key agreements, PreKey bundle generation, and asserts Alice and Bob compute 100% identical shared secrets.
- `tests/Zenix.Tests/Zalo/Crypto/DoubleRatchetTests.cs`:
  - Simulates 100 sequential ratchet steps (send/receive) and verifies out-of-order packet delivery succeeds without decryption errors using skipped message keys table.
- `tests/Zenix.Tests/Zalo/Crypto/SignalE2eeTests.cs`:
  - Asserts WhisperMessage and PreKeyWhisperMessage wire serialization and roundtrip encryption/decryption.
  - Asserts 60-digit Safety Number calculation produces 12 groups of 5 digits and is symmetric between Alice and Bob.

## Technical Invariants & Rules
- **CRLF Line Endings**: All files must use Windows CRLF (`\r\n`).
- **Roslyn IDE0065**: All `using` directives must strictly be outside and above namespace declarations.
- **Zero UI Dependencies**: Absolutely zero references to `Microsoft.UI.Xaml.*`, `UIElement`, `Window`, etc.
- **Async Methods**: 100% of asynchronous methods must accept `CancellationToken cancellationToken = default`.
- **English Documentation**: 100% of public types, constructors, methods, properties must have XML doc comments (`/// <summary>`) in technical American English.
- **Constant-Time Verification**: All HMAC verifications must use `CryptographicOperations.FixedTimeEquals`.
- **Zero Memory Leakage**: Ephemeral keys and sensitive plaintext buffers must be zeroed using `CryptographicOperations.ZeroMemory` or wrapped in disposable structs.
- **Verification Commands**:
  - `dotnet build Zenix.sln -c Debug` -> Must succeed with 0 errors and 0 warnings.
  - `dotnet test tests/Zenix.Tests/Zenix.Tests.csproj --filter "Category=Crypto"` -> All crypto tests pass.
  - `dotnet test Zenix.sln` -> All existing 83 tests + all new crypto tests must pass without any regression.

## Deliverable
Write your implementation changes report and verification results to:
`d:\DEV\WinUI\Zenix\.agents\teamwork\worker_wave2_crypto\handoff.md`
Update your `progress.md` with liveness timestamps.
Once finished, send a brief message to the orchestrator: "I'm done, check my report".

## 2026-11-11T08:26:01Z
[Message] sender=8f2b359c-f3ff-43a7-ad0b-7cd2933ec630 priority=MESSAGE_PRIORITY_HIGH
Read d:\DEV\WinUI\Zenix\.agents\teamwork\worker_wave2_crypto\DISPATCH.md and implement the Cryptography & Signal Protocol subsystem and unit tests for Wave 2 (Tasks T-014 to T-018).
Working directory: d:\DEV\WinUI\Zenix
State folder: d:\DEV\WinUI\Zenix\.agents\teamwork\worker_wave2_crypto
Original request: d:\DEV\WinUI\Zenix\.agents\teamwork\ORIGINAL_REQUEST.md

