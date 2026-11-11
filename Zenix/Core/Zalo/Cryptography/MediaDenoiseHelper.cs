// Copyright (c) Zenix. All rights reserved.
// Licensed under the proprietary and confidential license.
// Unauthorized copying, distribution, modification, or disclosure of this file,
// via any medium, is strictly prohibited without prior written permission.

using System;

namespace Zenix.Core.Zalo.Cryptography;

/// <summary>
/// Provides utility routines for reversing anti-scraping scrambling applied to media payloads
/// downloaded from the Zalo content delivery network (CDN).
/// </summary>
public static class MediaDenoiseHelper
{
    /// <summary>
    /// The 6-byte magic signature prefix prepended by Zalo CDN to scrambled media payloads:
    /// <c>0x07, 0x7A, 0x41, 0x6C, 0x4F, 0x07</c> (ASCII: <c>\a z A l O \a</c>).
    /// </summary>
    public static ReadOnlySpan<byte> SignaturePrefix => [0x07, 0x7A, 0x41, 0x6C, 0x4F, 0x07];

    /// <summary>
    /// The XOR masking key applied byte-wise to scrambled media payloads after the magic signature.
    /// </summary>
    public const byte XorKey = 147; // 0x93

    /// <summary>
    /// De-noises media bytes downloaded from Zalo CDN.
    /// If the input buffer begins with the 6-byte signature <c>077a416c4f07</c>, strips the signature
    /// and applies byte-wise XOR with key 147 (<c>0x93</c>) to restore the original clean media stream (e.g. JPEG, PNG, MP4).
    /// If the signature is absent or the buffer has fewer than 6 bytes, returns the input untouched.
    /// </summary>
    /// <param name="mediaBytes">The raw bytes of the downloaded media file.</param>
    /// <returns>A memory block containing clean media file bytes.</returns>
    public static ReadOnlyMemory<byte> DenoiseMedia(ReadOnlySpan<byte> mediaBytes)
    {
        if (mediaBytes.Length < SignaturePrefix.Length || !mediaBytes.StartsWith(SignaturePrefix))
        {
            return mediaBytes.ToArray();
        }

        var payload = mediaBytes[SignaturePrefix.Length..];
        var result = new byte[payload.Length];

        for (var i = 0; i < payload.Length; i++)
        {
            result[i] = (byte)(payload[i] ^ XorKey);
        }

        return result;
    }
}
