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

    /// <summary>
    /// Probes Double Ratchet and X3DH handshake resilience against forged Type 3 PreKeyWhisperMessage packets:
    /// An attacker attempting to hijack an established session by delivering a bogus Type 3 handshake
    /// must fail inner wire HMAC authentication and leave the existing session completely unmodified.
    /// </summary>
    [Fact]
    [Trait("Category", "Crypto")]
    public async Task Adversarial_ForgedType3Handshake_DoesNotCorruptOrHijackExistingSession()
    {
        const string aliceId = "alice_type3_probe";
        const string bobId = "bob_type3_probe";

        await _e2ee.RegisterIdentityAsync(aliceId);
        await _e2ee.RegisterIdentityAsync(bobId);

        // 1. Establish authentic session
        var initEnv = await _e2ee.EncryptAsync("authentic_init", aliceId, bobId);
        var initDecrypted = await _e2ee.DecryptAsync(initEnv, bobId, aliceId);
        initDecrypted.Should().Be("authentic_init");

        var replyEnv = await _e2ee.EncryptAsync("authentic_reply", bobId, aliceId);
        await _e2ee.DecryptAsync(replyEnv, aliceId, bobId);

        // 2. Attacker crafts a forged Type 3 PreKeyWhisperMessage
        var attackerBaseKey = Curve25519Helper.GenerateKeyPair(includePrefix: true).PublicKey;
        var attackerIdKey = Curve25519Helper.GenerateKeyPair(includePrefix: true).PublicKey;
        var bogusEphemeral = Curve25519Helper.GenerateKeyPair(includePrefix: true).PublicKey;

        // Valid inner WhisperMessage protobuf structure with bad HMAC
        using var innerMs = new System.IO.MemoryStream();
        innerMs.WriteByte((1 << 3) | 2);
        innerMs.WriteByte((byte)bogusEphemeral.Length);
        innerMs.Write(bogusEphemeral);
        innerMs.WriteByte((2 << 3) | 0);
        innerMs.WriteByte(0);
        innerMs.WriteByte((3 << 3) | 0);
        innerMs.WriteByte(0);
        innerMs.WriteByte((4 << 3) | 2);
        innerMs.WriteByte(16);
        innerMs.Write(new byte[16]);

        var innerProto = innerMs.ToArray();
        var bogusWire = new byte[1 + innerProto.Length + 8];
        bogusWire[0] = 0x33;
        innerProto.CopyTo(bogusWire.AsSpan(1));
        RandomNumberGenerator.Fill(bogusWire.AsSpan(1 + innerProto.Length, 8));

        using var ms = new System.IO.MemoryStream();
        // field 1: registrationId
        ms.WriteByte((1 << 3) | 0);
        ms.WriteByte(99);
        // field 2: baseKey
        ms.WriteByte((2 << 3) | 2);
        ms.WriteByte((byte)attackerBaseKey.Length);
        ms.Write(attackerBaseKey);
        // field 3: identityKey
        ms.WriteByte((3 << 3) | 2);
        ms.WriteByte((byte)attackerIdKey.Length);
        ms.Write(attackerIdKey);
        // field 4: message (inner WhisperMessage wire)
        ms.WriteByte((4 << 3) | 2);
        ms.WriteByte((byte)bogusWire.Length);
        ms.Write(bogusWire);

        var preKeyProto = ms.ToArray();
        var forgedEnvelopeBytes = new byte[1 + preKeyProto.Length];
        forgedEnvelopeBytes[0] = 0x33;
        preKeyProto.CopyTo(forgedEnvelopeBytes.AsSpan(1));

        var forgedType3Env = new SignalMessageEnvelope(3, Convert.ToBase64String(forgedEnvelopeBytes), 9999);

        // Bob receives attacker's forged Type 3 packet -> must throw CryptographicException on inner wire HMAC
        var actForged = async () => await _e2ee.DecryptAsync(forgedType3Env, bobId, aliceId);
        await actForged.Should().ThrowAsync<CryptographicException>()
            .WithMessage("*HMAC authentication failed for WhisperMessage*");

        // 3. Alice sends next authentic Type 1 message
        var legitEnv = await _e2ee.EncryptAsync("authentic_message_post_type3_attack", aliceId, bobId);
        legitEnv.Type.Should().Be(1);

        // Bob MUST decrypt Alice's message cleanly using the un-hijacked session
        var decrypted = await _e2ee.DecryptAsync(legitEnv, bobId, aliceId);
        decrypted.Should().Be("authentic_message_post_type3_attack",
            "Bob's session with Alice must not be hijacked or corrupted by an attacker's failed Type 3 handshake.");
    }

    /// <summary>
    /// Probes continuous bombardment attack: 25 cycles of an attacker injecting forged packets
    /// with bogus ephemeral keys and invalid HMACs, interleaved with legitimate bidirectional messages.
    /// Asserts 100% session synchronization and zero message loss under persistent adversarial noise.
    /// </summary>
    [Fact]
    [Trait("Category", "Crypto")]
    public async Task Adversarial_ContinuousBombardment_InterleavedWithLegitimateTraffic_PreservesIntegrity()
    {
        const string aliceId = "alice_bombard_probe";
        const string bobId = "bob_bombard_probe";

        await _e2ee.RegisterIdentityAsync(aliceId);
        await _e2ee.RegisterIdentityAsync(bobId);

        // Establish session
        var env0 = await _e2ee.EncryptAsync("handshake", aliceId, bobId);
        await _e2ee.DecryptAsync(env0, bobId, aliceId);

        var reply0 = await _e2ee.EncryptAsync("handshake_reply", bobId, aliceId);
        await _e2ee.DecryptAsync(reply0, aliceId, bobId);

        for (var i = 1; i <= 25; i++)
        {
            // 1. Attacker delivers forged packet with random ephemeral key
            var bogusEphemeral = Curve25519Helper.GenerateKeyPair(includePrefix: true).PublicKey;
            using var ms = new System.IO.MemoryStream();
            ms.WriteByte((1 << 3) | 2);
            ms.WriteByte((byte)bogusEphemeral.Length);
            ms.Write(bogusEphemeral);
            ms.WriteByte((2 << 3) | 0);
            ms.WriteByte((byte)i);
            ms.WriteByte((3 << 3) | 0);
            ms.WriteByte(0);
            ms.WriteByte((4 << 3) | 2);
            ms.WriteByte(16);
            ms.Write(new byte[16]);

            var bogusProto = ms.ToArray();
            var bogusWire = new byte[1 + bogusProto.Length + 8];
            bogusWire[0] = 0x33;
            bogusProto.CopyTo(bogusWire.AsSpan(1));
            RandomNumberGenerator.Fill(bogusWire.AsSpan(1 + bogusProto.Length, 8));

            var forgedEnv = new SignalMessageEnvelope(1, Convert.ToBase64String(bogusWire), 8888 + i);
            var act = async () => await _e2ee.DecryptAsync(forgedEnv, bobId, aliceId);
            await act.Should().ThrowAsync<CryptographicException>();

            // 2. Alice sends legitimate message
            var aliceText = $"Alice_Legit_{i}";
            var aliceEnv = await _e2ee.EncryptAsync(aliceText, aliceId, bobId);
            var bobReceived = await _e2ee.DecryptAsync(aliceEnv, bobId, aliceId);
            bobReceived.Should().Be(aliceText);

            // 3. Bob sends legitimate reply
            var bobText = $"Bob_Reply_{i}";
            var bobEnv = await _e2ee.EncryptAsync(bobText, bobId, aliceId);
            var aliceReceived = await _e2ee.DecryptAsync(bobEnv, aliceId, bobId);
            aliceReceived.Should().Be(bobText);
        }
    }

    /// <summary>
    /// Probes out-of-order delivery through the full ISignalE2eeService stack (including state cloning,
    /// wire HMAC verification, candidate commit, and AES decryption).
    /// Asserts all out-of-order messages decrypt cleanly and replaying any fails.
    /// </summary>
    [Fact]
    [Trait("Category", "Crypto")]
    public async Task Adversarial_OutOfOrderDelivery_ThroughE2eeService_DecryptsAllCleanly()
    {
        const string aliceId = "alice_ooo_stack";
        const string bobId = "bob_ooo_stack";

        await _e2ee.RegisterIdentityAsync(aliceId);
        await _e2ee.RegisterIdentityAsync(bobId);

        // Handshake
        var h1 = await _e2ee.EncryptAsync("init", aliceId, bobId);
        await _e2ee.DecryptAsync(h1, bobId, aliceId);
        var h2 = await _e2ee.EncryptAsync("reply", bobId, aliceId);
        await _e2ee.DecryptAsync(h2, aliceId, bobId);

        // Alice generates 10 sequential messages
        var envelopes = new List<SignalMessageEnvelope>();
        var plaintexts = new List<string>();
        for (var i = 0; i < 10; i++)
        {
            var text = $"Message_{i}";
            plaintexts.Add(text);
            envelopes.Add(await _e2ee.EncryptAsync(text, aliceId, bobId));
        }

        // Adversarial shuffled arrival order: 3, 1, 0, 4, 7, 2, 5, 9, 8, 6
        int[] shuffle = [3, 1, 0, 4, 7, 2, 5, 9, 8, 6];
        foreach (var idx in shuffle)
        {
            var decrypted = await _e2ee.DecryptAsync(envelopes[idx], bobId, aliceId);
            decrypted.Should().Be(plaintexts[idx], $"Out-of-order message #{idx} must decrypt cleanly.");
        }

        // Replay of already delivered message must fail
        var replayAct = async () => await _e2ee.DecryptAsync(envelopes[4], bobId, aliceId);
        await replayAct.Should().ThrowAsync<CryptographicException>();
    }

    /// <summary>
    /// Probes exact boundary enforcement in StepDhRatchet:
    /// A jump of exactly 2000 skipped keys (2000 - 0 = 2000) must succeed and allocate 2000 keys.
    /// A jump of 2001 skipped keys (2001 - 0 = 2001) must throw InvalidOperationException and allocate 0 keys.
    /// </summary>
    [Fact]
    [Trait("Category", "Crypto")]
    public void StepDhRatchet_ExactBoundary_2000Allowed_2001Rejected()
    {
        var aliceId = Curve25519Helper.GenerateKeyPair(includePrefix: true);
        var bobId = Curve25519Helper.GenerateKeyPair(includePrefix: true);
        var bobSpk = Curve25519Helper.GenerateKeyPair(includePrefix: true);

        var sharedMasterSecret = new byte[64];
        RandomNumberGenerator.Fill(sharedMasterSecret);

        // Boundary test 1: Jump of exactly 2000 is allowed
        using var aliceSession1 = _ratchet.InitializeInitiatorSession(
            sharedMasterSecret, bobSpk.PublicKey, aliceId.PublicKey, bobId.PublicKey, 4001);
        using var bobSession1 = _ratchet.InitializeRecipientSession(
            sharedMasterSecret, bobSpk, bobId.PublicKey, aliceId.PublicKey, 4002);

        var (ct0, c0, p0, ephem0, _) = _ratchet.Encrypt(aliceSession1, "init"u8);
        _ratchet.Decrypt(bobSession1, ephem0, c0, p0, ct0);

        var (repCt, repC, repP, repEphem, _) = _ratchet.Encrypt(bobSession1, "reply"u8);
        _ratchet.Decrypt(aliceSession1, repEphem, repC, repP, repCt);

        // Alice steps ratchet with previousCounter = 2001 (diff = 2001 - 1 = 2000 <= 2000)
        var targetPrevCounter2000 = bobSession1.ReceiveMessageCounter + 2000;
        var (ctBound2000, cBound2000, _, ephemBound2000, _) = _ratchet.Encrypt(aliceSession1, "boundary2000"u8);
        var (plain2000, _) = _ratchet.Decrypt(bobSession1, ephemBound2000, cBound2000, targetPrevCounter2000, ctBound2000);
        Encoding.UTF8.GetString(plain2000).Should().Be("boundary2000");
        bobSession1.SkippedMessageKeys.Count.Should().Be(2000, "Exactly 2000 skipped keys should be allocated.");

        // Boundary test 2: Jump of 2001 is rejected
        using var aliceSession2 = _ratchet.InitializeInitiatorSession(
            sharedMasterSecret, bobSpk.PublicKey, aliceId.PublicKey, bobId.PublicKey, 4003);
        using var bobSession2 = _ratchet.InitializeRecipientSession(
            sharedMasterSecret, bobSpk, bobId.PublicKey, aliceId.PublicKey, 4004);

        var (ct0B, c0B, p0B, ephem0B, _) = _ratchet.Encrypt(aliceSession2, "initB"u8);
        _ratchet.Decrypt(bobSession2, ephem0B, c0B, p0B, ct0B);

        var (repCtB, repCB, repPB, repEphemB, _) = _ratchet.Encrypt(bobSession2, "replyB"u8);
        _ratchet.Decrypt(aliceSession2, repEphemB, repCB, repPB, repCtB);

        // Alice steps ratchet with previousCounter = 2002 (diff = 2002 - 1 = 2001 > 2000)
        var targetPrevCounter2001 = bobSession2.ReceiveMessageCounter + 2001;
        var (ctBound2001, cBound2001, _, ephemBound2001, _) = _ratchet.Encrypt(aliceSession2, "boundary2001"u8);
        var act2001 = () => _ratchet.Decrypt(bobSession2, ephemBound2001, cBound2001, targetPrevCounter2001, ctBound2001);
        act2001.Should().Throw<InvalidOperationException>()
            .WithMessage("*Over messages into the future: 2000*");
        bobSession2.SkippedMessageKeys.Should().BeEmpty("Memory must remain empty on rejected boundary jump.");
    }

    /// <summary>
    /// Probes retransmission of a packet that steps the DH ratchet:
    /// When a message with a new remote ephemeral key has its wire HMAC corrupted in transit,
    /// Bob's live session must not step its DH ratchet or mutate its root key.
    /// Retransmission of the authentic stepped packet must then succeed and step the ratchet cleanly.
    /// </summary>
    [Fact]
    [Trait("Category", "Crypto")]
    public async Task Adversarial_SteppedDhRetransmission_AllowsLegitimatePacketAfterCorruption()
    {
        const string aliceId = "alice_step_retrans";
        const string bobId = "bob_step_retrans";

        await _e2ee.RegisterIdentityAsync(aliceId);
        await _e2ee.RegisterIdentityAsync(bobId);

        // 1. Establish session
        var h1 = await _e2ee.EncryptAsync("init", aliceId, bobId);
        await _e2ee.DecryptAsync(h1, bobId, aliceId);

        // 2. Bob replies (Bob steps sending ratchet)
        var h2 = await _e2ee.EncryptAsync("reply_from_bob", bobId, aliceId);
        await _e2ee.DecryptAsync(h2, aliceId, bobId);

        // 3. Alice sends a new message (this steps DH ratchet because Alice replies to Bob's new ephemeral key)
        var aliceSteppedEnv = await _e2ee.EncryptAsync("alice_stepped_message", aliceId, bobId);

        // Corrupt HMAC in transit
        var wireBytes = Convert.FromBase64String(aliceSteppedEnv.Body);
        wireBytes[^1] ^= 0xFF;
        var corruptedEnv = new SignalMessageEnvelope(aliceSteppedEnv.Type, Convert.ToBase64String(wireBytes), aliceSteppedEnv.RegistrationId);

        // Bob attempts to decrypt corrupted stepped packet -> must fail HMAC
        var decryptAct = async () => await _e2ee.DecryptAsync(corruptedEnv, bobId, aliceId);
        await decryptAct.Should().ThrowAsync<CryptographicException>()
            .WithMessage("*HMAC authentication failed for WhisperMessage*");

        // 4. Alice retransmits the authentic stepped packet -> must succeed!
        var retransmitted = await _e2ee.DecryptAsync(aliceSteppedEnv, bobId, aliceId);
        retransmitted.Should().Be("alice_stepped_message",
            "Retransmission of a stepped DH packet after in-transit corruption must succeed because Bob's live DH ratchet was not poisoned.");

        // 5. Subsequent message on that new chain decrypts cleanly
        var nextAliceEnv = await _e2ee.EncryptAsync("alice_next_msg", aliceId, bobId);
        var decryptedNext = await _e2ee.DecryptAsync(nextAliceEnv, bobId, aliceId);
        decryptedNext.Should().Be("alice_next_msg");
    }
}
