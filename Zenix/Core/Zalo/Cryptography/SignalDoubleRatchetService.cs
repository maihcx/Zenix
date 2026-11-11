// Copyright (c) Zenix. All rights reserved.
// Licensed under the proprietary and confidential license.
// Unauthorized copying, distribution, modification, or disclosure of this file,
// via any medium, is strictly prohibited without prior written permission.

using System;
using System.Security.Cryptography;
using System.Text;

namespace Zenix.Core.Zalo.Cryptography;

/// <summary>
/// Implements the Signal Double Ratchet algorithm with symmetric chain advancing,
/// asymmetric Diffie-Hellman stepping, message key expansion, and out-of-order packet caching.
/// </summary>
public sealed class SignalDoubleRatchetService
{
    private static readonly byte[] StepOne = [0x01];
    private static readonly byte[] StepTwo = [0x02];
    private static readonly byte[] ZeroSalt32 = new byte[32];
    private static readonly byte[] ZaloRatchetInfo = Encoding.UTF8.GetBytes("ZaloRatchet");
    private static readonly byte[] ZaloMessageKeysInfo = Encoding.UTF8.GetBytes("ZaloMessageKeys");

    /// <summary>
    /// The maximum number of skipped message keys that can be cached into the future (2,000)
    /// to mitigate denial-of-service state exhaustion attacks.
    /// </summary>
    public const int MaxMessageKeys = 2000;

    /// <summary>
    /// Initializes an initiator (Alice) Double Ratchet session state from the X3DH master shared secret.
    /// </summary>
    /// <param name="masterSecret">The 64-byte master secret derived from X3DH.</param>
    /// <param name="bobSignedPreKeyPub">Bob's signed prekey public key (33 bytes or 32 bytes).</param>
    /// <param name="ourIdentityKey">Alice's 33-byte identity public key.</param>
    /// <param name="bobIdentityKey">Bob's 33-byte identity public key.</param>
    /// <param name="bobRegistrationId">Bob's registration ID.</param>
    /// <returns>A newly initialized <see cref="DoubleRatchetSessionState"/>.</returns>
    public DoubleRatchetSessionState InitializeInitiatorSession(
        ReadOnlySpan<byte> masterSecret,
        ReadOnlySpan<byte> bobSignedPreKeyPub,
        ReadOnlySpan<byte> ourIdentityKey,
        ReadOnlySpan<byte> bobIdentityKey,
        int bobRegistrationId)
    {
        var session = new DoubleRatchetSessionState
        {
            RootKey = masterSecret[..32].ToArray(),
            OurIdentityPublicKey = ourIdentityKey.ToArray(),
            RemoteIdentityPublicKey = bobIdentityKey.ToArray(),
            RemoteRegistrationId = bobRegistrationId,
            RemoteEphemeralPublicKey = bobSignedPreKeyPub.ToArray(),
            LocalEphemeralKeyPair = Curve25519Helper.GenerateKeyPair(includePrefix: true)
        };

        // Step initial sending ratchet: DH(localEphemeralPriv, bobSignedPreKeyPub)
        var dhResult = Curve25519Helper.CalculateAgreement(session.LocalEphemeralKeyPair.PrivateKey, bobSignedPreKeyPub);
        Span<byte> derived = stackalloc byte[64];
        try
        {
            HKDF.DeriveKey(HashAlgorithmName.SHA256, dhResult, derived, session.RootKey, ZaloRatchetInfo);
            session.RootKey = derived[..32].ToArray();
            session.SendingChainKey = derived[32..64].ToArray();
            session.SendMessageCounter = 0;
            return session;
        }
        finally
        {
            CryptographicOperations.ZeroMemory(dhResult);
            CryptographicOperations.ZeroMemory(derived);
        }
    }

    /// <summary>
    /// Initializes a recipient (Bob) Double Ratchet session state from the X3DH master shared secret.
    /// </summary>
    /// <param name="masterSecret">The 64-byte master secret derived from X3DH.</param>
    /// <param name="ourSignedPreKeyPair">Bob's signed prekey key pair.</param>
    /// <param name="ourIdentityKey">Bob's 33-byte identity public key.</param>
    /// <param name="aliceIdentityKey">Alice's 33-byte identity public key.</param>
    /// <param name="aliceRegistrationId">Alice's registration ID.</param>
    /// <returns>A newly initialized <see cref="DoubleRatchetSessionState"/>.</returns>
    public DoubleRatchetSessionState InitializeRecipientSession(
        ReadOnlySpan<byte> masterSecret,
        (byte[] PrivateKey, byte[] PublicKey) ourSignedPreKeyPair,
        ReadOnlySpan<byte> ourIdentityKey,
        ReadOnlySpan<byte> aliceIdentityKey,
        int aliceRegistrationId)
    {
        return new DoubleRatchetSessionState
        {
            RootKey = masterSecret[..32].ToArray(),
            OurIdentityPublicKey = ourIdentityKey.ToArray(),
            RemoteIdentityPublicKey = aliceIdentityKey.ToArray(),
            RemoteRegistrationId = aliceRegistrationId,
            LocalEphemeralKeyPair = ((byte[])ourSignedPreKeyPair.PrivateKey.Clone(), (byte[])ourSignedPreKeyPair.PublicKey.Clone())
        };
    }

    /// <summary>
    /// Encrypts plaintext bytes using the current sending chain of the Double Ratchet session.
    /// </summary>
    /// <param name="session">The active Double Ratchet session state.</param>
    /// <param name="plaintext">The raw plaintext bytes to encrypt.</param>
    /// <returns>A tuple containing the ciphertext, message counter, previous chain counter, local ephemeral key, and macKey.</returns>
    /// <exception cref="InvalidOperationException">Thrown when sending chain is not initialized.</exception>
    public (byte[] Ciphertext, uint Counter, uint PreviousCounter, byte[] EphemeralPublicKey, byte[] MacKey) Encrypt(
        DoubleRatchetSessionState session,
        ReadOnlySpan<byte> plaintext)
    {
        ArgumentNullException.ThrowIfNull(session);

        if (session.SendingChainKey == null)
        {
            throw new InvalidOperationException("Cannot encrypt on an uninitialized sending chain.");
        }

        // Advance sending chain:
        // messageKey = HMAC-SHA256(chainKey, [0x01])
        // nextChainKey = HMAC-SHA256(chainKey, [0x02])
        var messageKey = HMACSHA256.HashData(session.SendingChainKey, StepOne);
        var nextChainKey = HMACSHA256.HashData(session.SendingChainKey, StepTwo);
        CryptographicOperations.ZeroMemory(session.SendingChainKey);
        session.SendingChainKey = nextChainKey;

        var counter = session.SendMessageCounter++;
        var previousCounter = session.PreviousCounter;
        var ephemeralPub = (byte[])session.LocalEphemeralKeyPair.PublicKey.Clone();

        Span<byte> expandedKeys = stackalloc byte[80];
        try
        {
            // Expand messageKey into 32B encKey + 32B macKey + 16B IV
            HKDF.DeriveKey(HashAlgorithmName.SHA256, messageKey, expandedKeys, ZeroSalt32, ZaloMessageKeysInfo);
            var encKey = expandedKeys[..32];
            var macKey = expandedKeys[32..64].ToArray();
            var iv = expandedKeys[64..80];

            using var aes = Aes.Create();
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7;
            aes.Key = encKey.ToArray();
            aes.IV = iv.ToArray();

            using var encryptor = aes.CreateEncryptor();
            var ciphertext = encryptor.TransformFinalBlock(plaintext.ToArray(), 0, plaintext.Length);

            return (ciphertext, counter, previousCounter, ephemeralPub, macKey);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(messageKey);
            CryptographicOperations.ZeroMemory(expandedKeys);
        }
    }

    /// <summary>
    /// Advances the Double Ratchet state to derive the symmetric message keys (encryption key, MAC key, and IV)
    /// for the given sequence counter without executing AES decryption.
    /// </summary>
    /// <param name="session">The active Double Ratchet session state.</param>
    /// <param name="remoteEphemeralKey">The sender's ephemeral public key accompanying the message.</param>
    /// <param name="counter">The sender's message sequence counter.</param>
    /// <param name="previousCounter">The sender's previous chain length counter.</param>
    /// <returns>A tuple containing the 32-byte encryption key, 32-byte MAC key, and 16-byte IV.</returns>
    public (byte[] EncKey, byte[] MacKey, byte[] Iv) DeriveMessageKeys(
        DoubleRatchetSessionState session,
        ReadOnlySpan<byte> remoteEphemeralKey,
        uint counter,
        uint previousCounter)
    {
        ArgumentNullException.ThrowIfNull(session);

        var ephemeralHex = Convert.ToHexStringLower(remoteEphemeralKey);

        // Check if message key was previously skipped and cached
        if (session.SkippedMessageKeys.Remove((ephemeralHex, counter), out var cached))
        {
            return ExpandMessageKeys(cached.MessageKey);
        }

        // If a new remote ephemeral key arrives, advance the asymmetric DH ratchet
        var isNewEphemeral = session.RemoteEphemeralPublicKey == null ||
                             !CryptographicOperations.FixedTimeEquals(session.RemoteEphemeralPublicKey, remoteEphemeralKey);

        if (isNewEphemeral)
        {
            StepDhRatchet(session, remoteEphemeralKey, previousCounter);
        }

        // Fill skipped message keys on current receiving chain up to counter
        if (session.ReceivingChainKey == null)
        {
            throw new CryptographicException("Receiving chain key is not initialized.");
        }

        if (counter < session.ReceiveMessageCounter)
        {
            throw new CryptographicException($"Message counter {counter} is older than current receiving counter {session.ReceiveMessageCounter} and not in skipped key store.");
        }

        if (counter - session.ReceiveMessageCounter > MaxMessageKeys)
        {
            throw new InvalidOperationException($"Over messages into the future: {MaxMessageKeys}");
        }

        while (session.ReceiveMessageCounter < counter)
        {
            var skippedMsgKey = HMACSHA256.HashData(session.ReceivingChainKey, StepOne);
            var nextChain = HMACSHA256.HashData(session.ReceivingChainKey, StepTwo);
            CryptographicOperations.ZeroMemory(session.ReceivingChainKey);
            session.ReceivingChainKey = nextChain;

            session.SkippedMessageKeys[(ephemeralHex, session.ReceiveMessageCounter)] = (skippedMsgKey, DateTime.UtcNow);
            session.ReceiveMessageCounter++;
        }

        // Derive target message key
        var messageKey = HMACSHA256.HashData(session.ReceivingChainKey, StepOne);
        var finalNextChain = HMACSHA256.HashData(session.ReceivingChainKey, StepTwo);
        CryptographicOperations.ZeroMemory(session.ReceivingChainKey);
        session.ReceivingChainKey = finalNextChain;
        session.ReceiveMessageCounter++;

        return ExpandMessageKeys(messageKey);
    }

    /// <summary>
    /// Decrypts ciphertext bytes using AES-256-CBC with PKCS7 padding using the derived encryption key and IV.
    /// </summary>
    /// <param name="encKey">The 32-byte AES encryption key.</param>
    /// <param name="iv">The 16-byte initialization vector.</param>
    /// <param name="ciphertext">The raw ciphertext bytes to decrypt.</param>
    /// <returns>The decrypted plaintext bytes.</returns>
    public byte[] DecryptCiphertext(ReadOnlySpan<byte> encKey, ReadOnlySpan<byte> iv, ReadOnlySpan<byte> ciphertext)
    {
        using var aes = Aes.Create();
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;
        aes.Key = encKey.ToArray();
        aes.IV = iv.ToArray();

        using var decryptor = aes.CreateDecryptor();
        return decryptor.TransformFinalBlock(ciphertext.ToArray(), 0, ciphertext.Length);
    }

    /// <summary>
    /// Decrypts ciphertext bytes using the Double Ratchet session, handling out-of-order packets and stepping ratchets.
    /// </summary>
    /// <param name="session">The active Double Ratchet session state.</param>
    /// <param name="remoteEphemeralKey">The sender's ephemeral public key accompanying the message.</param>
    /// <param name="counter">The sender's message sequence counter.</param>
    /// <param name="previousCounter">The sender's previous chain length counter.</param>
    /// <param name="ciphertext">The raw ciphertext bytes.</param>
    /// <returns>A tuple containing the decrypted plaintext bytes and the 32-byte MAC key used for wire authentication.</returns>
    public (byte[] Plaintext, byte[] MacKey) Decrypt(
        DoubleRatchetSessionState session,
        ReadOnlySpan<byte> remoteEphemeralKey,
        uint counter,
        uint previousCounter,
        ReadOnlySpan<byte> ciphertext)
    {
        ArgumentNullException.ThrowIfNull(session);

        var (encKey, macKey, iv) = DeriveMessageKeys(session, remoteEphemeralKey, counter, previousCounter);
        try
        {
            var plaintext = DecryptCiphertext(encKey, iv, ciphertext);
            return (plaintext, macKey);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(encKey);
            CryptographicOperations.ZeroMemory(iv);
        }
    }

    private static (byte[] EncKey, byte[] MacKey, byte[] Iv) ExpandMessageKeys(byte[] messageKey)
    {
        Span<byte> expandedKeys = stackalloc byte[80];
        try
        {
            HKDF.DeriveKey(HashAlgorithmName.SHA256, messageKey, expandedKeys, ZeroSalt32, ZaloMessageKeysInfo);
            var encKey = expandedKeys[..32].ToArray();
            var macKey = expandedKeys[32..64].ToArray();
            var iv = expandedKeys[64..80].ToArray();
            return (encKey, macKey, iv);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(messageKey);
            CryptographicOperations.ZeroMemory(expandedKeys);
        }
    }

    private static void StepDhRatchet(
        DoubleRatchetSessionState session,
        ReadOnlySpan<byte> remoteEphemeralKey,
        uint remotePreviousCounter)
    {
        var oldEphemeralHex = session.RemoteEphemeralPublicKey != null
            ? Convert.ToHexStringLower(session.RemoteEphemeralPublicKey)
            : string.Empty;

        // Exhaust remaining skipped keys on previous receiving chain if applicable
        if (session.ReceivingChainKey != null && !string.IsNullOrEmpty(oldEphemeralHex))
        {
            // Guard against unsigned underflow and excessive future gap jumps (> 2000)
            if (remotePreviousCounter > session.ReceiveMessageCounter &&
                remotePreviousCounter - session.ReceiveMessageCounter > MaxMessageKeys)
            {
                throw new InvalidOperationException($"Over messages into the future: {MaxMessageKeys}");
            }

            while (session.ReceiveMessageCounter < remotePreviousCounter)
            {
                var skipKey = HMACSHA256.HashData(session.ReceivingChainKey, StepOne);
                var nextChain = HMACSHA256.HashData(session.ReceivingChainKey, StepTwo);
                CryptographicOperations.ZeroMemory(session.ReceivingChainKey);
                session.ReceivingChainKey = nextChain;

                session.SkippedMessageKeys[(oldEphemeralHex, session.ReceiveMessageCounter)] = (skipKey, DateTime.UtcNow);
                session.ReceiveMessageCounter++;
            }
        }

        // DH Step 1 (Receiving chain):
        // dhReceive = ECDHE(remoteEphemeral, localEphemeralPriv)
        var dhReceive = Curve25519Helper.CalculateAgreement(session.LocalEphemeralKeyPair.PrivateKey, remoteEphemeralKey);
        Span<byte> derivedRecv = stackalloc byte[64];
        try
        {
            HKDF.DeriveKey(HashAlgorithmName.SHA256, dhReceive, derivedRecv, session.RootKey, ZaloRatchetInfo);
            session.RootKey = derivedRecv[..32].ToArray();
            session.ReceivingChainKey = derivedRecv[32..64].ToArray();
            session.ReceiveMessageCounter = 0;
            session.RemoteEphemeralPublicKey = remoteEphemeralKey.ToArray();
            session.PreviousCounter = session.SendMessageCounter;
        }
        finally
        {
            CryptographicOperations.ZeroMemory(dhReceive);
            CryptographicOperations.ZeroMemory(derivedRecv);
        }

        // DH Step 2 (Sending chain):
        // Generate new local ephemeral key pair
        var newLocalKp = Curve25519Helper.GenerateKeyPair(includePrefix: true);
        var dhSend = Curve25519Helper.CalculateAgreement(newLocalKp.PrivateKey, remoteEphemeralKey);
        Span<byte> derivedSend = stackalloc byte[64];
        try
        {
            HKDF.DeriveKey(HashAlgorithmName.SHA256, dhSend, derivedSend, session.RootKey, ZaloRatchetInfo);
            session.RootKey = derivedSend[..32].ToArray();
            session.SendingChainKey = derivedSend[32..64].ToArray();
            session.SendMessageCounter = 0;
            session.LocalEphemeralKeyPair = newLocalKp;
        }
        finally
        {
            CryptographicOperations.ZeroMemory(dhSend);
            CryptographicOperations.ZeroMemory(derivedSend);
        }
    }
}
