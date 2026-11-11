// Copyright (c) Zenix. All rights reserved.
// Licensed under the proprietary and confidential license.
// Unauthorized copying, distribution, modification, or disclosure of this file,
// via any medium, is strictly prohibited without prior written permission.

using System;
using System.Collections.Concurrent;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Zenix.Core.Zalo.Contracts;

namespace Zenix.Core.Zalo.Cryptography;

/// <summary>
/// Implements the <see cref="ISignalE2eeService"/> contract, coordinating asynchronous X3DH session initialization,
/// Double Ratchet encryption/decryption, wire serialization/deserialization for PreKeyWhisperMessage (type 3)
/// and WhisperMessage (type 1), and 60-digit mutual safety number generation.
/// </summary>
public sealed class SignalE2eeService : ISignalE2eeService
{
    private const byte WireVersion = 0x33; // 51 in decimal

    private readonly SignalX3dhService _x3dhService = new();
    private readonly SignalDoubleRatchetService _ratchetService = new();

    // Session cache: (senderId, recipientId) -> DoubleRatchetSessionState
    private readonly ConcurrentDictionary<string, DoubleRatchetSessionState> _sessions = new(StringComparer.Ordinal);

    private static string GetSessionKey(string localId, string remoteId) => $"{localId}:{remoteId}";

    /// <inheritdoc/>
    public Task<int> RegisterIdentityAsync(string userId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);

        var store = _x3dhService.GetOrCreateUserStore(userId);
        return Task.FromResult(store.RegistrationId);
    }

    /// <inheritdoc/>
    public Task<bool> HasSessionAsync(string senderId, string recipientId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentException.ThrowIfNullOrWhiteSpace(senderId);
        ArgumentException.ThrowIfNullOrWhiteSpace(recipientId);

        var key = GetSessionKey(senderId, recipientId);
        return Task.FromResult(_sessions.ContainsKey(key));
    }

    /// <inheritdoc/>
    public Task<SignalPreKeyBundle> GetPreKeyBundleAsync(string userId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);

        return Task.FromResult(_x3dhService.CreatePreKeyBundle(userId));
    }

    /// <inheritdoc/>
    public Task<SignalMessageEnvelope> EncryptAsync(
        string plaintext,
        string senderId,
        string recipientId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(plaintext);
        ArgumentException.ThrowIfNullOrWhiteSpace(senderId);
        ArgumentException.ThrowIfNullOrWhiteSpace(recipientId);

        var sessionKey = GetSessionKey(senderId, recipientId);
        var senderStore = _x3dhService.GetOrCreateUserStore(senderId);

        bool isPreKeyMessage = false;
        (byte[] PrivateKey, byte[] PublicKey)? aliceBaseKeyPair = null;
        SignalPreKeyBundle? recipientBundle = null;

        if (!_sessions.TryGetValue(sessionKey, out var session))
        {
            // Initiate new X3DH session
            recipientBundle = _x3dhService.CreatePreKeyBundle(recipientId);
            aliceBaseKeyPair = Curve25519Helper.GenerateKeyPair(includePrefix: true);

            var masterSecret = _x3dhService.InitiateHandshake(senderStore, recipientBundle, aliceBaseKeyPair.Value);

            session = _ratchetService.InitializeInitiatorSession(
                masterSecret,
                recipientBundle.SignedPreKey,
                senderStore.IdentityKeyPair.PublicKey,
                recipientBundle.IdentityKey,
                recipientBundle.RegistrationId);

            _sessions[sessionKey] = session;
            isPreKeyMessage = true;
        }

        // Encrypt message on current Double Ratchet sending chain
        var plaintextBytes = Encoding.UTF8.GetBytes(plaintext);
        var (ciphertext, counter, prevCounter, ephemeralPub, macKey) = _ratchetService.Encrypt(session, plaintextBytes);

        // Serialize inner WhisperMessage protobuf
        var whisperProto = EncodeWhisperMessage(ephemeralPub, counter, prevCounter, ciphertext);

        // Frame inner WhisperMessage wire array: [0x33: 1B] || [Proto: NB] || [HMAC: 8B]
        var whisperWire = FrameWhisperWire(whisperProto, session.OurIdentityPublicKey, session.RemoteIdentityPublicKey, macKey);

        if (isPreKeyMessage && aliceBaseKeyPair.HasValue && recipientBundle != null)
        {
            // Serialize outer PreKeyWhisperMessage protobuf
            var preKeyProto = EncodePreKeyWhisperMessage(
                recipientBundle.OneTimePreKeyId,
                aliceBaseKeyPair.Value.PublicKey,
                senderStore.IdentityKeyPair.PublicKey,
                whisperWire,
                (uint)senderStore.RegistrationId,
                recipientBundle.SignedPreKeyId);

            // Frame PreKey wire: [0x33: 1B] || [Proto: MB]
            var preKeyWire = new byte[1 + preKeyProto.Length];
            preKeyWire[0] = WireVersion;
            preKeyProto.CopyTo(preKeyWire.AsSpan(1));

            var body = Convert.ToBase64String(preKeyWire);
            return Task.FromResult(new SignalMessageEnvelope(3, body, session.RemoteRegistrationId));
        }
        else
        {
            var body = Convert.ToBase64String(whisperWire);
            return Task.FromResult(new SignalMessageEnvelope(1, body, session.RemoteRegistrationId));
        }
    }

    /// <inheritdoc/>
    public Task<string> DecryptAsync(
        SignalMessageEnvelope envelope,
        string recipientId,
        string senderId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(envelope);
        ArgumentException.ThrowIfNullOrWhiteSpace(recipientId);
        ArgumentException.ThrowIfNullOrWhiteSpace(senderId);

        var recipientStore = _x3dhService.GetOrCreateUserStore(recipientId);
        var sessionKey = GetSessionKey(recipientId, senderId);
        var envelopeBytes = Convert.FromBase64String(envelope.Body);

        if (envelopeBytes.Length < 1)
        {
            throw new CryptographicException("Received empty message envelope.");
        }

        var versionByte = envelopeBytes[0];
        if ((versionByte & 15) > 3 || (versionByte >> 4) < 3)
        {
            throw new CryptographicException($"Incompatible version number 0x{versionByte:X2} on message envelope.");
        }

        if (envelope.Type == 3)
        {
            // Type 3: PreKeyWhisperMessage
            var preKeyProto = envelopeBytes.AsSpan(1);
            var (opkId, aliceBaseKey, aliceIdKey, innerWhisperWire, senderRegId, _) = DecodePreKeyWhisperMessage(preKeyProto);

            // Perform X3DH handshake calculation at Bob
            var masterSecret = _x3dhService.ReceiveHandshake(recipientStore, aliceIdKey, aliceBaseKey, opkId);

            var session = _ratchetService.InitializeRecipientSession(
                masterSecret,
                (recipientStore.SignedPreKey.PrivateKey, recipientStore.SignedPreKey.PublicKey),
                recipientStore.IdentityKeyPair.PublicKey,
                aliceIdKey,
                (int)senderRegId);

            // Decrypt inner WhisperMessage wire FIRST
            var plaintext = DecryptWhisperWire(innerWhisperWire, session);

            // Only insert into _sessions if authentication and decryption succeeded
            _sessions[sessionKey] = session;

            return Task.FromResult(plaintext);
        }
        else if (envelope.Type == 1)
        {
            // Type 1: WhisperMessage
            if (!_sessions.TryGetValue(sessionKey, out var session))
            {
                throw new CryptographicException($"No open Double Ratchet session found for communication between '{recipientId}' and '{senderId}'.");
            }

            return Task.FromResult(DecryptWhisperWire(envelopeBytes, session));
        }
        else
        {
            throw new CryptographicException($"Unknown Signal message type: {envelope.Type}");
        }
    }

    /// <inheritdoc/>
    public Task<string> ComputeSafetyNumberAsync(string userIdA, string userIdB, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentException.ThrowIfNullOrWhiteSpace(userIdA);
        ArgumentException.ThrowIfNullOrWhiteSpace(userIdB);

        var storeA = _x3dhService.GetOrCreateUserStore(userIdA);
        var storeB = _x3dhService.GetOrCreateUserStore(userIdB);

        var safetyNumber = SafetyNumberFormatter.ComputeSafetyNumber(
            userIdA,
            storeA.IdentityKeyPair.PublicKey,
            userIdB,
            storeB.IdentityKeyPair.PublicKey);

        return Task.FromResult(safetyNumber);
    }

    private static byte[] FrameWhisperWire(
        byte[] whisperProto,
        ReadOnlySpan<byte> ourIdentityKey,
        ReadOnlySpan<byte> remoteIdentityKey,
        ReadOnlySpan<byte> macKey)
    {
        // MAC Input: [SenderIK: 33B][ReceiverIK: 33B][0x33: 1B][ProtobufBytes: NB]
        var macInputLen = ourIdentityKey.Length + remoteIdentityKey.Length + 1 + whisperProto.Length;
        var macInput = new byte[macInputLen];
        ourIdentityKey.CopyTo(macInput.AsSpan(0));
        remoteIdentityKey.CopyTo(macInput.AsSpan(ourIdentityKey.Length));
        macInput[ourIdentityKey.Length + remoteIdentityKey.Length] = WireVersion;
        whisperProto.CopyTo(macInput.AsSpan(ourIdentityKey.Length + remoteIdentityKey.Length + 1));

        var fullHmac = HMACSHA256.HashData(macKey, macInput);

        // Wire buffer: [0x33: 1B] || [Protobuf: NB] || [HMAC: 8B]
        var wire = new byte[1 + whisperProto.Length + 8];
        wire[0] = WireVersion;
        whisperProto.CopyTo(wire.AsSpan(1));
        fullHmac.AsSpan(0, 8).CopyTo(wire.AsSpan(1 + whisperProto.Length, 8));

        return wire;
    }

    private static string DecryptWhisperWire(ReadOnlySpan<byte> wireBytes, DoubleRatchetSessionState session)
    {
        if (wireBytes.Length < 1 + 1 + 8) // version (1B) + min proto (1B) + HMAC (8B)
        {
            throw new CryptographicException("Whisper message payload is too short to be valid.");
        }

        var versionByte = wireBytes[0];
        if ((versionByte & 15) > 3 || (versionByte >> 4) < 3)
        {
            throw new CryptographicException($"Incompatible version number 0x{versionByte:X2} on WhisperMessage.");
        }

        var protoBytes = wireBytes[1..^8];
        var wireHmac = wireBytes[^8..];

        var (ephemeralKey, counter, prevCounter, ciphertext) = DecodeWhisperMessage(protoBytes);

        // Protect session from concurrent race conditions
        lock (session)
        {
            // STAGED CANDIDATE TRANSITION:
            // Operate strictly on a cloned candidate state.
            // If HMAC check fails, candidate is disposed and live session remains 100% untouched.
            using var candidate = session.Clone();

            var ratchetService = new SignalDoubleRatchetService();
            var (encKey, macKey, iv) = ratchetService.DeriveMessageKeys(
                candidate,
                ephemeralKey,
                counter,
                prevCounter);

            try
            {
                // Verify HMAC-SHA256 truncated 8-byte tag FIRST (Encrypt-then-MAC discipline)
                // MAC Input: [RemoteIK: 33B][OurIK: 33B][0x33: 1B][ProtobufBytes: NB]
                var macInputLen = candidate.RemoteIdentityPublicKey.Length + candidate.OurIdentityPublicKey.Length + 1 + protoBytes.Length;
                var macInput = new byte[macInputLen];
                candidate.RemoteIdentityPublicKey.CopyTo(macInput.AsSpan(0));
                candidate.OurIdentityPublicKey.CopyTo(macInput.AsSpan(candidate.RemoteIdentityPublicKey.Length));
                macInput[candidate.RemoteIdentityPublicKey.Length + candidate.OurIdentityPublicKey.Length] = WireVersion;
                protoBytes.CopyTo(macInput.AsSpan(candidate.RemoteIdentityPublicKey.Length + candidate.OurIdentityPublicKey.Length + 1));

                var fullHmac = HMACSHA256.HashData(macKey, macInput);
                var computedTag = fullHmac.AsSpan(0, 8);

                if (!CryptographicOperations.FixedTimeEquals(computedTag, wireHmac))
                {
                    throw new CryptographicException("HMAC authentication failed for WhisperMessage. Message has been tampered with or corrupted.");
                }

                // Wire packet is authentic! Atomically commit candidate mutations to live session.
                candidate.CommitTo(session);

                // Decrypt ciphertext with authenticated keys
                var plaintext = ratchetService.DecryptCiphertext(encKey, iv, ciphertext);
                return Encoding.UTF8.GetString(plaintext);
            }
            finally
            {
                CryptographicOperations.ZeroMemory(encKey);
                CryptographicOperations.ZeroMemory(macKey);
                CryptographicOperations.ZeroMemory(iv);
            }
        }
    }

    private static byte[] EncodeWhisperMessage(
        byte[] ephemeralKey,
        uint counter,
        uint previousCounter,
        byte[] ciphertext)
    {
        using var ms = new MemoryStream();

        // field 1: ephemeralKey (bytes)
        WriteVarint(ms, (1 << 3) | 2);
        WriteVarint(ms, (ulong)ephemeralKey.Length);
        ms.Write(ephemeralKey);

        // field 2: counter (uint32)
        WriteVarint(ms, (2 << 3) | 0);
        WriteVarint(ms, counter);

        // field 3: previousCounter (uint32)
        WriteVarint(ms, (3 << 3) | 0);
        WriteVarint(ms, previousCounter);

        // field 4: ciphertext (bytes)
        WriteVarint(ms, (4 << 3) | 2);
        WriteVarint(ms, (ulong)ciphertext.Length);
        ms.Write(ciphertext);

        return ms.ToArray();
    }

    private static (byte[] EphemeralKey, uint Counter, uint PreviousCounter, byte[] Ciphertext) DecodeWhisperMessage(ReadOnlySpan<byte> buffer)
    {
        byte[]? ephemeralKey = null;
        uint counter = 0;
        uint previousCounter = 0;
        byte[]? ciphertext = null;

        var offset = 0;
        while (offset < buffer.Length)
        {
            var tag = ReadVarint(buffer, ref offset);
            var fieldNum = tag >> 3;
            var wireType = tag & 0x07;

            switch (fieldNum)
            {
                case 1:
                    var len1 = (int)ReadVarint(buffer, ref offset);
                    ephemeralKey = buffer.Slice(offset, len1).ToArray();
                    offset += len1;
                    break;
                case 2:
                    counter = (uint)ReadVarint(buffer, ref offset);
                    break;
                case 3:
                    previousCounter = (uint)ReadVarint(buffer, ref offset);
                    break;
                case 4:
                    var len4 = (int)ReadVarint(buffer, ref offset);
                    ciphertext = buffer.Slice(offset, len4).ToArray();
                    offset += len4;
                    break;
                default:
                    if (wireType == 0)
                    {
                        ReadVarint(buffer, ref offset);
                    }
                    else if (wireType == 2)
                    {
                        var skipLen = (int)ReadVarint(buffer, ref offset);
                        offset += skipLen;
                    }
                    else
                    {
                        throw new FormatException($"Unsupported wire type {wireType} in WhisperMessage.");
                    }
                    break;
            }
        }

        return (ephemeralKey ?? [], counter, previousCounter, ciphertext ?? []);
    }

    private static byte[] EncodePreKeyWhisperMessage(
        uint? preKeyId,
        byte[] baseKey,
        byte[] identityKey,
        byte[] message,
        uint registrationId,
        uint signedPreKeyId)
    {
        using var ms = new MemoryStream();

        if (preKeyId.HasValue)
        {
            // field 1: preKeyId (uint32)
            WriteVarint(ms, (1 << 3) | 0);
            WriteVarint(ms, preKeyId.Value);
        }

        // field 2: baseKey (bytes)
        WriteVarint(ms, (2 << 3) | 2);
        WriteVarint(ms, (ulong)baseKey.Length);
        ms.Write(baseKey);

        // field 3: identityKey (bytes)
        WriteVarint(ms, (3 << 3) | 2);
        WriteVarint(ms, (ulong)identityKey.Length);
        ms.Write(identityKey);

        // field 4: message (bytes)
        WriteVarint(ms, (4 << 3) | 2);
        WriteVarint(ms, (ulong)message.Length);
        ms.Write(message);

        // field 5: registrationId (uint32)
        WriteVarint(ms, (5 << 3) | 0);
        WriteVarint(ms, registrationId);

        // field 6: signedPreKeyId (uint32)
        WriteVarint(ms, (6 << 3) | 0);
        WriteVarint(ms, signedPreKeyId);

        return ms.ToArray();
    }

    private static (uint? PreKeyId, byte[] BaseKey, byte[] IdentityKey, byte[] Message, uint RegistrationId, uint SignedPreKeyId)
        DecodePreKeyWhisperMessage(ReadOnlySpan<byte> buffer)
    {
        uint? preKeyId = null;
        byte[]? baseKey = null;
        byte[]? identityKey = null;
        byte[]? message = null;
        uint registrationId = 0;
        uint signedPreKeyId = 0;

        var offset = 0;
        while (offset < buffer.Length)
        {
            var tag = ReadVarint(buffer, ref offset);
            var fieldNum = tag >> 3;
            var wireType = tag & 0x07;

            switch (fieldNum)
            {
                case 1:
                    preKeyId = (uint)ReadVarint(buffer, ref offset);
                    break;
                case 2:
                    var len2 = (int)ReadVarint(buffer, ref offset);
                    baseKey = buffer.Slice(offset, len2).ToArray();
                    offset += len2;
                    break;
                case 3:
                    var len3 = (int)ReadVarint(buffer, ref offset);
                    identityKey = buffer.Slice(offset, len3).ToArray();
                    offset += len3;
                    break;
                case 4:
                    var len4 = (int)ReadVarint(buffer, ref offset);
                    message = buffer.Slice(offset, len4).ToArray();
                    offset += len4;
                    break;
                case 5:
                    registrationId = (uint)ReadVarint(buffer, ref offset);
                    break;
                case 6:
                    signedPreKeyId = (uint)ReadVarint(buffer, ref offset);
                    break;
                default:
                    if (wireType == 0)
                    {
                        ReadVarint(buffer, ref offset);
                    }
                    else if (wireType == 2)
                    {
                        var skipLen = (int)ReadVarint(buffer, ref offset);
                        offset += skipLen;
                    }
                    else
                    {
                        throw new FormatException($"Unsupported wire type {wireType} in PreKeyWhisperMessage.");
                    }
                    break;
            }
        }

        return (preKeyId, baseKey ?? [], identityKey ?? [], message ?? [], registrationId, signedPreKeyId);
    }

    private static void WriteVarint(Stream stream, ulong value)
    {
        while (value >= 0x80)
        {
            stream.WriteByte((byte)((value & 0x7F) | 0x80));
            value >>= 7;
        }

        stream.WriteByte((byte)(value & 0x7F));
    }

    private static ulong ReadVarint(ReadOnlySpan<byte> buffer, ref int offset)
    {
        ulong result = 0;
        var shift = 0;

        while (offset < buffer.Length)
        {
            var b = buffer[offset++];
            result |= (ulong)(b & 0x7F) << shift;

            if ((b & 0x80) == 0)
            {
                return result;
            }

            shift += 7;
        }

        throw new FormatException("Malformed protobuf varint reached end of buffer.");
    }
}
