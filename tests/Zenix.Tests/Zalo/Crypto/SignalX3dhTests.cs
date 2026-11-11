// Copyright (c) Zenix. All rights reserved.
// Licensed under the proprietary and confidential license.
// Unauthorized copying, distribution, modification, or disclosure of this file,
// via any medium, is strictly prohibited without prior written permission.

using System;
using System.Security.Cryptography;
using FluentAssertions;
using Xunit;
using Zenix.Core.Zalo.Contracts;
using Zenix.Core.Zalo.Cryptography;

namespace Zenix.Tests.Zalo.Crypto;

/// <summary>
/// Unit tests for Curve25519 Montgomery ladder (RFC 7748) and Signal X3DH key agreement services.
/// </summary>
public sealed class SignalX3dhTests
{
    private readonly SignalX3dhService _x3dh = new();

    /// <summary>
    /// Asserts that Curve25519 Montgomery scalar multiplication matches the official RFC 7748 test vectors.
    /// </summary>
    [Fact]
    [Trait("Category", "Crypto")]
    public void Curve25519_MatchesRfc7748TestVectors()
    {
        // RFC 7748 Section 6.1 Test Vectors
        var alicePriv = Convert.FromHexString("77076d0a7318a57d3c16c17251b26645df4c2f87ebc0992ab177fba51db92c2a");
        var expectedAlicePub = "8520f0098930a754748b7ddcb43ef75a0dbf3a0d26381af4eba4a98eaa9b4e6a";

        var bobPriv = Convert.FromHexString("5dab087e624a8a4b79e17f8b83800ee66f3bb1292618b6fd1c2f8b27ff88e0eb");
        var expectedBobPub = "de9edb7d7b7dc1b4d35b61c2ece435373f8343c85b78674dadfc7e146f882b4f";

        var expectedSharedSecret = "4a5d9d5ba4ce2de1728e3bf480350f25e07e21c947d19e3376f09b3c1e161742";

        // Test public key derivation from base point u = 9
        var actualAlicePub = Curve25519Helper.GetPublicKey(alicePriv, includePrefix: false);
        Convert.ToHexStringLower(actualAlicePub).Should().Be(expectedAlicePub, "Alice public key must match RFC 7748 vector.");

        var actualBobPub = Curve25519Helper.GetPublicKey(bobPriv, includePrefix: false);
        Convert.ToHexStringLower(actualBobPub).Should().Be(expectedBobPub, "Bob public key must match RFC 7748 vector.");

        // Test shared secret calculation (Alice pub + Bob priv, and Bob pub + Alice priv)
        var sharedAlice = Curve25519Helper.CalculateAgreement(alicePriv, actualBobPub);
        Convert.ToHexStringLower(sharedAlice).Should().Be(expectedSharedSecret, "Shared secret computed by Alice must match RFC 7748 vector.");

        var sharedBob = Curve25519Helper.CalculateAgreement(bobPriv, actualAlicePub);
        Convert.ToHexStringLower(sharedBob).Should().Be(expectedSharedSecret, "Shared secret computed by Bob must match RFC 7748 vector.");

        sharedAlice.Should().Equal(sharedBob, "Diffie-Hellman key agreement must be strictly symmetric.");

        // Test 33-byte 0x05 prefix support
        var alicePubPrefixed = Curve25519Helper.GetPublicKey(alicePriv, includePrefix: true);
        alicePubPrefixed.Length.Should().Be(33);
        alicePubPrefixed[0].Should().Be(0x05);

        var sharedPrefixed = Curve25519Helper.CalculateAgreement(bobPriv, alicePubPrefixed);
        sharedPrefixed.Should().Equal(sharedAlice, "CalculateAgreement must seamlessly accept 33-byte 0x05-prefixed keys.");

        // Test invalid prefix rejection
        var badPrefix = (byte[])alicePubPrefixed.Clone();
        badPrefix[0] = 0x04;
        var badAct = () => Curve25519Helper.CalculateAgreement(bobPriv, badPrefix);
        badAct.Should().Throw<ArgumentException>("public key with prefix other than 0x05 must be rejected.");
    }

    /// <summary>
    /// Asserts that Curve25519 identity key signing and signature verification are authentic and detect tampering.
    /// </summary>
    [Fact]
    [Trait("Category", "Crypto")]
    public void Curve25519_SigningAndVerification_RoundtripAuthentic()
    {
        var (privKey, pubKey) = Curve25519Helper.GenerateKeyPair(includePrefix: true);
        byte[] payload = [0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07, 0x08];

        var signature = Curve25519Helper.Sign(privKey, payload);
        signature.Length.Should().Be(64, "Curve25519 signature must be exactly 64 bytes.");

        var isValid = Curve25519Helper.VerifySignature(pubKey, payload, signature);
        isValid.Should().BeTrue("Authentic signature must verify cleanly.");

        // Tampered payload
        byte[] tamperedPayload = [0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07, 0xFF];
        var isTamperedValid = Curve25519Helper.VerifySignature(pubKey, tamperedPayload, signature);
        isTamperedValid.Should().BeFalse("Tampered payload must fail signature verification.");

        // Tampered signature
        var tamperedSig = (byte[])signature.Clone();
        tamperedSig[10] ^= 0x5A;
        var isTamperedSigValid = Curve25519Helper.VerifySignature(pubKey, payload, tamperedSig);
        isTamperedSigValid.Should().BeFalse("Tampered signature byte must fail verification.");
    }

    /// <summary>
    /// Asserts that Alice and Bob generate valid PreKey bundles and compute 100% identical master shared secrets (SK).
    /// </summary>
    [Fact]
    [Trait("Category", "Crypto")]
    public void SignalX3dh_HandshakeAgreement_ProducesIdenticalSharedSecrets()
    {
        var aliceStore = _x3dh.GetOrCreateUserStore("alice_identity");
        var bobStore = _x3dh.GetOrCreateUserStore("bob_identity");

        aliceStore.RegistrationId.Should().BeInRange(0, 16383, "RegistrationId must be a 14-bit unsigned integer.");
        bobStore.RegistrationId.Should().BeInRange(0, 16383);

        // Bob publishes prekey bundle
        var bobBundle = _x3dh.CreatePreKeyBundle("bob_identity");
        bobBundle.IdentityKey.Length.Should().Be(33);
        bobBundle.IdentityKey[0].Should().Be(0x05);
        bobBundle.SignedPreKey.Length.Should().Be(33);
        bobBundle.Signature.Length.Should().Be(64);
        bobBundle.OneTimePreKeyId.Should().NotBeNull();
        bobBundle.OneTimePreKey.Should().NotBeNull();

        // Alice initiates handshake
        var aliceBaseKeyPair = Curve25519Helper.GenerateKeyPair(includePrefix: true);
        var aliceMasterSecret = _x3dh.InitiateHandshake(aliceStore, bobBundle, aliceBaseKeyPair);
        aliceMasterSecret.Length.Should().Be(64, "Derived master secret must contain 32B rootKey + 32B chainKey.");

        // Bob receives handshake
        var bobMasterSecret = _x3dh.ReceiveHandshake(
            bobStore,
            aliceStore.IdentityKeyPair.PublicKey,
            aliceBaseKeyPair.PublicKey,
            bobBundle.OneTimePreKeyId);

        bobMasterSecret.Should().Equal(aliceMasterSecret, "Alice and Bob must derive 100% identical master shared secrets from X3DH.");

        // Bob's rootKey matches Alice's rootKey
        bobMasterSecret[..32].Should().Equal(aliceMasterSecret[..32]);
        // Bob's initial chainKey matches Alice's initial chainKey
        bobMasterSecret[32..64].Should().Equal(aliceMasterSecret[32..64]);

        // Handshake without One-Time PreKey (exhausted pool)
        var bobBundleNoOpk = new SignalPreKeyBundle(
            IdentityKey: bobBundle.IdentityKey,
            RegistrationId: bobBundle.RegistrationId,
            SignedPreKeyId: bobBundle.SignedPreKeyId,
            SignedPreKey: bobBundle.SignedPreKey,
            Signature: bobBundle.Signature,
            OneTimePreKeyId: null,
            OneTimePreKey: null);

        var aliceBase2 = Curve25519Helper.GenerateKeyPair(includePrefix: true);
        var aliceSecretNoOpk = _x3dh.InitiateHandshake(aliceStore, bobBundleNoOpk, aliceBase2);
        var bobSecretNoOpk = _x3dh.ReceiveHandshake(bobStore, aliceStore.IdentityKeyPair.PublicKey, aliceBase2.PublicKey, null);

        bobSecretNoOpk.Should().Equal(aliceSecretNoOpk, "X3DH handshake must succeed without one-time prekeys and produce identical keys.");

        // Tampered signature rejection
        var tamperedBundle = new SignalPreKeyBundle(
            IdentityKey: bobBundle.IdentityKey,
            RegistrationId: bobBundle.RegistrationId,
            SignedPreKeyId: bobBundle.SignedPreKeyId,
            SignedPreKey: bobBundle.SignedPreKey,
            Signature: new byte[64],
            OneTimePreKeyId: bobBundle.OneTimePreKeyId,
            OneTimePreKey: bobBundle.OneTimePreKey);

        var tamperAct = () => _x3dh.InitiateHandshake(aliceStore, tamperedBundle, aliceBaseKeyPair);
        tamperAct.Should().Throw<CryptographicException>("invalid signed prekey signature must abort X3DH session establishment.");
    }
}
