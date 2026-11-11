// Copyright (c) Zenix. All rights reserved.
// Licensed under the proprietary and confidential license.
// Unauthorized copying, distribution, modification, or disclosure of this file,
// via any medium, is strictly prohibited without prior written permission.

using System;
using System.Security.Cryptography;
using System.Text;
using Zenix.Core.Zalo.Contracts;

namespace Zenix.Core.Zalo.Cryptography;

/// <summary>
/// Implements standard cryptographic operations required by the Zalo desktop protocol,
/// including client telemetry encryption (<c>zcid</c>), request parameter encryption/decryption,
/// trusted-device linking keychain envelopes, media de-noising, and Signal safety numbers.
/// </summary>
public sealed partial class ZaloCryptoService : IZaloCryptoService
{
    private static readonly byte[] ZcidKey = Encoding.UTF8.GetBytes("3FC4F0D2AB50057BCE0D90D9187A22B1");
    private static readonly byte[] ApiParamsKey = Encoding.UTF8.GetBytes("DE71B7D4C57C0749158CB90D5E431AE3");
    private static readonly byte[] ZeroIv16 = new byte[16];

    /// <inheritdoc/>
    public string EncryptZcid(int clientType, string imei, long timestamp)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(imei);

        var plaintext = $"{clientType},{imei},{timestamp}";
        var plaintextBytes = Encoding.UTF8.GetBytes(plaintext);

        using var aes = Aes.Create();
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;
        aes.Key = ZcidKey;
        aes.IV = ZeroIv16;

        using var encryptor = aes.CreateEncryptor();
        var ciphertext = encryptor.TransformFinalBlock(plaintextBytes, 0, plaintextBytes.Length);

        return Convert.ToHexStringLower(ciphertext);
    }

    /// <inheritdoc/>
    public string EncryptApiParams(string jsonPayload)
    {
        ArgumentNullException.ThrowIfNull(jsonPayload);

        var plaintextBytes = Encoding.UTF8.GetBytes(jsonPayload);

        using var aes = Aes.Create();
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;
        aes.Key = ApiParamsKey;
        aes.IV = ZeroIv16;

        using var encryptor = aes.CreateEncryptor();
        var ciphertext = encryptor.TransformFinalBlock(plaintextBytes, 0, plaintextBytes.Length);

        return Convert.ToBase64String(ciphertext);
    }

    /// <summary>
    /// Decrypts a Base64-encoded request parameter payload using AES-256-CBC with fixed key
    /// <c>"DE71B7D4C57C0749158CB90D5E431AE3"</c> and zero IV, returning the UTF-8 JSON plaintext string.
    /// </summary>
    /// <param name="base64Ciphertext">The Base64-encoded encrypted payload.</param>
    /// <returns>The decrypted UTF-8 JSON plaintext string.</returns>
    /// <exception cref="ArgumentException">Thrown when ciphertext string is null or whitespace.</exception>
    /// <exception cref="CryptographicException">Thrown when decryption or padding verification fails.</exception>
    public string DecryptApiParams(string base64Ciphertext)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(base64Ciphertext);

        var ciphertextBytes = Convert.FromBase64String(base64Ciphertext);

        using var aes = Aes.Create();
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;
        aes.Key = ApiParamsKey;
        aes.IV = ZeroIv16;

        using var decryptor = aes.CreateDecryptor();
        var plaintextBytes = decryptor.TransformFinalBlock(ciphertextBytes, 0, ciphertextBytes.Length);

        return Encoding.UTF8.GetString(plaintextBytes);
    }

    /// <inheritdoc/>
    public ReadOnlyMemory<byte> DenoiseMedia(ReadOnlySpan<byte> mediaBytes)
    {
        return MediaDenoiseHelper.DenoiseMedia(mediaBytes);
    }

    /// <inheritdoc/>
    public string ComputeSafetyNumber(ReadOnlySpan<byte> ourIdentityKey, ReadOnlySpan<byte> theirIdentityKey)
    {
        return SafetyNumberFormatter.ComputeSafetyNumber(ourIdentityKey, theirIdentityKey);
    }
}
