// Copyright (c) Zenix. All rights reserved.
// Licensed under the proprietary and confidential license.
// Unauthorized copying, distribution, modification, or disclosure of this file,
// via any medium, is strictly prohibited without prior written permission.

using System;
using System.Threading;
using System.Threading.Tasks;

namespace Zenix.Core.Zalo.Contracts;

/// <summary>
/// Represents a serialized Signal Protocol message wire envelope.
/// </summary>
/// <param name="Type">The message type: 3 for PreKeyWhisperMessage, 1 for WhisperMessage.</param>
/// <param name="Body">The Base64-encoded ciphertext payload.</param>
/// <param name="RegistrationId">The sender's unique registration identifier.</param>
public sealed record SignalMessageEnvelope(int Type, string Body, int RegistrationId)
{
    /// <summary>
    /// Gets a value indicating whether this envelope encapsulates a session setup PreKeyWhisperMessage (type 3).
    /// </summary>
    public bool IsPreKeyMessage => Type == 3;
}

/// <summary>
/// Represents a public cryptographic bundle used to establish an asynchronous Signal X3DH session.
/// </summary>
/// <param name="IdentityKey">The Curve25519 identity public key bytes (33 bytes with 0x05 prefix or 32 raw bytes).</param>
/// <param name="RegistrationId">The 14-bit unsigned registration identifier.</param>
/// <param name="SignedPreKeyId">The identifier of the signed prekey.</param>
/// <param name="SignedPreKey">The Curve25519 signed prekey public key bytes.</param>
/// <param name="Signature">The 64-byte signature over the signed prekey public key.</param>
/// <param name="OneTimePreKeyId">The optional identifier of the one-time prekey.</param>
/// <param name="OneTimePreKey">The optional Curve25519 one-time prekey public key bytes.</param>
public sealed record SignalPreKeyBundle(
    byte[] IdentityKey,
    int RegistrationId,
    uint SignedPreKeyId,
    byte[] SignedPreKey,
    byte[] Signature,
    uint? OneTimePreKeyId,
    byte[]? OneTimePreKey);

/// <summary>
/// Defines asynchronous end-to-end encryption services leveraging the Signal Protocol
/// (X3DH key agreement and Double Ratchet session framing) for secure 1-1 communication.
/// </summary>
public interface ISignalE2eeService
{
    /// <summary>
    /// Registers or retrieves an identity for the specified user, generating identity keys,
    /// signed prekeys, and a batch of one-time prekeys if not already present.
    /// </summary>
    /// <param name="userId">The unique identifier of the user identity.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The 14-bit unsigned registration ID of the registered identity.</returns>
    Task<int> RegisterIdentityAsync(string userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks whether an open Double Ratchet session exists between the sender and recipient.
    /// </summary>
    /// <param name="senderId">The sender's user identifier.</param>
    /// <param name="recipientId">The recipient's user identifier.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns><c>true</c> if an active session exists; otherwise, <c>false</c>.</returns>
    Task<bool> HasSessionAsync(string senderId, string recipientId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Exports a public PreKey bundle for the specified user suitable for initiating an X3DH session.
    /// Consumes one one-time prekey from the available pool.
    /// </summary>
    /// <param name="userId">The identifier of the user whose bundle is requested.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A newly allocated <see cref="SignalPreKeyBundle"/>.</returns>
    Task<SignalPreKeyBundle> GetPreKeyBundleAsync(string userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Encrypts UTF-8 plaintext from a sender to a recipient using Signal Protocol.
    /// Automatically establishes an X3DH session (producing a Type 3 PreKeyWhisperMessage) if no session exists,
    /// or ratchets forward producing a Type 1 WhisperMessage.
    /// </summary>
    /// <param name="plaintext">The UTF-8 plaintext message to encrypt.</param>
    /// <param name="senderId">The sender's user identifier.</param>
    /// <param name="recipientId">The recipient's user identifier.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The encrypted <see cref="SignalMessageEnvelope"/> containing type, body, and registration ID.</returns>
    Task<SignalMessageEnvelope> EncryptAsync(string plaintext, string senderId, string recipientId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Decrypts a Signal Protocol message envelope at the recipient, advancing the receiving ratchet
    /// or processing an inbound session setup prekey message.
    /// </summary>
    /// <param name="envelope">The received <see cref="SignalMessageEnvelope"/>.</param>
    /// <param name="recipientId">The recipient's user identifier.</param>
    /// <param name="senderId">The sender's user identifier.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The decrypted UTF-8 plaintext message string.</returns>
    Task<string> DecryptAsync(SignalMessageEnvelope envelope, string recipientId, string senderId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Computes the 60-digit numeric Safety Number between two registered identities
    /// formatted into 12 space-separated blocks of 5 decimal digits for visual verification.
    /// </summary>
    /// <param name="userIdA">The first user identifier.</param>
    /// <param name="userIdB">The second user identifier.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A 60-digit string formatted in 12 groups of 5 decimal digits.</returns>
    Task<string> ComputeSafetyNumberAsync(string userIdA, string userIdB, CancellationToken cancellationToken = default);
}
