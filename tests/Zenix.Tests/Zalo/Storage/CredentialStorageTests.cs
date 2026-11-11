// Copyright (c) Zenix. All rights reserved.
// Licensed under the proprietary and confidential license.
// Unauthorized copying, distribution, modification, or disclosure of this file,
// via any medium, is strictly prohibited without prior written permission.

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using FluentAssertions;
using Xunit;
using Zenix.Core.Zalo.Models.Dto;
using Zenix.Core.Zalo.Storage;

namespace Zenix.Tests.Zalo.Storage;

/// <summary>
/// Unit tests for <see cref="ZaloCredentialStorage"/> verifying Windows DPAPI encryption roundtrip,
/// domain scoping validation, credential log scrubbing, and secure file storage.
/// </summary>
public sealed class CredentialStorageTests : IDisposable
{
    private readonly string _tempDirectory;
    private readonly ZaloCredentialStorage _storage;

    public CredentialStorageTests()
    {
        _tempDirectory = Path.Combine(Path.GetTempPath(), $"zenix_cred_test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDirectory);
        _storage = new ZaloCredentialStorage(_tempDirectory);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_tempDirectory))
            {
                Directory.Delete(_tempDirectory, recursive: true);
            }
        }
        catch (IOException)
        {
            // Best effort cleanup
        }
    }

    [Fact]
    [Trait("Category", "Storage")]
    public void ProtectAndUnprotect_Bytes_RoundTripsSuccessfully()
    {
        var originalBytes = Encoding.UTF8.GetBytes("SuperSecretZaloToken12345");
        var encrypted = ZaloCredentialStorage.Protect(originalBytes);

        encrypted.Should().NotEqual(originalBytes);

        var decrypted = ZaloCredentialStorage.Unprotect(encrypted);
        decrypted.Should().Equal(originalBytes);
    }

    [Fact]
    [Trait("Category", "Storage")]
    public void ProtectStringAndUnprotectString_RoundTripsProperly()
    {
        const string secret = "zpw_sek=AbCdEfGhIjKlMnOpQrStUvWxYz012345";
        var encryptedBase64 = ZaloCredentialStorage.ProtectString(secret);

        encryptedBase64.Should().NotBeNullOrWhiteSpace();
        encryptedBase64.Should().NotContain("AbCdEfGh");

        var decrypted = ZaloCredentialStorage.UnprotectString(encryptedBase64);
        decrypted.Should().Be(secret);
    }

    [Theory]
    [InlineData(".zalo.me")]
    [InlineData("zalo.me")]
    [InlineData("chat.zalo.me")]
    [InlineData("api.zalo.me")]
    [InlineData(".api.zalo.me")]
    [Trait("Category", "Storage")]
    public void ValidateCookieDomain_ValidDomains_Succeeds(string validDomain)
    {
        var act = () => ZaloCredentialStorage.ValidateCookieDomain(validDomain);
        act.Should().NotThrow();
    }

    [Theory]
    [InlineData("evil.com")]
    [InlineData("phishing-zalo.me")]
    [InlineData("notzalo.me")]
    [InlineData("zalo.me.evil.com")]
    [InlineData("")]
    [InlineData("   ")]
    [Trait("Category", "Storage")]
    public void ValidateCookieDomain_InvalidDomains_ThrowsArgumentException(string invalidDomain)
    {
        var act = () => ZaloCredentialStorage.ValidateCookieDomain(invalidDomain);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    [Trait("Category", "Storage")]
    public void SaveAndGetSessionCookie_PersistsEncryptedAndEnforcesDomainScoping()
    {
        var cookie = new LoginSessionCookie
        {
            ZpwSecKey = "sec_key_xyz",
            ZpwEncKey = "enc_key_abc",
            SessionId = "sess_001",
            UserId = "user_123",
            ExpiresAt = 1800000000000L,
            RawCookies = new Dictionary<string, string> { ["zpw_sek"] = "sec_key_xyz" }
        };

        // Saving with invalid domain fails
        var failAct = () => _storage.SaveSessionCookie("acc_1", cookie, "malicious.site");
        failAct.Should().Throw<ArgumentException>();

        // Saving with valid domain succeeds
        _storage.SaveSessionCookie("acc_1", cookie, ".zalo.me");

        var retrieved = _storage.GetSessionCookie("acc_1");
        retrieved.Should().NotBeNull();
        retrieved!.ZpwSecKey.Should().Be("sec_key_xyz");
        retrieved.ZpwEncKey.Should().Be("enc_key_abc");
        retrieved.UserId.Should().Be("user_123");
    }

    [Fact]
    [Trait("Category", "Storage")]
    public void ScrubSecrets_ReplacesSensitiveValuesWithRedacted()
    {
        const string rawLog = "Error calling gateway with zpw_sek=SecretSessionKey99 and Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9. Token payload: {\"zpw_sec_key\":\"SecVal88\",\"zpw_enc_key\":\"EncVal77\"}, password=MyPassword123!";
        var scrubbed = ZaloCredentialStorage.ScrubSecrets(rawLog);

        scrubbed.Should().NotContain("SecretSessionKey99");
        scrubbed.Should().NotContain("eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9");
        scrubbed.Should().NotContain("SecVal88");
        scrubbed.Should().NotContain("EncVal77");
        scrubbed.Should().NotContain("MyPassword123!");

        scrubbed.Should().Contain("zpw_sek=[REDACTED]");
        scrubbed.Should().Contain("Bearer [REDACTED]");
        scrubbed.Should().Contain(@"""zpw_sec_key"":""[REDACTED]""");
        scrubbed.Should().Contain(@"""zpw_enc_key"":""[REDACTED]""");
        scrubbed.Should().Contain("password=[REDACTED]");
    }

    [Fact]
    [Trait("Category", "Storage")]
    public void ClearCredentials_DeletesAccountCredentialFiles()
    {
        _storage.SaveSecret("account_wipe_target", "imei", "device-imei-12345");
        _storage.GetSecret("account_wipe_target", "imei").Should().Be("device-imei-12345");

        _storage.ClearCredentials("account_wipe_target");

        _storage.GetSecret("account_wipe_target", "imei").Should().BeNull();
    }
}
