// Copyright (c) Zenix. All rights reserved.
// Licensed under the proprietary and confidential license.
// Unauthorized copying, distribution, modification, or disclosure of this file,
// via any medium, is strictly prohibited without prior written permission.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using Zenix.Core.Zalo.Contracts;

namespace Zenix.Core.Zalo.Cryptography;

/// <summary>
/// Implements the Extended Triple Diffie-Hellman (X3DH) asynchronous key agreement protocol
/// for Signal Protocol E2EE session establishment in Zalo.
/// </summary>
public sealed class SignalX3dhService
{
    private static readonly byte[] ZaloTextInfo = Encoding.UTF8.GetBytes("ZaloText");
    private static readonly byte[] ZeroSalt32 = new byte[32];

    private readonly ConcurrentDictionary<string, UserKeyStore> _stores = new(StringComparer.Ordinal);

    /// <summary>
    /// Internal in-memory key container for an identity.
    /// </summary>
    public sealed class UserKeyStore
    {
        /// <summary>
        /// Gets the unique user identifier.
        /// </summary>
        public string UserId { get; }

        /// <summary>
        /// Gets the 14-bit unsigned registration identifier (0..16383).
        /// </summary>
        public int RegistrationId { get; }

        /// <summary>
        /// Gets our Curve25519 identity key pair.
        /// </summary>
        public (byte[] PrivateKey, byte[] PublicKey) IdentityKeyPair { get; }

        /// <summary>
        /// Gets our current signed prekey.
        /// </summary>
        public (uint KeyId, byte[] PrivateKey, byte[] PublicKey, byte[] Signature) SignedPreKey { get; }

        /// <summary>
        /// Gets the pool of available one-time prekeys.
        /// </summary>
        public ConcurrentQueue<(uint KeyId, byte[] PrivateKey, byte[] PublicKey)> OneTimePreKeys { get; } = new();

        /// <summary>
        /// Gets a dictionary of claimed one-time prekeys indexed by key ID.
        /// </summary>
        public ConcurrentDictionary<uint, byte[]> ClaimedPreKeyPrivates { get; } = new();

        private int _nextPreKeyId = 1;

        /// <summary>
        /// Initializes a new instance of the <see cref="UserKeyStore"/> class.
        /// </summary>
        public UserKeyStore(string userId)
        {
            UserId = userId;
            RegistrationId = RandomNumberGenerator.GetInt32(0, 16384);
            IdentityKeyPair = Curve25519Helper.GenerateKeyPair(includePrefix: true);

            var spk = Curve25519Helper.GenerateKeyPair(includePrefix: true);
            var sig = Curve25519Helper.Sign(IdentityKeyPair.PrivateKey, spk.PublicKey);
            SignedPreKey = (1, spk.PrivateKey, spk.PublicKey, sig);

            RefillPreKeys(5);
        }

        /// <summary>
        /// Generates a batch of new one-time prekeys.
        /// </summary>
        /// <param name="count">The number of prekeys to generate.</param>
        public void RefillPreKeys(int count = 5)
        {
            for (var i = 0; i < count; i++)
            {
                var id = (uint)Interlocked.Increment(ref _nextPreKeyId);
                var kp = Curve25519Helper.GenerateKeyPair(includePrefix: true);
                OneTimePreKeys.Enqueue((id, kp.PrivateKey, kp.PublicKey));
                ClaimedPreKeyPrivates[id] = kp.PrivateKey;
            }
        }
    }

    /// <summary>
    /// Registers or loads the user's in-memory key store, generating identity and prekeys if not already present.
    /// </summary>
    /// <param name="userId">The unique user identifier.</param>
    /// <returns>The user's key store.</returns>
    public UserKeyStore GetOrCreateUserStore(string userId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        return _stores.GetOrAdd(userId, id => new UserKeyStore(id));
    }

    /// <summary>
    /// Produces a public <see cref="SignalPreKeyBundle"/> for the user to be published or given to an initiator.
    /// Consumes one one-time prekey from the pool.
    /// </summary>
    /// <param name="userId">The identifier of the user whose bundle is requested.</param>
    /// <returns>A newly allocated <see cref="SignalPreKeyBundle"/>.</returns>
    public SignalPreKeyBundle CreatePreKeyBundle(string userId)
    {
        var store = GetOrCreateUserStore(userId);

        if (store.OneTimePreKeys.IsEmpty)
        {
            store.RefillPreKeys(5);
        }

        uint? opkId = null;
        byte[]? opkPub = null;

        if (store.OneTimePreKeys.TryDequeue(out var opk))
        {
            opkId = opk.KeyId;
            opkPub = opk.PublicKey;
        }

        return new SignalPreKeyBundle(
            IdentityKey: (byte[])store.IdentityKeyPair.PublicKey.Clone(),
            RegistrationId: store.RegistrationId,
            SignedPreKeyId: store.SignedPreKey.KeyId,
            SignedPreKey: (byte[])store.SignedPreKey.PublicKey.Clone(),
            Signature: (byte[])store.SignedPreKey.Signature.Clone(),
            OneTimePreKeyId: opkId,
            OneTimePreKey: opkPub);
    }

    /// <summary>
    /// Computes the X3DH master shared secret from the initiator's perspective (Alice).
    /// </summary>
    /// <param name="initiatorStore">Alice's key store.</param>
    /// <param name="recipientBundle">Bob's public prekey bundle.</param>
    /// <param name="ephemeralKeyPair">Alice's generated ephemeral key pair (baseKey).</param>
    /// <returns>A 64-byte derived key array (32 bytes rootKey + 32 bytes initial sendingChainKey).</returns>
    /// <exception cref="CryptographicException">Thrown when signed prekey signature verification fails.</exception>
    public byte[] InitiateHandshake(
        UserKeyStore initiatorStore,
        SignalPreKeyBundle recipientBundle,
        (byte[] PrivateKey, byte[] PublicKey) ephemeralKeyPair)
    {
        ArgumentNullException.ThrowIfNull(initiatorStore);
        ArgumentNullException.ThrowIfNull(recipientBundle);

        // Verify Bob's signed prekey signature using Bob's identity key
        var isSigValid = Curve25519Helper.VerifySignature(
            recipientBundle.IdentityKey,
            recipientBundle.SignedPreKey,
            recipientBundle.Signature);

        if (!isSigValid)
        {
            throw new CryptographicException("Bob's signed prekey signature verification failed against Bob's identity key.");
        }

        // Agreements:
        // DH1 = ECDHE(theirSignedPreKey, ourIdentityPrivKey)
        var dh1 = Curve25519Helper.CalculateAgreement(initiatorStore.IdentityKeyPair.PrivateKey, recipientBundle.SignedPreKey);

        // DH2 = ECDHE(theirIdentityKey, ourBasePrivKey)
        var dh2 = Curve25519Helper.CalculateAgreement(ephemeralKeyPair.PrivateKey, recipientBundle.IdentityKey);

        // DH3 = ECDHE(theirSignedPreKey, ourBasePrivKey)
        var dh3 = Curve25519Helper.CalculateAgreement(ephemeralKeyPair.PrivateKey, recipientBundle.SignedPreKey);

        // DH4 = ECDHE(theirOneTimePreKey, ourBasePrivKey) (optional)
        byte[]? dh4 = null;
        if (recipientBundle.OneTimePreKey != null && recipientBundle.OneTimePreKey.Length > 0)
        {
            dh4 = Curve25519Helper.CalculateAgreement(ephemeralKeyPair.PrivateKey, recipientBundle.OneTimePreKey);
        }

        return DeriveMasterSecret(dh1, dh2, dh3, dh4);
    }

    /// <summary>
    /// Computes the X3DH master shared secret from the recipient's perspective (Bob).
    /// </summary>
    /// <param name="recipientStore">Bob's key store.</param>
    /// <param name="aliceIdentityKey">Alice's identity public key (33 bytes or 32 bytes).</param>
    /// <param name="aliceBaseKey">Alice's ephemeral public key (baseKey, 33 bytes or 32 bytes).</param>
    /// <param name="oneTimePreKeyId">The one-time prekey ID used by Alice, if any.</param>
    /// <returns>A 64-byte derived key array matching Alice's derived master secret.</returns>
    public byte[] ReceiveHandshake(
        UserKeyStore recipientStore,
        ReadOnlySpan<byte> aliceIdentityKey,
        ReadOnlySpan<byte> aliceBaseKey,
        uint? oneTimePreKeyId)
    {
        ArgumentNullException.ThrowIfNull(recipientStore);

        // DH1 = ECDHE(theirIdentityKey, ourSignedPreKeyPrivKey)
        var dh1 = Curve25519Helper.CalculateAgreement(recipientStore.SignedPreKey.PrivateKey, aliceIdentityKey);

        // DH2 = ECDHE(theirBaseKey, ourIdentityPrivKey)
        var dh2 = Curve25519Helper.CalculateAgreement(recipientStore.IdentityKeyPair.PrivateKey, aliceBaseKey);

        // DH3 = ECDHE(theirBaseKey, ourSignedPreKeyPrivKey)
        var dh3 = Curve25519Helper.CalculateAgreement(recipientStore.SignedPreKey.PrivateKey, aliceBaseKey);

        // DH4 = ECDHE(theirBaseKey, ourOneTimePreKeyPrivKey) (optional)
        byte[]? dh4 = null;
        if (oneTimePreKeyId.HasValue && recipientStore.ClaimedPreKeyPrivates.TryGetValue(oneTimePreKeyId.Value, out var opkPriv))
        {
            dh4 = Curve25519Helper.CalculateAgreement(opkPriv, aliceBaseKey);
        }

        return DeriveMasterSecret(dh1, dh2, dh3, dh4);
    }

    private static byte[] DeriveMasterSecret(byte[] dh1, byte[] dh2, byte[] dh3, byte[]? dh4)
    {
        var uLen = 32 + 32 + 32 + 32 + (dh4 != null ? 32 : 0);
        var u = new byte[uLen];

        // 32 bytes of 0xFF prefix
        u.AsSpan(0, 32).Fill(0xFF);

        dh1.CopyTo(u.AsSpan(32, 32));
        dh2.CopyTo(u.AsSpan(64, 32));
        dh3.CopyTo(u.AsSpan(96, 32));

        if (dh4 != null)
        {
            dh4.CopyTo(u.AsSpan(128, 32));
        }

        Span<byte> derived = stackalloc byte[64];
        try
        {
            HKDF.DeriveKey(HashAlgorithmName.SHA256, u, derived, ZeroSalt32, ZaloTextInfo);
            return derived.ToArray();
        }
        finally
        {
            CryptographicOperations.ZeroMemory(u);
            CryptographicOperations.ZeroMemory(derived);
            CryptographicOperations.ZeroMemory(dh1);
            CryptographicOperations.ZeroMemory(dh2);
            CryptographicOperations.ZeroMemory(dh3);
            if (dh4 != null)
            {
                CryptographicOperations.ZeroMemory(dh4);
            }
        }
    }
}
