// Copyright (c) Zenix. All rights reserved.
// Licensed under the proprietary and confidential license.
// Unauthorized copying, distribution, modification, or disclosure of this file,
// via any medium, is strictly prohibited without prior written permission.

using System;
using System.Security.Cryptography;
using System.Text;

namespace Zenix.Core.Zalo.Cryptography;

public sealed partial class ZaloCryptoService
{
    private const byte KeychainEnvelopeType = 0x49;
    private const int MinimumEnvelopeLength = 65; // 1 + 16 (IV) + 16 (Salt) + 0 (min Ciphertext) + 32 (HMAC)

    private static readonly byte[] PrimaryKeychainInfo = Encoding.UTF8.GetBytes("ZaloDeviceLink");
    private static readonly byte[] FallbackKeychainInfo = Encoding.UTF8.GetBytes("ZprotocolSecureKDF");

    /// <inheritdoc/>
    public byte[] EncryptKeychainData(ReadOnlySpan<byte> plainData, ReadOnlySpan<byte> rootSecret)
    {
        if (rootSecret.Length != 32)
        {
            throw new ArgumentException("Root secret must be exactly 32 bytes in length.", nameof(rootSecret));
        }

        Span<byte> iv = stackalloc byte[16];
        RandomNumberGenerator.Fill(iv);

        Span<byte> salt = stackalloc byte[16];
        RandomNumberGenerator.Fill(salt);

        Span<byte> derivedKeys = stackalloc byte[64];
        try
        {
            HKDF.DeriveKey(HashAlgorithmName.SHA256, rootSecret, derivedKeys, salt, PrimaryKeychainInfo);
            var aesKey = derivedKeys[..32];
            var hmacKey = derivedKeys[32..64];

            byte[] ciphertext;
            using (var aes = Aes.Create())
            {
                aes.Mode = CipherMode.CBC;
                aes.Padding = PaddingMode.PKCS7;
                aes.Key = aesKey.ToArray();
                aes.IV = iv.ToArray();

                using var encryptor = aes.CreateEncryptor();
                ciphertext = encryptor.TransformFinalBlock(plainData.ToArray(), 0, plainData.Length);
            }

            var envelopeLength = 1 + iv.Length + salt.Length + ciphertext.Length + 32;
            var envelope = new byte[envelopeLength];
            envelope[0] = KeychainEnvelopeType;
            iv.CopyTo(envelope.AsSpan(1, 16));
            salt.CopyTo(envelope.AsSpan(17, 16));
            ciphertext.CopyTo(envelope.AsSpan(33, ciphertext.Length));

            var authenticatedData = envelope.AsSpan(0, envelope.Length - 32);
            var hmacTag = HMACSHA256.HashData(hmacKey, authenticatedData);
            hmacTag.CopyTo(envelope.AsSpan(envelope.Length - 32, 32));

            return envelope;
        }
        finally
        {
            CryptographicOperations.ZeroMemory(derivedKeys);
        }
    }

    /// <inheritdoc/>
    public byte[] DecryptKeychainData(ReadOnlySpan<byte> encryptedEnvelope, ReadOnlySpan<byte> rootSecret)
    {
        if (rootSecret.Length != 32)
        {
            throw new ArgumentException("Root secret must be exactly 32 bytes in length.", nameof(rootSecret));
        }

        if (encryptedEnvelope.Length < MinimumEnvelopeLength)
        {
            throw new ArgumentException($"Envelope length {encryptedEnvelope.Length} is less than minimum required {MinimumEnvelopeLength} bytes.", nameof(encryptedEnvelope));
        }

        if (encryptedEnvelope[0] != KeychainEnvelopeType)
        {
            throw new ArgumentException($"Invalid envelope version header byte: 0x{encryptedEnvelope[0]:X2}, expected 0x{KeychainEnvelopeType:X2}.", nameof(encryptedEnvelope));
        }

        var iv = encryptedEnvelope.Slice(1, 16);
        var salt = encryptedEnvelope.Slice(17, 16);
        var ciphertext = encryptedEnvelope.Slice(33, encryptedEnvelope.Length - 33 - 32);
        var envelopeHmac = encryptedEnvelope[^32..];
        var authenticatedData = encryptedEnvelope[..^32];

        Span<byte> derivedKeys = stackalloc byte[64];
        try
        {
            // Primary key derivation: info = "ZaloDeviceLink"
            HKDF.DeriveKey(HashAlgorithmName.SHA256, rootSecret, derivedKeys, salt, PrimaryKeychainInfo);
            var hmacKey = derivedKeys[32..64];

            var computedHmac = HMACSHA256.HashData(hmacKey, authenticatedData);
            var isValidHmac = CryptographicOperations.FixedTimeEquals(computedHmac, envelopeHmac);

            if (!isValidHmac)
            {
                // Fallback key derivation: info = "ZprotocolSecureKDF"
                HKDF.DeriveKey(HashAlgorithmName.SHA256, rootSecret, derivedKeys, salt, FallbackKeychainInfo);
                hmacKey = derivedKeys[32..64];
                computedHmac = HMACSHA256.HashData(hmacKey, authenticatedData);
                isValidHmac = CryptographicOperations.FixedTimeEquals(computedHmac, envelopeHmac);
            }

            if (!isValidHmac)
            {
                throw new CryptographicException("HMAC-SHA256 authentication failed for keychain envelope. Data has been tampered with or corrupted.");
            }

            var aesKey = derivedKeys[..32];

            using var aes = Aes.Create();
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7;
            aes.Key = aesKey.ToArray();
            aes.IV = iv.ToArray();

            using var decryptor = aes.CreateDecryptor();
            return decryptor.TransformFinalBlock(ciphertext.ToArray(), 0, ciphertext.Length);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(derivedKeys);
        }
    }
}
