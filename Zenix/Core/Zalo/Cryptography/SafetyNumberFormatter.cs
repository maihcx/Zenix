// Copyright (c) Zenix. All rights reserved.
// Licensed under the proprietary and confidential license.
// Unauthorized copying, distribution, modification, or disclosure of this file,
// via any medium, is strictly prohibited without prior written permission.

using System;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace Zenix.Core.Zalo.Cryptography;

/// <summary>
/// Formats mutual 60-digit numeric Safety Numbers (fingerprints) for Curve25519 identity key verification,
/// matching the Signal Protocol specification over 5,200 SHA-512 iterations.
/// </summary>
public static class SafetyNumberFormatter
{
    /// <summary>
    /// The number of iterative SHA-512 hashing rounds required by the Signal fingerprint specification (5,200).
    /// </summary>
    public const int DefaultIterations = 5200;

    /// <summary>
    /// Computes a mutual 60-digit Safety Number between two identity public keys without user identifiers,
    /// formatted symmetrically into 12 space-separated blocks of 5 decimal digits.
    /// </summary>
    /// <param name="ourIdentityKey">Our Curve25519 identity public key bytes (32 or 33 bytes).</param>
    /// <param name="theirIdentityKey">The remote party's Curve25519 identity public key bytes (32 or 33 bytes).</param>
    /// <returns>A 60-digit string formatted in 12 groups of 5 decimal digits (e.g. <c>XXXXX XXXXX ...</c>).</returns>
    public static string ComputeSafetyNumber(ReadOnlySpan<byte> ourIdentityKey, ReadOnlySpan<byte> theirIdentityKey)
    {
        return ComputeSafetyNumber(string.Empty, ourIdentityKey, string.Empty, theirIdentityKey, DefaultIterations);
    }

    /// <summary>
    /// Computes a mutual 60-digit Safety Number between two parties given their user identifiers and identity keys,
    /// formatted symmetrically into 12 space-separated blocks of 5 decimal digits.
    /// </summary>
    /// <param name="userA">The identifier of user A.</param>
    /// <param name="keyA">The Curve25519 identity public key bytes of user A.</param>
    /// <param name="userB">The identifier of user B.</param>
    /// <param name="keyB">The Curve25519 identity public key bytes of user B.</param>
    /// <param name="iterations">The number of SHA-512 iterations (defaults to 5,200).</param>
    /// <returns>A 60-digit string formatted in 12 groups of 5 decimal digits.</returns>
    public static string ComputeSafetyNumber(
        string userA,
        ReadOnlySpan<byte> keyA,
        string userB,
        ReadOnlySpan<byte> keyB,
        int iterations = DefaultIterations)
    {
        ArgumentNullException.ThrowIfNull(userA);
        ArgumentNullException.ThrowIfNull(userB);

        if (keyA.Length != 32 && keyA.Length != 33)
        {
            throw new ArgumentException("Identity key A must be 32 or 33 bytes in length.", nameof(keyA));
        }

        if (keyB.Length != 32 && keyB.Length != 33)
        {
            throw new ArgumentException("Identity key B must be 32 or 33 bytes in length.", nameof(keyB));
        }

        var normA = NormalizeKey(keyA);
        var normB = NormalizeKey(keyB);

        var fpA = ComputeIndividualFingerprint(userA, normA, iterations);
        var fpB = ComputeIndividualFingerprint(userB, normB, iterations);

        // Sort the two 30-digit strings symmetrically
        var combined = string.CompareOrdinal(fpA, fpB) <= 0 ? fpA + fpB : fpB + fpA;

        var sb = new StringBuilder(71);
        for (var i = 0; i < 12; i++)
        {
            if (i > 0)
            {
                sb.Append(' ');
            }

            sb.Append(combined.AsSpan(i * 5, 5));
        }

        return sb.ToString();
    }

    private static byte[] NormalizeKey(ReadOnlySpan<byte> key)
    {
        if (key.Length == 33 && key[0] == 0x05)
        {
            return key.ToArray();
        }

        if (key.Length == 32)
        {
            var prefixed = new byte[33];
            prefixed[0] = 0x05;
            key.CopyTo(prefixed.AsSpan(1));
            return prefixed;
        }

        return key.ToArray();
    }

    private static string ComputeIndividualFingerprint(string userId, byte[] normalizedKey, int iterations)
    {
        var idBytes = Encoding.UTF8.GetBytes(userId);

        // Step 1: Initial block = [0x00, 0x00] (version 0) || normalizedKey || idBytes || normalizedKey
        var initialLen = 2 + normalizedKey.Length + idBytes.Length + normalizedKey.Length;
        var initialBuf = new byte[initialLen];
        initialBuf[0] = 0x00;
        initialBuf[1] = 0x00;
        normalizedKey.CopyTo(initialBuf.AsSpan(2));
        idBytes.CopyTo(initialBuf.AsSpan(2 + normalizedKey.Length));
        normalizedKey.CopyTo(initialBuf.AsSpan(2 + normalizedKey.Length + idBytes.Length));

        var currentHash = SHA512.HashData(initialBuf);

        // Next rounds: currentHash = SHA512(currentHash || normalizedKey)
        var stepBuf = new byte[64 + normalizedKey.Length];
        normalizedKey.CopyTo(stepBuf.AsSpan(64));

        for (var i = 1; i < iterations; i++)
        {
            currentHash.CopyTo(stepBuf.AsSpan(0, 64));
            currentHash = SHA512.HashData(stepBuf);
        }

        // Extract 6 chunks of 5 bytes at offsets 0, 5, 10, 15, 20, 25
        var sb = new StringBuilder(30);
        ReadOnlySpan<int> offsets = [0, 5, 10, 15, 20, 25];

        foreach (var offset in offsets)
        {
            ulong val = ((ulong)currentHash[offset] << 32)
                      | ((ulong)currentHash[offset + 1] << 24)
                      | ((ulong)currentHash[offset + 2] << 16)
                      | ((ulong)currentHash[offset + 3] << 8)
                      | currentHash[offset + 4];

            var chunk = (uint)(val % 100000UL);
            sb.Append(chunk.ToString("D5", CultureInfo.InvariantCulture));
        }

        return sb.ToString();
    }
}
