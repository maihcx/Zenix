// --------------------------------------------------------------------------------------------------------------------
// <copyright file="IZaloCryptoService.cs" company="Zenix">
//   Copyright (c) Zenix. All rights reserved.
// </copyright>
// --------------------------------------------------------------------------------------------------------------------

using System;
using System.Threading;
using System.Threading.Tasks;

namespace Zenix.Core.Zalo.Contracts;

/// <summary>
/// Defines cryptographic operations required by the Zalo protocol, including telemetry encryption,
/// request signature generation, trusted device keychain envelopes, media de-noising, and Signal Protocol E2EE.
/// </summary>
public interface IZaloCryptoService
{
    /// <summary>
    /// Encrypts client telemetry parameters into the 64-character lowercase hex string required for <c>zcid</c>.
    /// Uses AES-256-CBC with the fixed key <c>"3FC4F0D2AB50057BCE0D90D9187A22B1"</c> and a 16-byte zero IV.
    /// </summary>
    /// <param name="clientType">The platform client type code (23 for desktop).</param>
    /// <param name="imei">The unique client IMEI identifier (<c>z_uuid</c>).</param>
    /// <param name="timestamp">The Unix epoch timestamp in milliseconds.</param>
    /// <returns>A 64-character lowercase hexadecimal ciphertext string.</returns>
    string EncryptZcid(int clientType, string imei, long timestamp);

    /// <summary>
    /// Encrypts structured JSON request parameters using AES-256-CBC with key <c>"DE71B7D4C57C0749158CB90D5E431AE3"</c>
    /// and a 16-byte zero IV, returning a Base64-encoded ciphertext.
    /// </summary>
    /// <param name="jsonPayload">The UTF-8 JSON string payload to encrypt.</param>
    /// <returns>A Base64-encoded encrypted string parameter.</returns>
    string EncryptApiParams(string jsonPayload);

    /// <summary>
    /// Decrypts a trusted-device linking envelope formatted as <c>[0x49: 1B][iv: 16B][salt: 16B][ciphertext: NB][hmac: 32B]</c>.
    /// Verifies the HMAC-SHA256 tag in constant time before invoking AES-256-CBC decryption.
    /// </summary>
    /// <param name="encryptedEnvelope">The raw bytes of the sealed keychain envelope.</param>
    /// <param name="rootSecret">The 32-byte shared root secret.</param>
    /// <returns>The decrypted plaintext byte array.</returns>
    /// <exception cref="ArgumentException">Thrown when envelope length is less than 65 bytes or header byte is invalid.</exception>
    /// <exception cref="System.Security.Cryptography.CryptographicException">Thrown when HMAC verification fails.</exception>
    byte[] DecryptKeychainData(ReadOnlySpan<byte> encryptedEnvelope, ReadOnlySpan<byte> rootSecret);

    /// <summary>
    /// Encrypts plaintext data into a trusted-device linking envelope using HKDF-SHA256 derived keys,
    /// AES-256-CBC encryption, and constant-time HMAC-SHA256 authentication.
    /// </summary>
    /// <param name="plainData">The raw payload to encrypt.</param>
    /// <param name="rootSecret">The 32-byte shared root secret.</param>
    /// <returns>A newly allocated byte array containing the sealed envelope.</returns>
    byte[] EncryptKeychainData(ReadOnlySpan<byte> plainData, ReadOnlySpan<byte> rootSecret);

    /// <summary>
    /// De-noises media bytes downloaded from Zalo CDN protected with the XOR 147 noise transform.
    /// Checks prefix signature <c>077a416c4f07</c> and restores original clean file bytes.
    /// </summary>
    /// <param name="mediaBytes">The raw bytes of the downloaded media file.</param>
    /// <returns>A memory block containing clean media file bytes.</returns>
    ReadOnlyMemory<byte> DenoiseMedia(ReadOnlySpan<byte> mediaBytes);

    /// <summary>
    /// Computes the 60-digit numeric Safety Number between two Curve25519 identity public keys
    /// formatted into 12 groups of 5 decimal digits for visual E2EE contact verification.
    /// </summary>
    /// <param name="ourIdentityKey">Our Curve25519 identity public key bytes.</param>
    /// <param name="theirIdentityKey">The contact's Curve25519 identity public key bytes.</param>
    /// <returns>A 60-digit string separated by spaces into 12 groups of 5 digits.</returns>
    string ComputeSafetyNumber(ReadOnlySpan<byte> ourIdentityKey, ReadOnlySpan<byte> theirIdentityKey);
}
