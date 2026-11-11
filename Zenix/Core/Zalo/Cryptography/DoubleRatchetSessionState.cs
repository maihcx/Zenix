// Copyright (c) Zenix. All rights reserved.
// Licensed under the proprietary and confidential license.
// Unauthorized copying, distribution, modification, or disclosure of this file,
// via any medium, is strictly prohibited without prior written permission.

using System;
using System.Collections.Generic;
using System.Security.Cryptography;

namespace Zenix.Core.Zalo.Cryptography;

/// <summary>
/// Maintains the ephemeral cryptographic state and ratcheted symmetric key chains
/// for an active Signal Double Ratchet session between two endpoints.
/// </summary>
public sealed class DoubleRatchetSessionState : IDisposable
{
    private bool _isDisposed;

    /// <summary>
    /// Gets or sets the current 32-byte Double Ratchet root key.
    /// </summary>
    public byte[] RootKey { get; set; } = [];

    /// <summary>
    /// Gets or sets the 32-byte symmetric sending chain key.
    /// </summary>
    public byte[]? SendingChainKey { get; set; }

    /// <summary>
    /// Gets or sets the 32-byte symmetric receiving chain key.
    /// </summary>
    public byte[]? ReceivingChainKey { get; set; }

    /// <summary>
    /// Gets or sets our local ephemeral key pair (DH ratchet key pair).
    /// </summary>
    public (byte[] PrivateKey, byte[] PublicKey) LocalEphemeralKeyPair { get; set; }

    /// <summary>
    /// Gets or sets the last received remote ephemeral public key.
    /// </summary>
    public byte[]? RemoteEphemeralPublicKey { get; set; }

    /// <summary>
    /// Gets or sets our 33-byte Curve25519 identity public key.
    /// </summary>
    public byte[] OurIdentityPublicKey { get; set; } = [];

    /// <summary>
    /// Gets or sets the remote peer's 33-byte Curve25519 identity public key.
    /// </summary>
    public byte[] RemoteIdentityPublicKey { get; set; } = [];

    /// <summary>
    /// Gets or sets the sequence counter for sent messages in the current ratchet chain.
    /// </summary>
    public uint SendMessageCounter { get; set; }

    /// <summary>
    /// Gets or sets the sequence counter for received messages in the current ratchet chain.
    /// </summary>
    public uint ReceiveMessageCounter { get; set; }

    /// <summary>
    /// Gets or sets the message counter from the previous sending chain.
    /// </summary>
    public uint PreviousCounter { get; set; }

    /// <summary>
    /// Gets or sets the remote peer's registration identifier.
    /// </summary>
    public int RemoteRegistrationId { get; set; }

    /// <summary>
    /// Cached skipped message keys for packets arriving out-of-order, indexed by (remoteEphemeralHex, counter).
    /// </summary>
    public Dictionary<(string EphemeralHex, uint Counter), (byte[] MessageKey, DateTime CreatedAt)> SkippedMessageKeys { get; } = new();

    /// <summary>
    /// Prunes cached skipped message keys that exceed the specified time-to-live.
    /// </summary>
    /// <param name="ttl">The maximum lifetime for skipped keys.</param>
    public void PruneExpiredSkippedKeys(TimeSpan ttl)
    {
        var cutoff = DateTime.UtcNow - ttl;
        var expiredKeys = new List<(string EphemeralHex, uint Counter)>();

        foreach (var (key, value) in SkippedMessageKeys)
        {
            if (value.CreatedAt < cutoff)
            {
                expiredKeys.Add(key);
                CryptographicOperations.ZeroMemory(value.MessageKey);
            }
        }

        foreach (var key in expiredKeys)
        {
            SkippedMessageKeys.Remove(key);
        }
    }

    /// <summary>
    /// Creates an independent deep copy of the Double Ratchet session state for staged candidate calculations.
    /// </summary>
    /// <returns>A new <see cref="DoubleRatchetSessionState"/> candidate clone.</returns>
    public DoubleRatchetSessionState Clone()
    {
        var clone = new DoubleRatchetSessionState
        {
            RootKey = (byte[])RootKey.Clone(),
            SendingChainKey = SendingChainKey != null ? (byte[])SendingChainKey.Clone() : null,
            ReceivingChainKey = ReceivingChainKey != null ? (byte[])ReceivingChainKey.Clone() : null,
            LocalEphemeralKeyPair = (
                LocalEphemeralKeyPair.PrivateKey != null ? (byte[])LocalEphemeralKeyPair.PrivateKey.Clone() : [],
                LocalEphemeralKeyPair.PublicKey != null ? (byte[])LocalEphemeralKeyPair.PublicKey.Clone() : []
            ),
            RemoteEphemeralPublicKey = RemoteEphemeralPublicKey != null ? (byte[])RemoteEphemeralPublicKey.Clone() : null,
            OurIdentityPublicKey = (byte[])OurIdentityPublicKey.Clone(),
            RemoteIdentityPublicKey = (byte[])RemoteIdentityPublicKey.Clone(),
            SendMessageCounter = SendMessageCounter,
            ReceiveMessageCounter = ReceiveMessageCounter,
            PreviousCounter = PreviousCounter,
            RemoteRegistrationId = RemoteRegistrationId
        };

        foreach (var (key, value) in SkippedMessageKeys)
        {
            clone.SkippedMessageKeys[key] = ((byte[])value.MessageKey.Clone(), value.CreatedAt);
        }

        return clone;
    }

    /// <summary>
    /// Atomically commits this candidate state's mutations to the target live session state,
    /// securely zeroing the target's superseded keys and transferring buffer ownership.
    /// </summary>
    /// <param name="target">The live session state to update.</param>
    public void CommitTo(DoubleRatchetSessionState target)
    {
        ArgumentNullException.ThrowIfNull(target);

        // 1. Securely zero target's previous cryptographic keys
        CryptographicOperations.ZeroMemory(target.RootKey);
        if (target.SendingChainKey != null) CryptographicOperations.ZeroMemory(target.SendingChainKey);
        if (target.ReceivingChainKey != null) CryptographicOperations.ZeroMemory(target.ReceivingChainKey);
        if (target.LocalEphemeralKeyPair.PrivateKey != null) CryptographicOperations.ZeroMemory(target.LocalEphemeralKeyPair.PrivateKey);

        foreach (var entry in target.SkippedMessageKeys.Values)
        {
            CryptographicOperations.ZeroMemory(entry.MessageKey);
        }
        target.SkippedMessageKeys.Clear();

        // 2. Transfer state buffers to target
        target.RootKey = RootKey;
        target.SendingChainKey = SendingChainKey;
        target.ReceivingChainKey = ReceivingChainKey;
        target.LocalEphemeralKeyPair = LocalEphemeralKeyPair;
        target.RemoteEphemeralPublicKey = RemoteEphemeralPublicKey;
        target.OurIdentityPublicKey = OurIdentityPublicKey;
        target.RemoteIdentityPublicKey = RemoteIdentityPublicKey;
        target.SendMessageCounter = SendMessageCounter;
        target.ReceiveMessageCounter = ReceiveMessageCounter;
        target.PreviousCounter = PreviousCounter;
        target.RemoteRegistrationId = RemoteRegistrationId;

        foreach (var (key, value) in SkippedMessageKeys)
        {
            target.SkippedMessageKeys[key] = value;
        }

        // 3. Detach sensitive references from candidate so Dispose() does not wipe transferred buffers
        RootKey = [];
        SendingChainKey = null;
        ReceivingChainKey = null;
        LocalEphemeralKeyPair = ([], []);
        RemoteEphemeralPublicKey = null;
        SkippedMessageKeys.Clear();
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        _isDisposed = true;

        CryptographicOperations.ZeroMemory(RootKey);
        if (SendingChainKey != null) CryptographicOperations.ZeroMemory(SendingChainKey);
        if (ReceivingChainKey != null) CryptographicOperations.ZeroMemory(ReceivingChainKey);
        if (LocalEphemeralKeyPair.PrivateKey != null) CryptographicOperations.ZeroMemory(LocalEphemeralKeyPair.PrivateKey);

        foreach (var entry in SkippedMessageKeys.Values)
        {
            CryptographicOperations.ZeroMemory(entry.MessageKey);
        }

        SkippedMessageKeys.Clear();
    }
}
