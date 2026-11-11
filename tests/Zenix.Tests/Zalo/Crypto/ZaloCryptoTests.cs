// Copyright (c) Zenix. All rights reserved.
// Licensed under the proprietary and confidential license.
// Unauthorized copying, distribution, modification, or disclosure of this file,
// via any medium, is strictly prohibited without prior written permission.

using System;
using System.Security.Cryptography;
using System.Text;
using FluentAssertions;
using Xunit;
using Zenix.Core.Zalo.Cryptography;

namespace Zenix.Tests.Zalo.Crypto;

/// <summary>
/// Unit tests for Zalo cryptographic primitives: AES-256-CBC zcid telemetry, API param encryption/decryption,
/// trusted-device linking keychain envelopes (HKDF-SHA256, constant-time HMAC), and XOR 147 media de-noising.
/// </summary>
public sealed class ZaloCryptoTests
{
    private readonly ZaloCryptoService _crypto = new();

    /// <summary>
    /// Verifies that EncryptZcid produces the exact reference ciphertext vector,
    /// EncryptApiParams matches Node/Swift parity Base64 ciphertext, and DecryptApiParams roundtrips.
    /// </summary>
    [Fact]
    [Trait("Category", "Crypto")]
    public void VerifyZcidAndParamEncryption()
    {
        // Vector from RuntimeTests.swift:24-30
        const int clientType = 23;
        const string imei = "abc-imei";
        const long timestamp = 1792963077777L;
        const string expectedZcid = "a1a69db6a344da4d883865c637f979495dd86112a4534347b462259bfdeb641b";

        var actualZcid = _crypto.EncryptZcid(clientType, imei, timestamp);
        actualZcid.Should().Be(expectedZcid, "zcid encryption must match official Node/Swift reference vector byte-for-byte.");

        // Vector from RuntimeTests.swift:32-43
        const string jsonPayload = "{\"language\":\"vi\"}";
        const string expectedApiParams = "V5p1S6tTwaE91W8UDCagn9vVP9wUQ3QzA04wG7xh+5E=";

        var actualApiParams = _crypto.EncryptApiParams(jsonPayload);
        actualApiParams.Should().Be(expectedApiParams, "API params encryption must match official Node/Swift reference Base64 vector.");

        var decryptedPayload = _crypto.DecryptApiParams(actualApiParams);
        decryptedPayload.Should().Be(jsonPayload, "DecryptApiParams must roundtrip back to exact original JSON plaintext.");

        // Tampered Base64 should fail decryption
        var tampered = Convert.ToBase64String(new byte[32]);
        var act = () => _crypto.DecryptApiParams(tampered);
        act.Should().Throw<CryptographicException>("tampered ciphertext must fail PKCS7 padding validation.");

        // Argument validations
        var nullImeiAct = () => _crypto.EncryptZcid(23, null!, 12345);
        nullImeiAct.Should().Throw<ArgumentException>();

        var emptyImeiAct = () => _crypto.EncryptZcid(23, "   ", 12345);
        emptyImeiAct.Should().Throw<ArgumentException>();

        var nullJsonAct = () => _crypto.EncryptApiParams(null!);
        nullJsonAct.Should().Throw<ArgumentNullException>();

        var emptyCipherAct = () => _crypto.DecryptApiParams("   ");
        emptyCipherAct.Should().Throw<ArgumentException>();
    }

    /// <summary>
    /// Verifies that Keychain envelopes seal and unseal data correctly, reject tampering in constant time,
    /// and that media de-noising reverses XOR 147 scrambling while leaving clean data untouched.
    /// </summary>
    [Fact]
    [Trait("Category", "Crypto")]
    public void VerifyKeychainAndDenoise()
    {
        // Keychain roundtrip
        var rootSecret = new byte[32];
        RandomNumberGenerator.Fill(rootSecret);

        var plainText = "SessionToken_super_secret_payload_xyz_1234567890";
        var plainBytes = Encoding.UTF8.GetBytes(plainText);

        var envelope = _crypto.EncryptKeychainData(plainBytes, rootSecret);
        envelope.Should().NotBeNull();
        envelope.Length.Should().BeGreaterThanOrEqualTo(65, "Keychain envelope must contain 65 bytes of header/salt/iv/hmac overhead.");
        envelope[0].Should().Be(0x49, "Envelope must begin with version byte 0x49.");

        var decryptedBytes = _crypto.DecryptKeychainData(envelope, rootSecret);
        var decryptedText = Encoding.UTF8.GetString(decryptedBytes);
        decryptedText.Should().Be(plainText, "Keychain decrypted data must match original plaintext exactly.");

        // Short envelope rejection (< 65 bytes)
        var shortEnvelope = new byte[64];
        shortEnvelope[0] = 0x49;
        var shortAct = () => _crypto.DecryptKeychainData(shortEnvelope, rootSecret);
        shortAct.Should().Throw<ArgumentException>("envelope shorter than 65 bytes must be rejected immediately.");

        // Invalid magic byte rejection
        var invalidMagicEnvelope = (byte[])envelope.Clone();
        invalidMagicEnvelope[0] = 0x48;
        var magicAct = () => _crypto.DecryptKeychainData(invalidMagicEnvelope, rootSecret);
        magicAct.Should().Throw<ArgumentException>("envelope without 0x49 magic byte must be rejected.");

        // Tampered ciphertext rejection
        var tamperedCiphertextEnvelope = (byte[])envelope.Clone();
        tamperedCiphertextEnvelope[34] ^= 0x5A;
        var tamperCipherAct = () => _crypto.DecryptKeychainData(tamperedCiphertextEnvelope, rootSecret);
        tamperCipherAct.Should().Throw<CryptographicException>("tampered ciphertext must fail HMAC authentication before decryption.");

        // Tampered HMAC rejection
        var tamperedHmacEnvelope = (byte[])envelope.Clone();
        tamperedHmacEnvelope[^1] ^= 0xFF;
        var tamperHmacAct = () => _crypto.DecryptKeychainData(tamperedHmacEnvelope, rootSecret);
        tamperHmacAct.Should().Throw<CryptographicException>("tampered HMAC tag must fail constant-time authentication.");

        // Invalid root secret length rejection
        var badSecret = new byte[31];
        var badSecretAct = () => _crypto.EncryptKeychainData(plainBytes, badSecret);
        badSecretAct.Should().Throw<ArgumentException>("root secret other than 32 bytes must be rejected.");

        // Media de-noising: XOR 147 (0x93) with 077a416c4f07 prefix
        byte[] cleanJpegHeader = [0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46, 0x49, 0x46];
        var scrambledPayload = new byte[cleanJpegHeader.Length];
        for (var i = 0; i < cleanJpegHeader.Length; i++)
        {
            scrambledPayload[i] = (byte)(cleanJpegHeader[i] ^ 0x93);
        }

        byte[] signature = [0x07, 0x7A, 0x41, 0x6C, 0x4F, 0x07];
        var scrambledBuffer = new byte[signature.Length + scrambledPayload.Length];
        signature.CopyTo(scrambledBuffer.AsSpan(0));
        scrambledPayload.CopyTo(scrambledBuffer.AsSpan(signature.Length));

        var denoised = _crypto.DenoiseMedia(scrambledBuffer);
        denoised.ToArray().Should().Equal(cleanJpegHeader, "DenoiseMedia must strip 6-byte prefix and reverse XOR 147 to restore clean JPEG header.");

        // Media de-noising on clean buffer without signature
        var untouchedClean = _crypto.DenoiseMedia(cleanJpegHeader);
        untouchedClean.ToArray().Should().Equal(cleanJpegHeader, "DenoiseMedia must leave clean data without signature untouched.");

        // Media de-noising on short buffer (< 6 bytes)
        byte[] shortMedia = [0x01, 0x02, 0x03];
        var untouchedShort = _crypto.DenoiseMedia(shortMedia);
        untouchedShort.ToArray().Should().Equal(shortMedia, "DenoiseMedia must leave short data untouched without errors.");
    }
}
