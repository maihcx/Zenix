// Copyright (c) Zenix. All rights reserved.
// Licensed under the proprietary and confidential license.
// Unauthorized copying, distribution, modification, or disclosure of this file,
// via any medium, is strictly prohibited without prior written permission.

using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Zenix.Core.Zalo.Models.Dto;

namespace Zenix.Core.Zalo.Storage;

/// <summary>
/// Manages secure credential persistence for Zalo sessions, IMEI device identifiers, and E2EE private keys
/// using Windows Data Protection API (DPAPI). Enforces .zalo.me domain scoping and provides secret redaction for logs.
/// </summary>
public sealed class ZaloCredentialStorage
{
    private static readonly Regex ZpwSekRegex = new(@"zpw_sek=([^;\s&]+)", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex ZpwSecKeyJsonRegex = new(@"""zpw_sec_key""\s*:\s*""([^""]+)""", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex ZpwEncKeyJsonRegex = new(@"""zpw_enc_key""\s*:\s*""([^""]+)""", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex BearerTokenRegex = new(@"Bearer\s+([A-Za-z0-9_\-\.]+)", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex PasswordRegex = new(@"(password|token|secret)\s*[:=]\s*([^;\s&,}\]]+)", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private readonly string _baseDirectory;

    /// <summary>
    /// Initializes a new instance of the <see cref="ZaloCredentialStorage"/> class.
    /// </summary>
    /// <param name="customBaseDirectory">Optional custom base directory override for testing.</param>
    public ZaloCredentialStorage(string? customBaseDirectory = null)
    {
        _baseDirectory = customBaseDirectory ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Zenix",
            "credentials");
    }

    /// <summary>
    /// Encrypts raw plaintext bytes using Windows DPAPI scoped to the current Windows user account.
    /// </summary>
    /// <param name="plaintext">The plaintext bytes to protect.</param>
    /// <param name="optionalEntropy">Optional additional entropy bytes.</param>
    /// <returns>The encrypted ciphertext bytes.</returns>
    public static byte[] Protect(byte[] plaintext, byte[]? optionalEntropy = null)
    {
        ArgumentNullException.ThrowIfNull(plaintext);
        return ProtectedData.Protect(plaintext, optionalEntropy, DataProtectionScope.CurrentUser);
    }

    /// <summary>
    /// Decrypts ciphertext bytes using Windows DPAPI scoped to the current Windows user account.
    /// </summary>
    /// <param name="ciphertext">The ciphertext bytes to unprotect.</param>
    /// <param name="optionalEntropy">Optional additional entropy bytes matching encryption.</param>
    /// <returns>The decrypted plaintext bytes.</returns>
    public static byte[] Unprotect(byte[] ciphertext, byte[]? optionalEntropy = null)
    {
        ArgumentNullException.ThrowIfNull(ciphertext);
        return ProtectedData.Unprotect(ciphertext, optionalEntropy, DataProtectionScope.CurrentUser);
    }

    /// <summary>
    /// Encrypts a plaintext string to a protected Base64 string via Windows DPAPI.
    /// </summary>
    /// <param name="plaintext">The plaintext string.</param>
    /// <param name="optionalEntropy">Optional additional entropy bytes.</param>
    /// <returns>The Base64 encoded ciphertext string.</returns>
    public static string ProtectString(string plaintext, byte[]? optionalEntropy = null)
    {
        ArgumentNullException.ThrowIfNull(plaintext);
        var bytes = Encoding.UTF8.GetBytes(plaintext);
        var protectedBytes = Protect(bytes, optionalEntropy);
        return Convert.ToBase64String(protectedBytes);
    }

    /// <summary>
    /// Decrypts a protected Base64 string to a plaintext string via Windows DPAPI.
    /// </summary>
    /// <param name="base64Ciphertext">The Base64 encoded ciphertext string.</param>
    /// <param name="optionalEntropy">Optional additional entropy bytes matching encryption.</param>
    /// <returns>The decrypted plaintext string.</returns>
    public static string UnprotectString(string base64Ciphertext, byte[]? optionalEntropy = null)
    {
        ArgumentNullException.ThrowIfNull(base64Ciphertext);
        var cipherBytes = Convert.FromBase64String(base64Ciphertext);
        var plainBytes = Unprotect(cipherBytes, optionalEntropy);
        return Encoding.UTF8.GetString(plainBytes);
    }

    /// <summary>
    /// Validates that a session cookie domain belongs strictly to <c>.zalo.me</c>.
    /// </summary>
    /// <param name="domain">The cookie domain string to validate.</param>
    /// <exception cref="ArgumentException">Thrown if the domain is null, empty, or does not end with <c>.zalo.me</c>.</exception>
    public static void ValidateCookieDomain(string domain)
    {
        if (string.IsNullOrWhiteSpace(domain))
        {
            throw new ArgumentException("Cookie domain cannot be empty.", nameof(domain));
        }

        var trimmed = domain.Trim().TrimStart('.').ToLowerInvariant();
        if (!trimmed.Equals("zalo.me", StringComparison.OrdinalIgnoreCase) &&
            !trimmed.EndsWith(".zalo.me", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException(
                $"Unauthorized cookie domain '{domain}'. Cookies must be strictly scoped to .zalo.me domain.",
                nameof(domain));
        }
    }

    /// <summary>
    /// Redacts sensitive authentication tokens, session secrets, and passwords from log or error messages.
    /// </summary>
    /// <param name="input">The raw string to scrub.</param>
    /// <returns>A sanitized string with credentials replaced by <c>[REDACTED]</c>.</returns>
    public static string ScrubSecrets(string? input)
    {
        if (string.IsNullOrEmpty(input))
        {
            return string.Empty;
        }

        var output = ZpwSekRegex.Replace(input, "zpw_sek=[REDACTED]");
        output = ZpwSecKeyJsonRegex.Replace(output, @"""zpw_sec_key"":""[REDACTED]""");
        output = ZpwEncKeyJsonRegex.Replace(output, @"""zpw_enc_key"":""[REDACTED]""");
        output = BearerTokenRegex.Replace(output, "Bearer [REDACTED]");
        output = PasswordRegex.Replace(output, "$1=[REDACTED]");
        return output;
    }

    /// <summary>
    /// Persists an authenticated session cookie encrypted via DPAPI, enforcing <c>.zalo.me</c> domain scoping.
    /// </summary>
    /// <param name="accountId">The account identifier.</param>
    /// <param name="cookie">The session cookie record.</param>
    /// <param name="domain">Optional domain to validate (defaults to <c>".zalo.me"</c>).</param>
    public void SaveSessionCookie(string accountId, LoginSessionCookie cookie, string domain = ".zalo.me")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(accountId);
        ArgumentNullException.ThrowIfNull(cookie);
        ValidateCookieDomain(domain);

        var accountDir = GetAccountDirectory(accountId);
        Directory.CreateDirectory(accountDir);

        var json = JsonSerializer.Serialize(cookie);
        var bytes = Encoding.UTF8.GetBytes(json);
        var encrypted = Protect(bytes);

        var filePath = Path.Combine(accountDir, "session_cookie.dat");
        File.WriteAllBytes(filePath, encrypted);
    }

    /// <summary>
    /// Retrieves and decrypts the session cookie for the specified account.
    /// </summary>
    /// <param name="accountId">The account identifier.</param>
    /// <returns>The decrypted session cookie, or <c>null</c> if none stored or invalid.</returns>
    public LoginSessionCookie? GetSessionCookie(string accountId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(accountId);

        var filePath = Path.Combine(GetAccountDirectory(accountId), "session_cookie.dat");
        if (!File.Exists(filePath))
        {
            return null;
        }

        try
        {
            var encrypted = File.ReadAllBytes(filePath);
            var decrypted = Unprotect(encrypted);
            var json = Encoding.UTF8.GetString(decrypted);
            return JsonSerializer.Deserialize<LoginSessionCookie>(json);
        }
        catch (CryptographicException)
        {
            // Decryption failure (e.g. another user or corrupted)
            return null;
        }
    }

    /// <summary>
    /// Persists a named secret string encrypted via DPAPI.
    /// </summary>
    /// <param name="accountId">The account identifier.</param>
    /// <param name="key">The secret key name (e.g., "z_uuid", "imei").</param>
    /// <param name="value">The plaintext secret value.</param>
    public void SaveSecret(string accountId, string key, string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(accountId);
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(value);

        var accountDir = GetAccountDirectory(accountId);
        Directory.CreateDirectory(accountDir);

        var encrypted = ProtectString(value);
        var sanitizedKey = SanitizeFileName(key);
        var filePath = Path.Combine(accountDir, $"{sanitizedKey}.dat");
        File.WriteAllText(filePath, encrypted);
    }

    /// <summary>
    /// Retrieves and decrypts a named secret string.
    /// </summary>
    /// <param name="accountId">The account identifier.</param>
    /// <param name="key">The secret key name.</param>
    /// <returns>The decrypted secret value, or <c>null</c> if not found.</returns>
    public string? GetSecret(string accountId, string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(accountId);
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        var sanitizedKey = SanitizeFileName(key);
        var filePath = Path.Combine(GetAccountDirectory(accountId), $"{sanitizedKey}.dat");
        if (!File.Exists(filePath))
        {
            return null;
        }

        try
        {
            var encryptedBase64 = File.ReadAllText(filePath);
            return UnprotectString(encryptedBase64);
        }
        catch (CryptographicException)
        {
            return null;
        }
    }

    /// <summary>
    /// Persists an E2EE identity private key securely under DPAPI.
    /// </summary>
    /// <param name="accountId">The account identifier.</param>
    /// <param name="privateKeyBytes">The private key bytes.</param>
    public void SaveE2eePrivateKey(string accountId, byte[] privateKeyBytes)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(accountId);
        ArgumentNullException.ThrowIfNull(privateKeyBytes);

        var accountDir = GetAccountDirectory(accountId);
        Directory.CreateDirectory(accountDir);

        var encrypted = Protect(privateKeyBytes);
        var filePath = Path.Combine(accountDir, "e2ee_identity.dat");
        File.WriteAllBytes(filePath, encrypted);
    }

    /// <summary>
    /// Retrieves and decrypts the stored E2EE identity private key.
    /// </summary>
    /// <param name="accountId">The account identifier.</param>
    /// <returns>The decrypted private key bytes, or <c>null</c> if not found.</returns>
    public byte[]? GetE2eePrivateKey(string accountId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(accountId);

        var filePath = Path.Combine(GetAccountDirectory(accountId), "e2ee_identity.dat");
        if (!File.Exists(filePath))
        {
            return null;
        }

        try
        {
            var encrypted = File.ReadAllBytes(filePath);
            return Unprotect(encrypted);
        }
        catch (CryptographicException)
        {
            return null;
        }
    }

    /// <summary>
    /// Deletes all stored credentials and encryption keys for the specified account.
    /// </summary>
    /// <param name="accountId">The account identifier.</param>
    public void ClearCredentials(string accountId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(accountId);

        var accountDir = GetAccountDirectory(accountId);
        if (Directory.Exists(accountDir))
        {
            Directory.Delete(accountDir, recursive: true);
        }
    }

    private string GetAccountDirectory(string accountId)
    {
        return Path.Combine(_baseDirectory, SanitizeFileName(accountId));
    }

    private static string SanitizeFileName(string input)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var chars = input.ToCharArray();
        for (var i = 0; i < chars.Length; i++)
        {
            if (Array.IndexOf(invalid, chars[i]) >= 0 || chars[i] == '/' || chars[i] == '\\')
            {
                chars[i] = '_';
            }
        }

        return new string(chars);
    }
}
