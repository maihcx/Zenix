# Handoff Report: Double Ratchet State-Poisoning & Lookahead Remediation Strategy

**Agent**: `explorer_wave2_remediation`  
**Date**: 2026-11-11T09:34:00Z  
**Milestone**: Wave 2: Storage & Cryptography Core (GATE 2B Remediation)  
**State Folder**: `d:\DEV\WinUI\Zenix\.agents\teamwork\explorer_wave2_remediation`  
**Verdict**: `STRATEGY_READY`  

---

## 1. Observation

Direct investigation of the codebase and challenger findings revealed the following concrete vulnerabilities:

### 1.1 Irrevocable Session Poisoning via Pre-Authentication State Mutation
- **Source Location**: `Zenix/Core/Zalo/Cryptography/SignalE2eeService.cs:264-287` and `SignalDoubleRatchetService.cs:183-227`.
- **Verbatim Code Path**:
  In `SignalE2eeService.cs:264-270`:
  ```csharp
  var (plaintext, macKey) = new SignalDoubleRatchetService().Decrypt(
      session,
      ephemeralKey,
      counter,
      prevCounter,
      ciphertext);
  ```
  Inside `SignalDoubleRatchetService.Decrypt` (`SignalDoubleRatchetService.cs:187-190`):
  ```csharp
  if (isNewEphemeral)
  {
      StepDhRatchet(session, remoteEphemeralKey, previousCounter);
  }
  ```
  `StepDhRatchet` (`SignalDoubleRatchetService.cs:253-289`) unconditionally mutates the live `session`:
  ```csharp
  session.RootKey = derivedRecv[..32].ToArray();
  session.ReceivingChainKey = derivedRecv[32..64].ToArray();
  session.ReceiveMessageCounter = 0;
  session.RemoteEphemeralPublicKey = remoteEphemeralKey.ToArray();
  session.PreviousCounter = session.SendMessageCounter;
  ...
  var newLocalKp = Curve25519Helper.GenerateKeyPair(includePrefix: true);
  ...
  session.LocalEphemeralKeyPair = newLocalKp;
  ```
  Only *after* `SignalDoubleRatchetService.Decrypt` completes and mutates `session` does `SignalE2eeService.DecryptWhisperWire` check the HMAC tag (`SignalE2eeService.cs:284-287`):
  ```csharp
  if (!CryptographicOperations.FixedTimeEquals(computedTag, wireHmac))
  {
      throw new CryptographicException("HMAC authentication failed for WhisperMessage. Message has been tampered with or corrupted.");
  }
  ```
- **Consequence**: When a tampered or forged packet is received, the HMAC failure throws `CryptographicException`, but `session` remains mutated with bogus keys. Bob's `LocalEphemeralKeyPair` was regenerated, destroying the private key Alice expects, and `RootKey` / chain keys are permanently corrupted. As proven in `AdversarialCryptoTests.cs:524-590`, Alice's subsequent legitimate messages can never be decrypted by Bob.

### 1.2 Unbounded Lookahead in `StepDhRatchet` (Memory Exhaustion DoS)
- **Source Location**: `Zenix/Core/Zalo/Cryptography/SignalDoubleRatchetService.cs:240-251`.
- **Verbatim Code Path**:
  ```csharp
  if (session.ReceivingChainKey != null && !string.IsNullOrEmpty(oldEphemeralHex))
  {
      while (session.ReceiveMessageCounter < remotePreviousCounter)
      {
          var skipKey = HMACSHA256.HashData(session.ReceivingChainKey, StepOne);
          var nextChain = HMACSHA256.HashData(session.ReceivingChainKey, StepTwo);
          CryptographicOperations.ZeroMemory(session.ReceivingChainKey);
          session.ReceivingChainKey = nextChain;

          session.SkippedMessageKeys[(oldEphemeralHex, session.ReceiveMessageCounter)] = (skipKey, DateTime.UtcNow);
          session.ReceiveMessageCounter++;
      }
  }
  ```
- **Consequence**: Unlike line 203 which checks `counter - session.ReceiveMessageCounter > MaxMessageKeys`, `StepDhRatchet` has no check on `remotePreviousCounter`. An attacker presenting `remotePreviousCounter = 100_000` or `1_000_000` forces the loop to execute 1,000,000 times, performing 2,000,000 HMAC-SHA256 iterations and allocating 1,000,000 dictionary entries into `SkippedMessageKeys`.

### 1.3 Pre-Authentication Handshake Overwrite in Type 3 Messages
- **Source Location**: `Zenix/Core/Zalo/Cryptography/SignalE2eeService.cs:174-184`.
- **Verbatim Code Path**:
  ```csharp
  var session = _ratchetService.InitializeRecipientSession(...);
  _sessions[sessionKey] = session;
  return Task.FromResult(DecryptWhisperWire(innerWhisperWire, session));
  ```
- **Consequence**: `_sessions[sessionKey]` is overwritten *before* `innerWhisperWire` is validated. If the inner wire packet fails HMAC authentication, an existing healthy session is destroyed and replaced with an unverified session.

### 1.4 Line Endings Compliance
- **Source Location**: `tests/Zenix.Tests/Zalo/Storage/StorageStressChallengerTests.cs`.
- **Evidence**: Verified via PowerShell byte inspection (`hasCr: False, hasLf: True`). The file contains pure LF line endings, in violation of Constitution Principle I (CRLF Line Endings Invariant).

---

## 2. Logic Chain

1. **Root Cause Analysis of State Poisoning**:
   - Double Ratchet generates message-specific MAC keys (`macKey`) from each message's ratcheted `messageKey` via HKDF.
   - Deriving `macKey` requires stepping the ratchet forward.
   - However, in standard cryptographic protocol design, **state transitions must be transactional/atomic**: state mutations must only be committed to the live session *after* message authentication succeeds.
   - If key derivation mutates the live session before wire HMAC validation, any unauthenticated adversary can forge a packet with a random ephemeral key and invalidate the session forever.

2. **Root Cause Analysis of Encrypt-then-MAC Violation**:
   - Currently, `SignalDoubleRatchetService.DecryptWithKey` invokes `decryptor.TransformFinalBlock(...)` (AES decryption) before the caller receives `macKey` to verify the wire HMAC.
   - If the ciphertext is corrupted, PKCS7 padding failure or block alignment errors occur before wire HMAC verification can reject the packet, violating the Encrypt-then-MAC principle and exposing potential padding oracle vectors.

3. **Remediation Invariants**:
   - **Invariant 1 (Atomic State Transition)**: Double Ratchet session state mutations must operate on an isolated candidate clone (`candidate = session.Clone()`). If wire HMAC verification fails, `candidate` is zeroed and disposed; `session` remains completely untouched.
   - **Invariant 2 (Commit on Authentication)**: Only when `CryptographicOperations.FixedTimeEquals` confirms the wire HMAC tag does `candidate.CommitTo(session)` transfer the mutated buffers into the live session.
   - **Invariant 3 (Encrypt-then-MAC Discipline)**: `SignalDoubleRatchetService` must separate key derivation (`DeriveMessageKeys`) from AES decryption (`DecryptCiphertext`). The wire HMAC must be authenticated before AES decryption is attempted.
   - **Invariant 4 (Bounded Lookahead)**: `StepDhRatchet` must reject `remotePreviousCounter - session.ReceiveMessageCounter > MaxMessageKeys` (2000) before entering the loop, with guards against unsigned underflow.
   - **Invariant 5 (Type 3 Handshake Atomicity)**: `SignalE2eeService` must only commit new sessions from Type 3 PreKeyWhisperMessages after the inner wire payload decrypts and passes HMAC authentication.

---

## 3. Caveats

- **Concurrency**: Although `_sessions` is a `ConcurrentDictionary`, Double Ratchet is inherently sequential. Staging transactions under a per-session monitor (`lock (session)`) is necessary to prevent race conditions during candidate commits when messages arrive concurrently.
- **Backward Compatibility**: `DoubleRatchetTests.cs` directly calls `_ratchet.Decrypt(session, ...)`. To avoid breaking existing tests, `SignalDoubleRatchetService.Decrypt` must be retained as a convenience wrapper calling `DeriveMessageKeys` followed by `DecryptCiphertext`.

---

## 4. Conclusion & Detailed Remediation Strategy

The worker should implement the following targeted, atomic changes across 4 files:

### 4.1 Changes to `Zenix/Core/Zalo/Cryptography/DoubleRatchetSessionState.cs`

Add `Clone()` and `CommitTo()` methods, and implement secure buffer transfer:

```csharp
/// <summary>
/// Creates an independent deep copy of the Double Ratchet session state for staged candidate calculations.
/// </summary>
/// <returns>A new <see cref="DoubleRatchetSessionState"/> candidate clone.</returns>
public DoubleRatchetSessionState Clone()
{
    var clone = new DoubleRatchetSessionState
    {
        RootKey = (byte[])RootKey.Clone(),
        SendingChainKey = SendingChainKey != null ? (byte[])SendingChainKey.Clone() : null,
        ReceivingChainKey = ReceivingChainKey != null ? (byte[])ReceivingChainKey.Clone() : null,
        LocalEphemeralKeyPair = (
            LocalEphemeralKeyPair.PrivateKey != null ? (byte[])LocalEphemeralKeyPair.PrivateKey.Clone() : [],
            LocalEphemeralKeyPair.PublicKey != null ? (byte[])LocalEphemeralKeyPair.PublicKey.Clone() : []
        ),
        RemoteEphemeralPublicKey = RemoteEphemeralPublicKey != null ? (byte[])RemoteEphemeralPublicKey.Clone() : null,
        OurIdentityPublicKey = (byte[])OurIdentityPublicKey.Clone(),
        RemoteIdentityPublicKey = (byte[])RemoteIdentityPublicKey.Clone(),
        SendMessageCounter = SendMessageCounter,
        ReceiveMessageCounter = ReceiveMessageCounter,
        PreviousCounter = PreviousCounter,
        RemoteRegistrationId = RemoteRegistrationId
    };

    foreach (var (key, value) in SkippedMessageKeys)
    {
        clone.SkippedMessageKeys[key] = ((byte[])value.MessageKey.Clone(), value.CreatedAt);
    }

    return clone;
}

/// <summary>
/// Atomically commits this candidate state's mutations to the target live session state,
/// securely zeroing the target's superseded keys and transferring buffer ownership.
/// </summary>
/// <param name="target">The live session state to update.</param>
public void CommitTo(DoubleRatchetSessionState target)
{
    ArgumentNullException.ThrowIfNull(target);

    // 1. Securely zero target's previous cryptographic keys
    CryptographicOperations.ZeroMemory(target.RootKey);
    if (target.SendingChainKey != null) CryptographicOperations.ZeroMemory(target.SendingChainKey);
    if (target.ReceivingChainKey != null) CryptographicOperations.ZeroMemory(target.ReceivingChainKey);
    if (target.LocalEphemeralKeyPair.PrivateKey != null) CryptographicOperations.ZeroMemory(target.LocalEphemeralKeyPair.PrivateKey);

    foreach (var entry in target.SkippedMessageKeys.Values)
    {
        CryptographicOperations.ZeroMemory(entry.MessageKey);
    }
    target.SkippedMessageKeys.Clear();

    // 2. Transfer state buffers to target
    target.RootKey = RootKey;
    target.SendingChainKey = SendingChainKey;
    target.ReceivingChainKey = ReceivingChainKey;
    target.LocalEphemeralKeyPair = LocalEphemeralKeyPair;
    target.RemoteEphemeralPublicKey = RemoteEphemeralPublicKey;
    target.OurIdentityPublicKey = OurIdentityPublicKey;
    target.RemoteIdentityPublicKey = RemoteIdentityPublicKey;
    target.SendMessageCounter = SendMessageCounter;
    target.ReceiveMessageCounter = ReceiveMessageCounter;
    target.PreviousCounter = PreviousCounter;
    target.RemoteRegistrationId = RemoteRegistrationId;

    foreach (var (key, value) in SkippedMessageKeys)
    {
        target.SkippedMessageKeys[key] = value;
    }

    // 3. Detach sensitive references from candidate so Dispose() does not wipe transferred buffers
    RootKey = [];
    SendingChainKey = null;
    ReceivingChainKey = null;
    LocalEphemeralKeyPair = ([], []);
    RemoteEphemeralPublicKey = null;
    SkippedMessageKeys.Clear();
}
```

### 4.2 Changes to `Zenix/Core/Zalo/Cryptography/SignalDoubleRatchetService.cs`

1. **Enforce `MaxMessageKeys` in `StepDhRatchet`** (`SignalDoubleRatchetService.cs:238-251`):
   ```csharp
   // Exhaust remaining skipped keys on previous receiving chain if applicable
   if (session.ReceivingChainKey != null && !string.IsNullOrEmpty(oldEphemeralHex))
   {
       // Guard against unsigned underflow and excessive future gap jumps (> 2000)
       if (remotePreviousCounter > session.ReceiveMessageCounter &&
           remotePreviousCounter - session.ReceiveMessageCounter > MaxMessageKeys)
       {
           throw new InvalidOperationException($"Over messages into the future: {MaxMessageKeys}");
       }

       while (session.ReceiveMessageCounter < remotePreviousCounter)
       {
           var skipKey = HMACSHA256.HashData(session.ReceivingChainKey, StepOne);
           var nextChain = HMACSHA256.HashData(session.ReceivingChainKey, StepTwo);
           CryptographicOperations.ZeroMemory(session.ReceivingChainKey);
           session.ReceivingChainKey = nextChain;

           session.SkippedMessageKeys[(oldEphemeralHex, session.ReceiveMessageCounter)] = (skipKey, DateTime.UtcNow);
           session.ReceiveMessageCounter++;
       }
   }
   ```

2. **Expose `DeriveMessageKeys` and `DecryptCiphertext`**:
   Refactor `Decrypt` into two composable primitives:
   - `public (byte[] EncKey, byte[] MacKey, byte[] Iv) DeriveMessageKeys(DoubleRatchetSessionState session, ReadOnlySpan<byte> remoteEphemeralKey, uint counter, uint previousCounter)`:
     Executes ratchet advancement, skipped key updates, and HKDF expansion to return `(encKey, macKey, iv)`. Does NOT call AES decryption.
   - `public byte[] DecryptCiphertext(ReadOnlySpan<byte> encKey, ReadOnlySpan<byte> iv, ReadOnlySpan<byte> ciphertext)`:
     Executes AES-256-CBC decryption with PKCS7 padding.
   - Maintain `public (byte[] Plaintext, byte[] MacKey) Decrypt(...)`:
     ```csharp
     public (byte[] Plaintext, byte[] MacKey) Decrypt(
         DoubleRatchetSessionState session,
         ReadOnlySpan<byte> remoteEphemeralKey,
         uint counter,
         uint previousCounter,
         ReadOnlySpan<byte> ciphertext)
     {
         ArgumentNullException.ThrowIfNull(session);

         var (encKey, macKey, iv) = DeriveMessageKeys(session, remoteEphemeralKey, counter, previousCounter);
         try
         {
             var plaintext = DecryptCiphertext(encKey, iv, ciphertext);
             return (plaintext, macKey);
         }
         finally
         {
             CryptographicOperations.ZeroMemory(encKey);
             CryptographicOperations.ZeroMemory(iv);
         }
     }
     ```

### 4.3 Changes to `Zenix/Core/Zalo/Cryptography/SignalE2eeService.cs`

1. **Transactional Staged Execution in `DecryptWhisperWire`**:
   ```csharp
   private static string DecryptWhisperWire(ReadOnlySpan<byte> wireBytes, DoubleRatchetSessionState session)
   {
       if (wireBytes.Length < 1 + 1 + 8) // version (1B) + min proto (1B) + HMAC (8B)
       {
           throw new CryptographicException("Whisper message payload is too short to be valid.");
       }

       var versionByte = wireBytes[0];
       if ((versionByte & 15) > 3 || (versionByte >> 4) < 3)
       {
           throw new CryptographicException($"Incompatible version number 0x{versionByte:X2} on WhisperMessage.");
       }

       var protoBytes = wireBytes[1..^8];
       var wireHmac = wireBytes[^8..];

       var (ephemeralKey, counter, prevCounter, ciphertext) = DecodeWhisperMessage(protoBytes);

       // Protect session from concurrent race conditions
       lock (session)
       {
           // STAGED CANDIDATE TRANSITION:
           // Operate strictly on a cloned candidate state.
           // If HMAC check fails, candidate is disposed and live session remains 100% untouched.
           using var candidate = session.Clone();

           var ratchetService = new SignalDoubleRatchetService();
           var (encKey, macKey, iv) = ratchetService.DeriveMessageKeys(
               candidate,
               ephemeralKey,
               counter,
               prevCounter);

           try
           {
               // Verify HMAC-SHA256 truncated 8-byte tag FIRST (Encrypt-then-MAC discipline)
               // MAC Input: [RemoteIK: 33B][OurIK: 33B][0x33: 1B][ProtobufBytes: NB]
               var macInputLen = candidate.RemoteIdentityPublicKey.Length + candidate.OurIdentityPublicKey.Length + 1 + protoBytes.Length;
               var macInput = new byte[macInputLen];
               candidate.RemoteIdentityPublicKey.CopyTo(macInput.AsSpan(0));
               candidate.OurIdentityPublicKey.CopyTo(macInput.AsSpan(candidate.RemoteIdentityPublicKey.Length));
               macInput[candidate.RemoteIdentityPublicKey.Length + candidate.OurIdentityPublicKey.Length] = WireVersion;
               protoBytes.CopyTo(macInput.AsSpan(candidate.RemoteIdentityPublicKey.Length + candidate.OurIdentityPublicKey.Length + 1));

               var fullHmac = HMACSHA256.HashData(macKey, macInput);
               var computedTag = fullHmac.AsSpan(0, 8);

               if (!CryptographicOperations.FixedTimeEquals(computedTag, wireHmac))
               {
                   throw new CryptographicException("HMAC authentication failed for WhisperMessage. Message has been tampered with or corrupted.");
               }

               // Wire packet is authentic! Atomically commit candidate mutations to live session.
               candidate.CommitTo(session);

               // Decrypt ciphertext with authenticated keys
               var plaintext = ratchetService.DecryptCiphertext(encKey, iv, ciphertext);
               return Encoding.UTF8.GetString(plaintext);
           }
           finally
           {
               CryptographicOperations.ZeroMemory(encKey);
               CryptographicOperations.ZeroMemory(macKey);
               CryptographicOperations.ZeroMemory(iv);
           }
       }
   }
   ```

2. **Atomic Handshake Commit in Type 3 Messages (`SignalE2eeService.cs:174-184`)**:
   ```csharp
   if (envelope.Type == 3)
   {
       var preKeyProto = envelopeBytes.AsSpan(1);
       var (opkId, aliceBaseKey, aliceIdKey, innerWhisperWire, senderRegId, _) = DecodePreKeyWhisperMessage(preKeyProto);

       var masterSecret = _x3dhService.ReceiveHandshake(recipientStore, aliceIdKey, aliceBaseKey, opkId);

       var session = _ratchetService.InitializeRecipientSession(
           masterSecret,
           (recipientStore.SignedPreKey.PrivateKey, recipientStore.SignedPreKey.PublicKey),
           recipientStore.IdentityKeyPair.PublicKey,
           aliceIdKey,
           (int)senderRegId);

       // Decrypt inner wire FIRST. If HMAC fails, session is discarded and never added to _sessions.
       var plaintext = DecryptWhisperWire(innerWhisperWire, session);

       // Only commit session to cache upon successful authentication and decryption
       _sessions[sessionKey] = session;

       return Task.FromResult(plaintext);
   }
   ```

### 4.4 Changes to `tests/Zenix.Tests/Zalo/Crypto/AdversarialCryptoTests.cs`

Update three test cases to assert positive security invariants:

1. **`Adversarial_ForgedEphemeralKeyPacket_DoesNotPoisonSessionState`** (formerly `...ProbesPermanentSessionPoisoning`):
   ```csharp
   // Bob attempts to decrypt forged packet -> throws HMAC exception
   var actBogus = async () => await _e2ee.DecryptAsync(forgedEnvelope, bobId, aliceId);
   await actBogus.Should().ThrowAsync<CryptographicException>()
       .WithMessage("*HMAC authentication failed for WhisperMessage*");

   // Alice transmits a legitimate message
   var legitEnv = await _e2ee.EncryptAsync("legitimate_message_after_attack", aliceId, bobId);

   // Bob MUST decrypt Alice's legitimate message cleanly without error
   var decrypted = await _e2ee.DecryptAsync(legitEnv, bobId, aliceId);
   decrypted.Should().Be("legitimate_message_after_attack",
       "Bob's session state must remain completely unpoisoned and synchronized with Alice after a forged packet is rejected.");
   ```

2. **`StepDhRatchet_LargePreviousCounter_ThrowsInvalidOperationExceptionAndProtectsMemory`** (formerly `...ProbesLookaheadBound`):
   ```csharp
   Action act = () => _ratchet.Decrypt(bobSession, ephemNew, counterNew, 2005, ctNew);

   act.Should().Throw<InvalidOperationException>()
       .WithMessage("*Over messages into the future: 2000*",
           "StepDhRatchet must enforce MaxMessageKeys bounds on remotePreviousCounter before loop execution.");

   bobSession.SkippedMessageKeys.Should().BeEmpty(
       "Memory must not allocate skipped keys when StepDhRatchet rejects an excessive lookahead jump.");
   ```

3. **`Adversarial_TamperedPacketDoesNotAdvanceCounter_AllowsLegitimateRetransmission`** (formerly `...ProbesDesync`):
   ```csharp
   // Bob attempts to decrypt tampered message 1 -> fails
   var decryptAct = async () => await _e2ee.DecryptAsync(tamperedEnv1, bobId, aliceId);
   await decryptAct.Should().ThrowAsync<CryptographicException>()
       .WithMessage("*HMAC authentication failed for WhisperMessage*");

   // Retransmission of authentic message 1 succeeds because Bob's counter was not incremented
   var retransmitted = await _e2ee.DecryptAsync(env1, bobId, aliceId);
   retransmitted.Should().Be("msg1_legit",
       "Retransmission of an authentic message after in-transit corruption must succeed because failed messages do not advance state.");

   // Alice sends message 2
   var env2 = await _e2ee.EncryptAsync("msg2_legit", aliceId, bobId);
   var decrypted2 = await _e2ee.DecryptAsync(env2, bobId, aliceId);
   decrypted2.Should().Be("msg2_legit");
   ```

### 4.5 Line Endings Normalization
- Convert `tests/Zenix.Tests/Zalo/Storage/StorageStressChallengerTests.cs` and `tests/Zenix.Tests/Zalo/Crypto/AdversarialCryptoTests.cs` to CRLF (`\r\n`).

---

## 5. Verification Method

To independently verify the implementation after code application:

1. **Execute Crypto Test Suite**:
   ```powershell
   dotnet test tests/Zenix.Tests/Zenix.Tests.csproj --filter "Category=Crypto"
   ```
   *Expected Result*: All 29 tests pass (0 failures), specifically confirming:
   - `Adversarial_ForgedEphemeralKeyPacket_DoesNotPoisonSessionState` passes.
   - `StepDhRatchet_LargePreviousCounter_ThrowsInvalidOperationExceptionAndProtectsMemory` passes.
   - `Adversarial_TamperedPacketDoesNotAdvanceCounter_AllowsLegitimateRetransmission` passes.

2. **Execute Full Solution Regression**:
   ```powershell
   dotnet test Zenix.sln
   ```
   *Expected Result*: All 162 tests pass with 0 failures and 0 warnings.

3. **Verify CRLF Compliance**:
   ```powershell
   Get-ChildItem -Path Zenix, tests -Recurse -Include *.cs, *.xaml | Where-Object { -not ((Get-Content -Raw $_.FullName) -match '\r\n') } | Select-Object FullName
   ```
   *Expected Result*: 0 files returned (100% CRLF).
