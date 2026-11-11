// Copyright (c) Zenix. All rights reserved.
// Licensed under the proprietary and confidential license.
// Unauthorized copying, distribution, modification, or disclosure of this file,
// via any medium, is strictly prohibited without prior written permission.

using System;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;
using FluentAssertions;
using Xunit;
using Zenix.Core.Zalo.Contracts;
using Zenix.Core.Zalo.Cryptography;

namespace Zenix.Tests.Zalo.Crypto;

/// <summary>
/// Unit tests for <see cref="SignalE2eeService"/>, validating PreKeyWhisperMessage (type 3) and WhisperMessage (type 1)
/// wire framing, HMAC authentication, multi-turn bidirectional ratcheting, and 60-digit symmetric Safety Numbers.
/// </summary>
public sealed class SignalE2eeTests
{
    private readonly ISignalE2eeService _e2ee = new SignalE2eeService();

    /// <summary>
    /// Asserts that the first message between two registered identities produces a Type 3 PreKeyWhisperMessage,
    /// Bob's reply produces a Type 1 WhisperMessage, and subsequent turns ratchet cleanly.
    /// Matches the behavior tested in E2EETests.swift:51-73.
    /// </summary>
    [Fact]
    [Trait("Category", "Crypto")]
    public async Task SignalE2ee_EstablishSessionThenRatchets_RoundtripMatchesSwiftVectors()
    {
        const string aliceId = "alice_uuid";
        const string bobId = "bob_uuid";

        // Register identities
        var aliceRegId = await _e2ee.RegisterIdentityAsync(aliceId);
        var bobRegId = await _e2ee.RegisterIdentityAsync(bobId);

        aliceRegId.Should().BeInRange(0, 16383);
        bobRegId.Should().BeInRange(0, 16383);

        // Before sending, Alice has no open session to Bob
        var hasSessionBefore = await _e2ee.HasSessionAsync(aliceId, bobId);
        hasSessionBefore.Should().BeFalse();

        // 1. First message from Alice to Bob -> Must be Type 3 (PreKeyWhisperMessage)
        const string firstMessage = "Chào Bob 👋";
        var firstEnvelope = await _e2ee.EncryptAsync(firstMessage, aliceId, bobId);

        firstEnvelope.Should().NotBeNull();
        firstEnvelope.Type.Should().Be(3, "The initial message to a peer must be a Type 3 PreKeyWhisperMessage.");
        firstEnvelope.IsPreKeyMessage.Should().BeTrue();
        firstEnvelope.Body.Should().NotBeNullOrWhiteSpace();

        // Bob decrypts first message
        var decryptedFirst = await _e2ee.DecryptAsync(firstEnvelope, bobId, aliceId);
        decryptedFirst.Should().Be(firstMessage, "Bob must cleanly decrypt Alice's first message.");

        // 2. Reply from Bob to Alice -> Must be Type 1 (WhisperMessage)
        const string replyMessage = "Chào Alice!";
        var replyEnvelope = await _e2ee.EncryptAsync(replyMessage, bobId, aliceId);

        replyEnvelope.Should().NotBeNull();
        replyEnvelope.Type.Should().Be(1, "A reply in an established session must be a Type 1 WhisperMessage.");
        replyEnvelope.IsPreKeyMessage.Should().BeFalse();

        // Alice decrypts reply
        var decryptedReply = await _e2ee.DecryptAsync(replyEnvelope, aliceId, bobId);
        decryptedReply.Should().Be(replyMessage, "Alice must cleanly decrypt Bob's reply.");

        // 3. Multi-turn ratcheting
        for (var i = 0; i < 5; i++)
        {
            var text = $"Tin nhắn số {i} — mã hóa đầu cuối 🔒";
            var env = await _e2ee.EncryptAsync(text, aliceId, bobId);
            env.Type.Should().Be(1, "Subsequent ratcheted messages must remain Type 1 WhisperMessages.");

            var decrypted = await _e2ee.DecryptAsync(env, bobId, aliceId);
            decrypted.Should().Be(text);
        }
    }

    /// <summary>
    /// Asserts that tampered ciphertext or modified wire HMAC is rejected with CryptographicException.
    /// Matches the behavior tested in E2EETests.swift:88-105.
    /// </summary>
    [Fact]
    [Trait("Category", "Crypto")]
    public async Task SignalE2ee_TamperedCiphertext_RejectsDecryption()
    {
        const string carolId = "carol_uuid";
        const string daveId = "dave_uuid";

        await _e2ee.RegisterIdentityAsync(carolId);
        await _e2ee.RegisterIdentityAsync(daveId);

        // Open session
        var setup = await _e2ee.EncryptAsync("hello", carolId, daveId);
        await _e2ee.DecryptAsync(setup, daveId, carolId);

        var ack = await _e2ee.EncryptAsync("ack", daveId, carolId);
        await _e2ee.DecryptAsync(ack, carolId, daveId);

        // Send message to tamper
        var env = await _e2ee.EncryptAsync("bí mật", carolId, daveId);
        env.Type.Should().Be(1);

        var wireBytes = Convert.FromBase64String(env.Body);
        wireBytes[wireBytes.Length / 2] ^= 0x5A; // Flip bits in ciphertext

        var tamperedEnv = new SignalMessageEnvelope(env.Type, Convert.ToBase64String(wireBytes), env.RegistrationId);

        var act = async () => await _e2ee.DecryptAsync(tamperedEnv, daveId, carolId);
        await act.Should().ThrowAsync<CryptographicException>("tampered WhisperMessage must fail HMAC authentication.");
    }

    /// <summary>
    /// Asserts that the 60-digit Safety Number calculation produces exactly 12 space-separated blocks of 5 decimal digits,
    /// is 100% symmetric between both parties (SafetyNumber(A, B) == SafetyNumber(B, A)), and contains only decimal digits and spaces.
    /// Matches the behavior tested in E2EETests.swift:75-86.
    /// </summary>
    [Fact]
    [Trait("Category", "Crypto")]
    public async Task SignalE2ee_SafetyNumber_IsSixtyDigitsAndSymmetric()
    {
        const string userMe = "me_user_12345";
        const string userMinhAnh = "minh_anh_67890";

        await _e2ee.RegisterIdentityAsync(userMe);
        await _e2ee.RegisterIdentityAsync(userMinhAnh);

        var ab = await _e2ee.ComputeSafetyNumberAsync(userMe, userMinhAnh);
        var ba = await _e2ee.ComputeSafetyNumberAsync(userMinhAnh, userMe);

        ab.Should().NotBeNullOrWhiteSpace();
        ba.Should().NotBeNullOrWhiteSpace();

        // Must be symmetric
        ab.Should().Be(ba, "Safety Number must be strictly symmetric regardless of evaluation order.");

        // Must be 12 groups of 5 decimal digits separated by single spaces
        var blocks = ab.Split(' ');
        blocks.Length.Should().Be(12, "Safety Number must be formatted into 12 space-separated groups.");

        foreach (var block in blocks)
        {
            block.Length.Should().Be(5, "Each Safety Number block must contain exactly 5 decimal digits.");
            block.All(char.IsDigit).Should().BeTrue("Each Safety Number block must consist solely of decimal digits.");
        }

        var digitsOnly = string.Concat(blocks);
        digitsOnly.Length.Should().Be(60, "The total numeric length of the Safety Number must be exactly 60 digits.");
    }
}
