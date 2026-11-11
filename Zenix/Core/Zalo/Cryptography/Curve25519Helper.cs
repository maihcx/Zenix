// Copyright (c) Zenix. All rights reserved.
// Licensed under the proprietary and confidential license.
// Unauthorized copying, distribution, modification, or disclosure of this file,
// via any medium, is strictly prohibited without prior written permission.

using System;
using System.Numerics;
using System.Security.Cryptography;

namespace Zenix.Core.Zalo.Cryptography;

/// <summary>
/// Provides Montgomery ladder scalar multiplication on Curve25519 (RFC 7748)
/// and Ed25519 / XEd25519 signature routines for Signal Protocol prekey bundles and key agreements.
/// </summary>
public static class Curve25519Helper
{
    private static readonly BigInteger Prime = (BigInteger.One << 255) - 19;
    private static readonly BigInteger A24 = 121665;
    private static readonly BigInteger OrderL = (BigInteger.One << 252) + BigInteger.Parse("27742317777372353535851937790883648493");
    private static readonly BigInteger D = (-121665 * BigInteger.ModPow(121666, Prime - 2, Prime)) % Prime;
    private static readonly BigInteger SqrtM1 = BigInteger.ModPow(2, (Prime - 1) / 4, Prime);

    private static readonly (BigInteger X, BigInteger Y) BasePointEdwards;

    /// <summary>
    /// The canonical 32-byte representation of the Curve25519 Montgomery base point (u = 9).
    /// </summary>
    public static readonly byte[] BasePointU =
    [
        9, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
        0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0
    ];

    static Curve25519Helper()
    {
        var by = (4 * BigInteger.ModPow(5, Prime - 2, Prime)) % Prime;
        if (by < 0)
        {
            by += Prime;
        }

        var bx = RecoverX(by, 0);
        BasePointEdwards = (bx, by);
    }

    /// <summary>
    /// Generates a new random 32-byte private key and derives its corresponding Curve25519 public key.
    /// </summary>
    /// <param name="includePrefix">If <c>true</c>, formats the public key as 33 bytes with leading <c>0x05</c>; otherwise 32 raw bytes.</param>
    /// <returns>A tuple of private key (32 bytes) and public key.</returns>
    public static (byte[] PrivateKey, byte[] PublicKey) GenerateKeyPair(bool includePrefix = false)
    {
        var privateKey = new byte[32];
        RandomNumberGenerator.Fill(privateKey);
        var publicKey = GetPublicKey(privateKey, includePrefix);
        return (privateKey, publicKey);
    }

    /// <summary>
    /// Derives the Curve25519 public key from a 32-byte private key by multiplying with base point u = 9.
    /// </summary>
    /// <param name="privateKey">The 32-byte private key.</param>
    /// <param name="includePrefix">If <c>true</c>, prepends the <c>0x05</c> type prefix byte (33 bytes total).</param>
    /// <returns>A byte array representing the public key.</returns>
    public static byte[] GetPublicKey(ReadOnlySpan<byte> privateKey, bool includePrefix = false)
    {
        var rawPub = CalculateAgreement(privateKey, BasePointU);

        if (!includePrefix)
        {
            return rawPub;
        }

        var prefixed = new byte[33];
        prefixed[0] = 0x05;
        rawPub.CopyTo(prefixed.AsSpan(1));
        return prefixed;
    }

    /// <summary>
    /// Performs Curve25519 Diffie-Hellman scalar multiplication (X25519) according to RFC 7748.
    /// Clamps the private key scalar and computes the shared secret.
    /// Supports both 32-byte raw public keys and 33-byte <c>0x05</c>-prefixed public keys.
    /// </summary>
    /// <param name="privateKey">The 32-byte private key scalar.</param>
    /// <param name="publicKey">The 32-byte or 33-byte (0x05 prefixed) public key.</param>
    /// <returns>A 32-byte shared secret array.</returns>
    /// <exception cref="ArgumentException">Thrown when key lengths are invalid or prefix byte is incorrect.</exception>
    public static byte[] CalculateAgreement(ReadOnlySpan<byte> privateKey, ReadOnlySpan<byte> publicKey)
    {
        if (privateKey.Length != 32)
        {
            throw new ArgumentException("Private key must be exactly 32 bytes in length.", nameof(privateKey));
        }

        ReadOnlySpan<byte> rawPub;
        if (publicKey.Length == 33)
        {
            if (publicKey[0] != 0x05)
            {
                throw new ArgumentException("33-byte public key must begin with the 0x05 prefix byte.", nameof(publicKey));
            }

            rawPub = publicKey[1..];
        }
        else if (publicKey.Length == 32)
        {
            rawPub = publicKey;
        }
        else
        {
            throw new ArgumentException("Public key must be either 32 raw bytes or 33 bytes with 0x05 prefix.", nameof(publicKey));
        }

        // RFC 7748 bit clamping on scalar
        Span<byte> clamped = stackalloc byte[32];
        privateKey.CopyTo(clamped);
        clamped[0] &= 248;
        clamped[31] &= 127;
        clamped[31] |= 64;

        var k = DecodeLittleEndian(clamped);
        var u = DecodeUCoordinate(rawPub);

        var result = ScalarMultMontgomery(k, u);
        return EncodeUCoordinate(result);
    }

    /// <summary>
    /// Signs a message using the Curve25519 identity private key, producing a 64-byte signature
    /// verifiable against the corresponding Curve25519 identity public key.
    /// </summary>
    /// <param name="privateKey">The 32-byte identity private key.</param>
    /// <param name="message">The raw message bytes to sign.</param>
    /// <returns>A 64-byte signature array.</returns>
    public static byte[] Sign(ReadOnlySpan<byte> privateKey, ReadOnlySpan<byte> message)
    {
        if (privateKey.Length != 32)
        {
            throw new ArgumentException("Private key must be exactly 32 bytes in length.", nameof(privateKey));
        }

        Span<byte> clamped = stackalloc byte[32];
        privateKey.CopyTo(clamped);
        clamped[0] &= 248;
        clamped[31] &= 127;
        clamped[31] |= 64;

        var k = DecodeLittleEndian(clamped) % OrderL;
        var e = EdwardsScalarMult(BasePointEdwards, k);
        BigInteger a;
        (BigInteger X, BigInteger Y) ptA;

        // If E.X is odd (sign bit == 1), negate scalar modulo L per XEdDSA
        if ((e.X & BigInteger.One) == BigInteger.One)
        {
            a = (OrderL - k) % OrderL;
            ptA = EdwardsScalarMult(BasePointEdwards, a);
        }
        else
        {
            a = k;
            ptA = e;
        }

        var aBytes = PointCompress(ptA);

        // Derive deterministic nonce r = SHA512(priv || msg) % L
        var nonceBuf = new byte[32 + message.Length];
        privateKey.CopyTo(nonceBuf.AsSpan(0, 32));
        message.CopyTo(nonceBuf.AsSpan(32));
        var nonceHash = SHA512.HashData(nonceBuf);
        var r = DecodeLittleEndian(nonceHash.AsSpan(0, 32)) % OrderL;
        if (r == BigInteger.Zero)
        {
            r = BigInteger.One;
        }

        var ptR = EdwardsScalarMult(BasePointEdwards, r);
        var rBytes = PointCompress(ptR);

        // Challenge h = SHA512(R || A || message) % L
        var challengeBuf = new byte[32 + 32 + message.Length];
        rBytes.CopyTo(challengeBuf.AsSpan(0));
        aBytes.CopyTo(challengeBuf.AsSpan(32));
        message.CopyTo(challengeBuf.AsSpan(64));

        var hHash = SHA512.HashData(challengeBuf);
        var h = DecodeLittleEndian(hHash.AsSpan(0, 32)) % OrderL;

        // S = (r + h * a) % L
        var s = (r + h * a) % OrderL;
        if (s < 0)
        {
            s += OrderL;
        }

        var signature = new byte[64];
        rBytes.CopyTo(signature.AsSpan(0, 32));
        EncodeLittleEndian(s, signature.AsSpan(32, 32));
        return signature;
    }

    /// <summary>
    /// Verifies a 64-byte signature against the specified Curve25519 identity public key and message.
    /// </summary>
    /// <param name="publicKey">The 32-byte or 33-byte (0x05 prefixed) Curve25519 identity public key.</param>
    /// <param name="message">The signed message bytes.</param>
    /// <param name="signature">The 64-byte signature to verify.</param>
    /// <returns><c>true</c> if the signature is authentic; otherwise, <c>false</c>.</returns>
    public static bool VerifySignature(ReadOnlySpan<byte> publicKey, ReadOnlySpan<byte> message, ReadOnlySpan<byte> signature)
    {
        if (signature.Length != 64)
        {
            return false;
        }

        ReadOnlySpan<byte> rawPub;
        if (publicKey.Length == 33)
        {
            if (publicKey[0] != 0x05)
            {
                return false;
            }

            rawPub = publicKey[1..];
        }
        else if (publicKey.Length == 32)
        {
            rawPub = publicKey;
        }
        else
        {
            return false;
        }

        // Convert Montgomery u to Edwards (x, y): y = (u - 1) / (u + 1) mod p
        var u = DecodeUCoordinate(rawPub);
        var denom = (u + 1) % Prime;
        if (denom == BigInteger.Zero)
        {
            return false;
        }

        var numer = (u - 1) % Prime;
        if (numer < 0)
        {
            numer += Prime;
        }

        var y = (numer * BigInteger.ModPow(denom, Prime - 2, Prime)) % Prime;
        if (y < 0)
        {
            y += Prime;
        }

        var x = RecoverX(y, 0);
        var ptA = (X: x, Y: y);
        var aBytes = PointCompress(ptA);

        var rBytes = signature[..32];
        var ptR = PointDecompress(rBytes);
        if (ptR == null)
        {
            return false;
        }

        var s = DecodeLittleEndian(signature[32..64]);
        if (s >= OrderL)
        {
            return false;
        }

        // Challenge k = SHA512(R || A || message) % L
        var challengeBuf = new byte[32 + 32 + message.Length];
        rBytes.CopyTo(challengeBuf.AsSpan(0));
        aBytes.CopyTo(challengeBuf.AsSpan(32));
        message.CopyTo(challengeBuf.AsSpan(64));

        var kHash = SHA512.HashData(challengeBuf);
        var k = DecodeLittleEndian(kHash.AsSpan(0, 32)) % OrderL;

        // Verify: s * B == R + k * A
        var sb = EdwardsScalarMult(BasePointEdwards, s);
        var ka = EdwardsScalarMult(ptA, k);
        var expected = EdwardsAdd(ptR.Value, ka);

        return sb.X == expected.X && sb.Y == expected.Y;
    }

    private static BigInteger ScalarMultMontgomery(BigInteger k, BigInteger u)
    {
        var x1 = u;
        var x2 = BigInteger.One;
        var z2 = BigInteger.Zero;
        var x3 = u;
        var z3 = BigInteger.One;
        var swap = 0;

        for (var t = 254; t >= 0; t--)
        {
            var kt = (int)((k >> t) & 1);
            swap ^= kt;
            if (swap != 0)
            {
                (x2, x3) = (x3, x2);
                (z2, z3) = (z3, z2);
            }

            swap = kt;

            var a = (x2 + z2) % Prime;
            var aa = (a * a) % Prime;
            var b = (x2 - z2) % Prime;
            if (b < 0) b += Prime;
            var bb = (b * b) % Prime;
            var e = (aa - bb) % Prime;
            if (e < 0) e += Prime;

            var c = (x3 + z3) % Prime;
            var d = (x3 - z3) % Prime;
            if (d < 0) d += Prime;
            var da = (d * a) % Prime;
            var cb = (c * b) % Prime;

            var sumDacb = (da + cb) % Prime;
            x3 = (sumDacb * sumDacb) % Prime;

            var diffDacb = (da - cb) % Prime;
            if (diffDacb < 0) diffDacb += Prime;
            z3 = (x1 * ((diffDacb * diffDacb) % Prime)) % Prime;

            x2 = (aa * bb) % Prime;
            var term = (aa + A24 * e) % Prime;
            z2 = (e * term) % Prime;
        }

        if (swap != 0)
        {
            (x2, x3) = (x3, x2);
            (z2, z3) = (z3, z2);
        }

        var invZ2 = BigInteger.ModPow(z2, Prime - 2, Prime);
        var res = (x2 * invZ2) % Prime;
        if (res < 0) res += Prime;
        return res;
    }

    private static (BigInteger X, BigInteger Y) EdwardsAdd((BigInteger X, BigInteger Y) p1, (BigInteger X, BigInteger Y) p2)
    {
        var x1 = p1.X; var y1 = p1.Y;
        var x2 = p2.X; var y2 = p2.Y;

        var term = (D * (((x1 * x2) % Prime) * ((y1 * y2) % Prime)) % Prime) % Prime;
        var denom1 = (1 + term) % Prime;
        if (denom1 < 0) denom1 += Prime;
        var denom2 = (1 - term) % Prime;
        if (denom2 < 0) denom2 += Prime;

        var numerX = ((x1 * y2) + (y1 * x2)) % Prime;
        if (numerX < 0) numerX += Prime;
        var numerY = ((y1 * y2) + (x1 * x2)) % Prime;
        if (numerY < 0) numerY += Prime;

        var x3 = (numerX * BigInteger.ModPow(denom1, Prime - 2, Prime)) % Prime;
        var y3 = (numerY * BigInteger.ModPow(denom2, Prime - 2, Prime)) % Prime;
        return (x3, y3);
    }

    private static (BigInteger X, BigInteger Y) EdwardsScalarMult((BigInteger X, BigInteger Y) p, BigInteger s)
    {
        var r = (X: BigInteger.Zero, Y: BigInteger.One);
        s %= OrderL;
        if (s < 0) s += OrderL;
        var cur = p;

        while (s > BigInteger.Zero)
        {
            if ((s & BigInteger.One) == BigInteger.One)
            {
                r = EdwardsAdd(r, cur);
            }

            cur = EdwardsAdd(cur, cur);
            s >>= 1;
        }

        return r;
    }

    private static byte[] PointCompress((BigInteger X, BigInteger Y) pt)
    {
        var res = new byte[32];
        var y = pt.Y % Prime;
        if (y < 0) y += Prime;
        var sign = (pt.X & BigInteger.One) == BigInteger.One ? 1 : 0;
        y |= (BigInteger)sign << 255;
        EncodeLittleEndian(y, res);
        return res;
    }

    private static (BigInteger X, BigInteger Y)? PointDecompress(ReadOnlySpan<byte> s)
    {
        if (s.Length != 32) return null;
        var raw = DecodeLittleEndian(s);
        var sign = (int)((raw >> 255) & 1);
        var y = raw & ((BigInteger.One << 255) - 1);
        if (y >= Prime) return null;

        var x = RecoverX(y, sign);
        return (x, y);
    }

    private static BigInteger RecoverX(BigInteger y, int sign)
    {
        var y2 = (y * y) % Prime;
        var numer = (y2 - 1) % Prime;
        if (numer < 0) numer += Prime;
        var denom = ((D * y2) + 1) % Prime;
        if (denom < 0) denom += Prime;

        var xx = (numer * BigInteger.ModPow(denom, Prime - 2, Prime)) % Prime;
        if (xx == BigInteger.Zero)
        {
            return BigInteger.Zero;
        }

        var x = BigInteger.ModPow(xx, (Prime + 3) / 8, Prime);
        if (((x * x) - xx) % Prime != BigInteger.Zero)
        {
            x = (x * SqrtM1) % Prime;
        }

        if (((x * x) - xx) % Prime != BigInteger.Zero)
        {
            x = BigInteger.Zero;
        }

        if ((int)(x & BigInteger.One) != sign)
        {
            x = (Prime - x) % Prime;
        }

        return x;
    }

    private static BigInteger DecodeLittleEndian(ReadOnlySpan<byte> b)
    {
        BigInteger val = BigInteger.Zero;
        for (var i = 0; i < b.Length; i++)
        {
            val |= (BigInteger)b[i] << (8 * i);
        }

        return val;
    }

    private static BigInteger DecodeUCoordinate(ReadOnlySpan<byte> b)
    {
        Span<byte> copy = stackalloc byte[32];
        b[..32].CopyTo(copy);
        copy[31] &= 127;
        return DecodeLittleEndian(copy) % Prime;
    }

    private static byte[] EncodeUCoordinate(BigInteger u)
    {
        var outBytes = new byte[32];
        EncodeLittleEndian(u % Prime, outBytes);
        return outBytes;
    }

    private static void EncodeLittleEndian(BigInteger val, Span<byte> destination)
    {
        destination.Clear();
        for (var i = 0; i < destination.Length && val > BigInteger.Zero; i++)
        {
            destination[i] = (byte)(val & 0xFF);
            val >>= 8;
        }
    }
}
