// Copyright (c) Zenix. All rights reserved.
// Licensed under the proprietary and confidential license.
// Unauthorized copying, distribution, modification, or disclosure of this file,
// via any medium, is strictly prohibited without prior written permission.

using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using FluentAssertions;
using Xunit;
using Zenix.Core.Zalo.Cryptography;

namespace Zenix.Tests.Zalo.Crypto;

/// <summary>
/// Unit tests for the Signal Double Ratchet algorithm, symmetric chain advances,
/// asymmetric DH ratchet stepping, out-of-order packet delivery, and skipped key store limits.
/// </summary>
public sealed class DoubleRatchetTests
{
    private readonly SignalDoubleRatchetService _ratchet = new();

    /// <summary>
    /// Simulates 100 sequential ratchet steps between Alice and Bob, testing bidirectional exchanges.
    /// </summary>
    [Fact]
    [Trait("Category", "Crypto")]
    public void DoubleRatchet_SimulatesSequentialRatchetSteps()
    {
        var aliceId = Curve25519Helper.GenerateKeyPair(includePrefix: true);
        var bobId = Curve25519Helper.GenerateKeyPair(includePrefix: true);
        var bobSpk = Curve25519Helper.GenerateKeyPair(includePrefix: true);

        var sharedMasterSecret = new byte[64];
        RandomNumberGenerator.Fill(sharedMasterSecret);

        // Initialize sessions
        using var aliceSession = _ratchet.InitializeInitiatorSession(
            sharedMasterSecret,
            bobSpk.PublicKey,
            aliceId.PublicKey,
            bobId.PublicKey,
            1234);

        using var bobSession = _ratchet.InitializeRecipientSession(
            sharedMasterSecret,
            bobSpk,
            bobId.PublicKey,
            aliceId.PublicKey,
            5678);

        // Perform 100 ping-pong rounds
        for (var step = 0; step < 100; step++)
        {
            // Alice -> Bob
            var msgAlice = $"Message from Alice to Bob, round #{step}";
            var (cipherA, counterA, prevA, ephemA, _) = _ratchet.Encrypt(aliceSession, Encoding.UTF8.GetBytes(msgAlice));

            var (plainB, _) = _ratchet.Decrypt(bobSession, ephemA, counterA, prevA, cipherA);
            Encoding.UTF8.GetString(plainB).Should().Be(msgAlice, $"Bob must cleanly decrypt message #{step} from Alice.");

            // Bob -> Alice
            var msgBob = $"Reply from Bob to Alice, round #{step}";
            var (cipherB, counterB, prevB, ephemB, _) = _ratchet.Encrypt(bobSession, Encoding.UTF8.GetBytes(msgBob));

            var (plainA, _) = _ratchet.Decrypt(aliceSession, ephemB, counterB, prevB, cipherB);
            Encoding.UTF8.GetString(plainA).Should().Be(msgBob, $"Alice must cleanly decrypt reply #{step} from Bob.");
        }
    }

    /// <summary>
    /// Verifies that out-of-order packet delivery succeeds without decryption errors
    /// by using the skipped message keys table, and rejects packet replays.
    /// </summary>
    [Fact]
    [Trait("Category", "Crypto")]
    public void DoubleRatchet_OutOfOrderPackets_DecryptedUsingSkippedKeysTable()
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
            1234);

        using var bobSession = _ratchet.InitializeRecipientSession(
            sharedMasterSecret,
            bobSpk,
            bobId.PublicKey,
            aliceId.PublicKey,
            5678);

        // Alice sends 10 messages without waiting for Bob
        var packets = new List<(byte[] Ciphertext, uint Counter, uint PrevCounter, byte[] EphemPub, string Plaintext)>();

        for (var i = 0; i < 10; i++)
        {
            var text = $"Packet #{i}";
            var (ct, counter, prev, ephem, _) = _ratchet.Encrypt(aliceSession, Encoding.UTF8.GetBytes(text));
            packets.Add((ct, counter, prev, ephem, text));
        }

        // Deliver packet #0 first (establishes Bob's receiving chain)
        var (p0, _) = _ratchet.Decrypt(bobSession, packets[0].EphemPub, packets[0].Counter, packets[0].PrevCounter, packets[0].Ciphertext);
        Encoding.UTF8.GetString(p0).Should().Be(packets[0].Plaintext);

        // Now deliver packet #5 before packets #1, #2, #3, #4!
        var (p5, _) = _ratchet.Decrypt(bobSession, packets[5].EphemPub, packets[5].Counter, packets[5].PrevCounter, packets[5].Ciphertext);
        Encoding.UTF8.GetString(p5).Should().Be(packets[5].Plaintext, "Bob must step chain forward and cache skipped keys 1..4 to decrypt packet #5.");

        // Bob should now have skipped keys for 1, 2, 3, 4
        bobSession.SkippedMessageKeys.Count.Should().Be(4, "Skipped keys for counters 1, 2, 3, 4 must be cached in session state.");

        // Now deliver packet #3
        var (p3, _) = _ratchet.Decrypt(bobSession, packets[3].EphemPub, packets[3].Counter, packets[3].PrevCounter, packets[3].Ciphertext);
        Encoding.UTF8.GetString(p3).Should().Be(packets[3].Plaintext, "Bob must consume cached skipped key 3.");

        // Delivering packet #3 again (replay attack) must fail
        var replayAct = () => _ratchet.Decrypt(bobSession, packets[3].EphemPub, packets[3].Counter, packets[3].PrevCounter, packets[3].Ciphertext);
        replayAct.Should().Throw<CryptographicException>("replayed packet must fail because its message key was consumed.");

        // Deliver remaining packets: 1, 2, 4, 6, 7, 8, 9
        int[] remainingIndices = [1, 2, 4, 6, 7, 8, 9];
        foreach (var idx in remainingIndices)
        {
            var (pRest, _) = _ratchet.Decrypt(bobSession, packets[idx].EphemPub, packets[idx].Counter, packets[idx].PrevCounter, packets[idx].Ciphertext);
            Encoding.UTF8.GetString(pRest).Should().Be(packets[idx].Plaintext);
        }

        bobSession.SkippedMessageKeys.Should().BeEmpty("All skipped keys must be exhausted after all out-of-order packets arrive.");
    }

    /// <summary>
    /// Verifies that packets jumping more than MAX_MESSAGE_KEYS (2000) into the future are rejected.
    /// </summary>
    [Fact]
    [Trait("Category", "Crypto")]
    public void DoubleRatchet_OverLookaheadThreshold_ThrowsException()
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
            1234);

        using var bobSession = _ratchet.InitializeRecipientSession(
            sharedMasterSecret,
            bobSpk,
            bobId.PublicKey,
            aliceId.PublicKey,
            5678);

        // Initial message to open Bob's receiving chain
        var (ct0, counter0, prev0, ephem0, _) = _ratchet.Encrypt(aliceSession, "hello"u8);
        _ratchet.Decrypt(bobSession, ephem0, counter0, prev0, ct0);

        // Forge a packet with counter = 2005 (exceeds MAX_MESSAGE_KEYS 2000)
        var act = () => _ratchet.Decrypt(bobSession, ephem0, 2005, 0, new byte[32]);
        act.Should().Throw<InvalidOperationException>().WithMessage("*Over messages into the future*");
    }
}
