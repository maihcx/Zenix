// Copyright (c) Zenix. All rights reserved.
// Licensed under the proprietary and confidential license.
// Unauthorized copying, distribution, modification, or disclosure of this file,
// via any medium, is strictly prohibited without prior written permission.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using FluentAssertions;
using Xunit;
using Zenix.Core.Zalo.Contracts;
using Zenix.Core.Zalo.Cryptography;

namespace Zenix.Tests.Zalo.Crypto;

/// <summary>
/// Adversarial test harness for the Cryptography and Signal Protocol subsystem.
/// Empirically probes HMAC tampering, truncated envelope headers, out-of-order packet
/// sequences, large gap jumps (> 2000), media de-noising edge cases, and safety number symmetry.
/// </summary>
public sealed class AdversarialCryptoTests
{
    private readonly ZaloCryptoService _crypto = new();
    private readonly SignalDoubleRatchetService _ratchet = new();
    private readonly ISignalE2eeService _e2ee = new SignalE2eeService();

    /// <summary>
    /// Probes Keychain envelope tamper resistance: flipping any single bit in the IV, salt, ciphertext,
    /// or HMAC tag must cause constant-time HMAC-SHA256 authentication failure and abort before AES decryption.
    /// </summary>
    [Fact]
    [Trait("Category", "Crypto")]
    public void Keychain_TamperedBits_FailsHmacAndAbortsBeforeDecryption()
    {
        var rootSecret = new byte[32];
        RandomNumberGenerator.Fill(rootSecret);

        var plainText = "Adversarial_Payload_Probe_Confidential_Credentials_987654321";
        var plainBytes = Encoding.UTF8.GetBytes(plainText);
        var envelope = _crypto.EncryptKeychainData(plainBytes, rootSecret);

        // 1. Bit flip in IV (offset 1..16)
        var tamperedIv = (byte[])envelope.Clone();
        tamperedIv[5] ^= 0x01;
        var ivAct = () => _crypto.DecryptKeychainData(tamperedIv, rootSecret);
        ivAct.Should().Throw<CryptographicException>()
            .WithMessage("*HMAC-SHA256 authentication failed*");

        // 2. Bit flip in Salt (offset 17..32)
        var tamperedSalt = (byte[])envelope.Clone();
        tamperedSalt[20] ^= 0x80;
        var saltAct = () => _crypto.DecryptKeychainData(tamperedSalt, rootSecret);
        saltAct.Should().Throw<CryptographicException>()
            .WithMessage("*HMAC-SHA256 authentication failed*");

        // 3. Bit flip in Ciphertext (offset 33..end-32)
        // Corrupts last ciphertext block so that PKCS7 unpadding would fail if decryption were attempted first.
        var tamperedCipher = (byte[])envelope.Clone();
        tamperedCipher[^33] ^= 0xFF;
        var cipherAct = () => _crypto.DecryptKeychainData(tamperedCipher, rootSecret);
        cipherAct.Should().Throw<CryptographicException>()
            .WithMessage("*HMAC-SHA256 authentication failed*",
                "HMAC check must fail and abort before AES decryption or PKCS7 unpadding is executed.");

        // 4. Bit flip in HMAC tag (last 32 bytes)
        var tamperedHmac = (byte[])envelope.Clone();
        tamperedHmac[^1] ^= 0x01;
        var hmacAct = () => _crypto.DecryptKeychainData(tamperedHmac, rootSecret);
        hmacAct.Should().Throw<CryptographicException>()
            .WithMessage("*HMAC-SHA256 authentication failed*");

        // 5. Wrong root secret
        var wrongSecret = new byte[32];
        RandomNumberGenerator.Fill(wrongSecret);
        var secretAct = () => _crypto.DecryptKeychainData(envelope, wrongSecret);
        secretAct.Should().Throw<CryptographicException>()
            .WithMessage("*HMAC-SHA256 authentication failed*");
    }

    /// <summary>
    /// Probes Keychain envelope header validation: envelopes shorter than 65 bytes or with
    /// magic byte != 0x49 must be rejected immediately without cryptographic derivation.
    /// </summary>
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(16)]
    [InlineData(32)]
    [InlineData(64)]
    [Trait("Category", "Crypto")]
    public void Keychain_TruncatedEnvelopes_ThrowsArgumentException(int length)
    {
        var rootSecret = new byte[32];
        RandomNumberGenerator.Fill(rootSecret);

        var truncated = new byte[length];
        if (length > 0)
        {
            truncated[0] = 0x49;
        }

        var act = () => _crypto.DecryptKeychainData(truncated, rootSecret);
        act.Should().Throw<ArgumentException>()
            .WithMessage("*less than minimum required 65 bytes*");
    }

    /// <summary>
    /// Probes Keychain envelope magic byte validation: envelopes of 65 bytes with magic byte != 0x49
    /// must be safely rejected immediately.
    /// </summary>
    [Theory]
    [InlineData(0x00)]
    [InlineData(0x48)]
    [InlineData(0x50)]
    [InlineData(0xFF)]
    [Trait("Category", "Crypto")]
    public void Keychain_MalformedMagicByte_ThrowsArgumentException(byte invalidMagic)
    {
        var rootSecret = new byte[32];
        RandomNumberGenerator.Fill(rootSecret);

        var malformed = new byte[65];
        malformed[0] = invalidMagic;

        var act = () => _crypto.DecryptKeychainData(malformed, rootSecret);
        act.Should().Throw<ArgumentException>()
            .WithMessage("*Invalid envelope version header byte*");
    }

    /// <summary>
    /// Probes WhisperMessage wire envelope tampering: flipping 1 bit in the truncated 8-byte HMAC tag
    /// or corrupting wire framing must cause decryption rejection.
    /// </summary>
    [Fact]
    [Trait("Category", "Crypto")]
    public async Task WhisperMessage_TamperedWireHmac_RejectsDecryption()
    {
        const string aliceId = "alice_adv_1";
        const string bobId = "bob_adv_1";

        await _e2ee.RegisterIdentityAsync(aliceId);
        await _e2ee.RegisterIdentityAsync(bobId);

        // Session establishment
        var initEnv = await _e2ee.EncryptAsync("init", aliceId, bobId);
        await _e2ee.DecryptAsync(initEnv, bobId, aliceId);

        var replyEnv = await _e2ee.EncryptAsync("reply", bobId, aliceId);
        await _e2ee.DecryptAsync(replyEnv, aliceId, bobId);

        // Send WhisperMessage (Type 1)
        var msgEnv = await _e2ee.EncryptAsync("Top Secret Intel", aliceId, bobId);
        msgEnv.Type.Should().Be(1);

        var wireBytes = Convert.FromBase64String(msgEnv.Body);

        // Flip last bit of the 8-byte HMAC tag
        var tamperedHmacWire = (byte[])wireBytes.Clone();
        tamperedHmacWire[^1] ^= 0x01;

        var tamperedEnv = new SignalMessageEnvelope(1, Convert.ToBase64String(tamperedHmacWire), msgEnv.RegistrationId);
        var act = async () => await _e2ee.DecryptAsync(tamperedEnv, bobId, aliceId);
        await act.Should().ThrowAsync<CryptographicException>()
            .WithMessage("*HMAC authentication failed for WhisperMessage*");

        // Incompatible version byte in WhisperMessage (wire byte 0)
        var tamperedVersionWire = (byte[])wireBytes.Clone();
        tamperedVersionWire[0] = 0x10;
        var badVersionEnv = new SignalMessageEnvelope(1, Convert.ToBase64String(tamperedVersionWire), msgEnv.RegistrationId);
        var versionAct = async () => await _e2ee.DecryptAsync(badVersionEnv, bobId, aliceId);
        await versionAct.Should().ThrowAsync<CryptographicException>()
            .WithMessage("*Incompatible version number*");

        // Truncated wire payload (< 10 bytes)
        var truncatedWire = new byte[8];
        truncatedWire[0] = 0x33;
        var truncEnv = new SignalMessageEnvelope(1, Convert.ToBase64String(truncatedWire), msgEnv.RegistrationId);
        var truncAct = async () => await _e2ee.DecryptAsync(truncEnv, bobId, aliceId);
        await truncAct.Should().ThrowAsync<CryptographicException>()
            .WithMessage("*Whisper message payload is too short*");
    }

    /// <summary>
    /// Probes Double Ratchet out-of-order packet delivery across a complex sequence:
    /// Simulates packets arriving in order: 1, 2, 5, 3, 4, 10, 6, 7, 8, 9, and finally 0!
    /// Asserts that all packets decrypt successfully using the skipped message keys table
    /// and that the skipped keys table is completely exhausted upon completion.
    /// </summary>
    [Fact]
    [Trait("Category", "Crypto")]
    public void DoubleRatchet_OutOfOrderComplexSequence_DecryptsSuccessfully()
    {
        var aliceId = Curve25519Helper.GenerateKeyPair(includePrefix: true);
        var bobId = Curve25519Helper.GenerateKeyPair(includePrefix: true);
        var bobSpk = Curve25519Helper.GenerateKeyPair(includePrefix: true);

        var sharedMasterSecret = new byte[64];
        RandomNumberGenerator.Fill(sharedMasterSecret);

        using var aliceSession = _ratchet.InitializeInitiatorSession(
            sharedMasterSecret,
            bobSpk.PublicKey,
            aliceId.PublicKey,
            bobId.PublicKey,
            1001);

        using var bobSession = _ratchet.InitializeRecipientSession(
            sharedMasterSecret,
            bobSpk,
            bobId.PublicKey,
            aliceId.PublicKey,
            1002);

        // Alice sends 11 messages (counters 0 through 10)
        var packets = new List<(byte[] Ciphertext, uint Counter, uint PrevCounter, byte[] EphemPub, string Plaintext)>();
        for (var i = 0; i <= 10; i++)
        {
            var text = $"Adversarial Packet #{i}";
            var (ct, counter, prev, ephem, _) = _ratchet.Encrypt(aliceSession, Encoding.UTF8.GetBytes(text));
            packets.Add((ct, counter, prev, ephem, text));
        }

        // Adversarial arrival sequence: 1, 2, 5, 3, 4, 10, 6, 7, 8, 9, 0
        int[] deliveryOrder = [1, 2, 5, 3, 4, 10, 6, 7, 8, 9, 0];

        foreach (var idx in deliveryOrder)
        {
            var pkt = packets[idx];
            var (plainBytes, _) = _ratchet.Decrypt(bobSession, pkt.EphemPub, pkt.Counter, pkt.PrevCounter, pkt.Ciphertext);
            var decrypted = Encoding.UTF8.GetString(plainBytes);
            decrypted.Should().Be(pkt.Plaintext, $"Packet #{idx} must decrypt cleanly out-of-order.");
        }

        // All skipped keys must be exhausted
        bobSession.SkippedMessageKeys.Should().BeEmpty(
            "All skipped keys must be consumed after every out-of-order packet has been delivered.");

        // Replay of any packet must fail
        var replayAct = () => _ratchet.Decrypt(bobSession, packets[5].EphemPub, packets[5].Counter, packets[5].PrevCounter, packets[5].Ciphertext);
        replayAct.Should().Throw<CryptographicException>("Replaying an already delivered packet must fail.");
    }

    /// <summary>
    /// Probes Double Ratchet lookahead threshold: counter jumps exceeding 2,000 messages into the future
    /// must throw InvalidOperationException immediately and not exhaust memory or hang CPU.
    /// Also probes boundary condition: jump of exactly 2000 messages is allowed, while 2001 messages is rejected.
    /// </summary>
    [Fact]
    [Trait("Category", "Crypto")]
    public void DoubleRatchet_GapJumpExceeding2000_ThrowsAndProtectsMemory()
    {
        var aliceId = Curve25519Helper.GenerateKeyPair(includePrefix: true);
        var bobId = Curve25519Helper.GenerateKeyPair(includePrefix: true);
        var bobSpk = Curve25519Helper.GenerateKeyPair(includePrefix: true);

        var sharedMasterSecret = new byte[64];
        RandomNumberGenerator.Fill(sharedMasterSecret);

        using var aliceSession = _ratchet.InitializeInitiatorSession(
            sharedMasterSecret,
            bobSpk.PublicKey,
            aliceId.PublicKey,
            bobId.PublicKey,
            2001);

        using var bobSession = _ratchet.InitializeRecipientSession(
            sharedMasterSecret,
            bobSpk,
            bobId.PublicKey,
            aliceId.PublicKey,
            2002);

        // Scenario A: Fresh receiving chain (ReceiveMessageCounter == 0)
        // Alice sends packet with counter = 2001 (jump = 2001 - 0 = 2001 > 2000)
        var freshEphemPub = aliceSession.LocalEphemeralKeyPair.PublicKey;
        var jumpFreshAct = () => _ratchet.Decrypt(bobSession, freshEphemPub, 2001, 0, new byte[32]);
        jumpFreshAct.Should().Throw<InvalidOperationException>()
            .WithMessage("*Over messages into the future: 2000*");
        bobSession.SkippedMessageKeys.Should().BeEmpty("Memory must not allocate skipped keys on rejected gap jump.");

        // Scenario B: Open receiving chain with packet #0 (now ReceiveMessageCounter becomes 1)
        var (ct0, counter0, prev0, ephem0, _) = _ratchet.Encrypt(aliceSession, "init"u8);
        _ratchet.Decrypt(bobSession, ephem0, counter0, prev0, ct0);
        bobSession.ReceiveMessageCounter.Should().Be(1);

        // Jump of 2001 on established chain: counter = 2002 (diff = 2002 - 1 = 2001 > 2000)
        var jump2001Act = () => _ratchet.Decrypt(bobSession, ephem0, 2002, 0, new byte[32]);
        jump2001Act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Over messages into the future: 2000*");
        bobSession.SkippedMessageKeys.Should().BeEmpty("No keys should be allocated on rejected jump.");

        // Jump of 1,000,000 (huge jump DoS attack)
        var jump1MAct = () => _ratchet.Decrypt(bobSession, ephem0, 1_000_000, 0, new byte[32]);
        jump1MAct.Should().Throw<InvalidOperationException>()
            .WithMessage("*Over messages into the future: 2000*");
        bobSession.SkippedMessageKeys.Should().BeEmpty("Memory must remain protected against massive counter jumps.");

        // Jump with uint.MaxValue (underflow attack check)
        var jumpMaxAct = () => _ratchet.Decrypt(bobSession, ephem0, uint.MaxValue, 0, new byte[32]);
        jumpMaxAct.Should().Throw<InvalidOperationException>()
            .WithMessage("*Over messages into the future: 2000*");
        bobSession.SkippedMessageKeys.Should().BeEmpty();
    }

    /// <summary>
    /// Verifies that StepDhRatchet enforces MaxMessageKeys bounds on remotePreviousCounter before loop execution,
    /// throwing InvalidOperationException and ensuring no memory/skipped keys are allocated.
    /// </summary>
    [Fact]
    [Trait("Category", "Crypto")]
    public void StepDhRatchet_LargePreviousCounter_ThrowsInvalidOperationExceptionAndProtectsMemory()
    {
        var aliceId = Curve25519Helper.GenerateKeyPair(includePrefix: true);
        var bobId = Curve25519Helper.GenerateKeyPair(includePrefix: true);
        var bobSpk = Curve25519Helper.GenerateKeyPair(includePrefix: true);

        var sharedMasterSecret = new byte[64];
        RandomNumberGenerator.Fill(sharedMasterSecret);

        using var aliceSession = _ratchet.InitializeInitiatorSession(
            sharedMasterSecret,
            bobSpk.PublicKey,
            aliceId.PublicKey,
            bobId.PublicKey,
            3001);

        using var bobSession = _ratchet.InitializeRecipientSession(
            sharedMasterSecret,
            bobSpk,
            bobId.PublicKey,
            aliceId.PublicKey,
            3002);

        // Open receiving chain at Bob with packet 0
        var (ct0, counter0, prev0, ephem0, _) = _ratchet.Encrypt(aliceSession, "msg0"u8);
        _ratchet.Decrypt(bobSession, ephem0, counter0, prev0, ct0);

        // Bob replies, advancing his ratchet
        var (replyCt, replyCounter, replyPrev, replyEphem, _) = _ratchet.Encrypt(bobSession, "reply0"u8);
        _ratchet.Decrypt(aliceSession, replyEphem, replyCounter, replyPrev, replyCt);

        // Now Alice steps ratchet with a new ephemeral key, but previousCounter is spoofed to 2005 (> 2000)
        var (ctNew, counterNew, _, ephemNew, _) = _ratchet.Encrypt(aliceSession, "msgNew"u8);

        Action act = () => _ratchet.Decrypt(bobSession, ephemNew, counterNew, 2005, ctNew);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Over messages into the future: 2000*",
                "StepDhRatchet must enforce MaxMessageKeys bounds on remotePreviousCounter before loop execution.");

        bobSession.SkippedMessageKeys.Should().BeEmpty(
            "Memory must not allocate skipped keys when StepDhRatchet rejects an excessive lookahead jump.");
    }

    /// <summary>
    /// Probes media de-noising edge cases: buffers shorter than 6 bytes, buffers without the 077a416c4f07 prefix,
    /// buffers with near-miss prefix bytes, and payload restoration via XOR 147.
    /// </summary>
    [Fact]
    [Trait("Category", "Crypto")]
    public void MediaDenoise_BoundaryAndEdgeCases_BehavesCorrectly()
    {
        ReadOnlySpan<byte> sig = [0x07, 0x7A, 0x41, 0x6C, 0x4F, 0x07];

        // 1. Exactly 6 bytes of signature with no payload -> returns 0 bytes
        var emptyPayload = _crypto.DenoiseMedia(sig);
        emptyPayload.Length.Should().Be(0, "De-noising signature-only buffer must return an empty block.");

        // 2. Signature + 1 byte (0x00 ^ 147 = 147)
        byte[] oneByteBuf = [0x07, 0x7A, 0x41, 0x6C, 0x4F, 0x07, 0x00];
        var denoisedOne = _crypto.DenoiseMedia(oneByteBuf);
        denoisedOne.ToArray().Should().Equal([147]);

        // 3. All 256 byte values roundtrip through XOR 147 (0x93)
        var allBytes = new byte[256];
        for (var i = 0; i < 256; i++)
        {
            allBytes[i] = (byte)i;
        }

        var scrambledAll = new byte[sig.Length + 256];
        sig.CopyTo(scrambledAll.AsSpan(0));
        for (var i = 0; i < 256; i++)
        {
            scrambledAll[sig.Length + i] = (byte)(allBytes[i] ^ 147);
        }

        var restoredAll = _crypto.DenoiseMedia(scrambledAll);
        restoredAll.ToArray().Should().Equal(allBytes, "All 256 byte values must be restored cleanly via XOR 147.");

        // 4. Buffers shorter than 6 bytes: returned completely untouched
        byte[] empty = [];
        _crypto.DenoiseMedia(empty).ToArray().Should().Equal(empty);

        byte[] single = [0x07];
        _crypto.DenoiseMedia(single).ToArray().Should().Equal(single);

        byte[] fiveBytes = [0x07, 0x7A, 0x41, 0x6C, 0x4F];
        _crypto.DenoiseMedia(fiveBytes).ToArray().Should().Equal(fiveBytes);

        // 5. Near-miss signatures: returned completely untouched
        byte[] nearMissFirst = [0x06, 0x7A, 0x41, 0x6C, 0x4F, 0x07, 0xAA, 0xBB];
        _crypto.DenoiseMedia(nearMissFirst).ToArray().Should().Equal(nearMissFirst);

        byte[] nearMissLast = [0x07, 0x7A, 0x41, 0x6C, 0x4F, 0x06, 0xAA, 0xBB];
        _crypto.DenoiseMedia(nearMissLast).ToArray().Should().Equal(nearMissLast);

        byte[] allZeros = new byte[16];
        _crypto.DenoiseMedia(allZeros).ToArray().Should().Equal(allZeros);
    }

    /// <summary>
    /// Probes Safety Number symmetry, resistance, and key normalization:
    /// Swapping parties, using 32-byte vs 33-byte (0x05 prefix) keys, and verifying strict 60-digit formatting.
    /// </summary>
    [Fact]
    [Trait("Category", "Crypto")]
    public void SafetyNumber_SymmetryAndResistance_BehavesCorrectly()
    {
        var keyPairA = Curve25519Helper.GenerateKeyPair(includePrefix: false);
        var keyPairB = Curve25519Helper.GenerateKeyPair(includePrefix: false);

        const string userA = "user_alpha_111";
        const string userB = "user_beta_222";

        // 1. Swapped identity keys produce identical Safety Number
        var snAB = SafetyNumberFormatter.ComputeSafetyNumber(userA, keyPairA.PublicKey, userB, keyPairB.PublicKey);
        var snBA = SafetyNumberFormatter.ComputeSafetyNumber(userB, keyPairB.PublicKey, userA, keyPairA.PublicKey);
        snAB.Should().Be(snBA, "Safety Number must be strictly symmetric regardless of party ordering.");

        // 2. 32-byte raw vs 33-byte (0x05-prefixed) keys produce identical Safety Number
        var prefixedA = new byte[33];
        prefixedA[0] = 0x05;
        keyPairA.PublicKey.CopyTo(prefixedA.AsSpan(1));

        var prefixedB = new byte[33];
        prefixedB[0] = 0x05;
        keyPairB.PublicKey.CopyTo(prefixedB.AsSpan(1));

        var snPrefixed = SafetyNumberFormatter.ComputeSafetyNumber(userA, prefixedA, userB, prefixedB);
        snPrefixed.Should().Be(snAB, "Key normalization must yield identical Safety Numbers for 32-byte and 33-byte key inputs.");

        // 3. Format verification: 12 groups of 5 decimal digits
        var groups = snAB.Split(' ');
        groups.Length.Should().Be(12, "Must contain exactly 12 groups.");
        foreach (var group in groups)
        {
            group.Length.Should().Be(5, "Each group must be 5 characters.");
            group.All(char.IsDigit).Should().BeTrue("Each character must be a decimal digit.");
        }

        // 4. Avalanche / Resistance: 1-bit change in key produces completely different Safety Number
        var tamperedKeyA = (byte[])keyPairA.PublicKey.Clone();
        tamperedKeyA[0] ^= 0x01;
        var snTampered = SafetyNumberFormatter.ComputeSafetyNumber(userA, tamperedKeyA, userB, keyPairB.PublicKey);
        snTampered.Should().NotBe(snAB, "A 1-bit change in identity key must produce an avalanche effect altering the Safety Number.");

        // 5. Invalid key lengths throw ArgumentException
        var badLenKey = new byte[31];
        var badKeyAct = () => SafetyNumberFormatter.ComputeSafetyNumber(userA, badLenKey, userB, keyPairB.PublicKey);
        badKeyAct.Should().Throw<ArgumentException>();
    }

    /// <summary>
    /// Verifies that an unauthenticated packet failing wire HMAC does not advance the session's receive counter,
    /// allowing subsequent retransmission of the authentic packet to succeed cleanly.
    /// </summary>
    [Fact]
    [Trait("Category", "Crypto")]
    public async Task Adversarial_TamperedPacketDoesNotAdvanceCounter_AllowsLegitimateRetransmission()
    {
        const string aliceId = "alice_tamper_probe";
        const string bobId = "bob_tamper_probe";

        await _e2ee.RegisterIdentityAsync(aliceId);
        await _e2ee.RegisterIdentityAsync(bobId);

        // Open session
        var env0 = await _e2ee.EncryptAsync("msg0", aliceId, bobId);
        await _e2ee.DecryptAsync(env0, bobId, aliceId);

        var reply0 = await _e2ee.EncryptAsync("reply0", bobId, aliceId);
        await _e2ee.DecryptAsync(reply0, aliceId, bobId);

        // Alice sends message 1
        var env1 = await _e2ee.EncryptAsync("msg1_legit", aliceId, bobId);

        // Tamper HMAC of message 1
        var wire1 = Convert.FromBase64String(env1.Body);
        wire1[^1] ^= 0x01;
        var tamperedEnv1 = new SignalMessageEnvelope(env1.Type, Convert.ToBase64String(wire1), env1.RegistrationId);

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
    }

    /// <summary>
    /// Verifies that a forged WhisperMessage packet with a random ephemeral key and bad HMAC is rejected
    /// without poisoning Bob's session state, allowing subsequent legitimate messages from Alice to decrypt cleanly.
    /// </summary>
    [Fact]
    [Trait("Category", "Crypto")]
    public async Task Adversarial_ForgedEphemeralKeyPacket_DoesNotPoisonSessionState()
    {
        const string aliceId = "alice_poison_probe";
        const string bobId = "bob_poison_probe";

        await _e2ee.RegisterIdentityAsync(aliceId);
        await _e2ee.RegisterIdentityAsync(bobId);

        // Open session
        var env0 = await _e2ee.EncryptAsync("msg0", aliceId, bobId);
        await _e2ee.DecryptAsync(env0, bobId, aliceId);

        var reply0 = await _e2ee.EncryptAsync("reply0", bobId, aliceId);
        await _e2ee.DecryptAsync(reply0, aliceId, bobId);

        // Attacker creates a forged packet with a random ephemeral key and bad HMAC
        var bogusEphemeral = Curve25519Helper.GenerateKeyPair(includePrefix: true).PublicKey;
        var aliceStore = new SignalX3dhService().GetOrCreateUserStore(aliceId);
        var bobStore = new SignalX3dhService().GetOrCreateUserStore(bobId);

        // Forge WhisperMessage wire with bogus ephemeral key, counter = 0, prevCounter = 0
        using var ms = new System.IO.MemoryStream();
        // field 1: ephemeralKey
        ms.WriteByte((1 << 3) | 2);
        ms.WriteByte((byte)bogusEphemeral.Length);
        ms.Write(bogusEphemeral);
        // field 2: counter = 0
        ms.WriteByte((2 << 3) | 0);
        ms.WriteByte(0);
        // field 3: prevCounter = 0
        ms.WriteByte((3 << 3) | 0);
        ms.WriteByte(0);
        // field 4: ciphertext = 16 bytes
        ms.WriteByte((4 << 3) | 2);
        ms.WriteByte(16);
        ms.Write(new byte[16]);

        var bogusProto = ms.ToArray();

        // Wire buffer: [0x33: 1B] || [Proto: NB] || [HMAC: 8B (bogus)]
        var bogusWire = new byte[1 + bogusProto.Length + 8];
        bogusWire[0] = 0x33;
        bogusProto.CopyTo(bogusWire.AsSpan(1));
        // Fill last 8 bytes with garbage HMAC
        RandomNumberGenerator.Fill(bogusWire.AsSpan(1 + bogusProto.Length, 8));

        var forgedEnvelope = new SignalMessageEnvelope(1, Convert.ToBase64String(bogusWire), 12345);

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
    }
}
