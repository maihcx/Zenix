// Copyright (c) Zenix. All rights reserved.
// Licensed under the proprietary and confidential license.
// Unauthorized copying, distribution, modification, or disclosure of this file,
// via any medium, is strictly prohibited without prior written permission.

using System.Collections.Generic;
using System.Text.Json;
using FluentAssertions;
using Xunit;
using Zenix.Core.Zalo.Models.Dto;
using Zenix.Core.Zalo.Models.Enums;

namespace Zenix.Tests.Zalo.Models;

/// <summary>
/// Unit tests verifying JSON serialization and deserialization of Zalo network wire DTOs.
/// </summary>
public sealed class DtoSerializationTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = false
    };

    /// <summary>
    /// Verifies QrCodeTokenResponse wire JSON mapping and round-trip integrity.
    /// </summary>
    [Fact]
    [Trait("Category", "Models")]
    public void QrCodeTokenResponse_SerializationRoundTrip()
    {
        var dto = new QrCodeTokenResponse
        {
            Token = "qr_token_abc123",
            QrCodeUrl = "https://qr.zalo.me/code/abc123.png",
            ExpiresInSeconds = 120,
            CreatedTimestamp = 1716000000000
        };

        var json = JsonSerializer.Serialize(dto, JsonOptions);
        json.Should().Contain("\"token\":\"qr_token_abc123\"");
        json.Should().Contain("\"qr_code_url\":");
        json.Should().Contain("\"expires_in\":120");

        var deserialized = JsonSerializer.Deserialize<QrCodeTokenResponse>(json, JsonOptions);
        deserialized.Should().NotBeNull();
        deserialized!.Token.Should().Be("qr_token_abc123");
        deserialized.QrCodeUrl.Should().Be("https://qr.zalo.me/code/abc123.png");
        deserialized.ExpiresInSeconds.Should().Be(120);
        deserialized.CreatedTimestamp.Should().Be(1716000000000);
    }

    /// <summary>
    /// Verifies QrWaitScanResponse wire status codes, approvals, and error representations.
    /// </summary>
    [Theory]
    [Trait("Category", "Models")]
    [InlineData(QrScanState.Initial, null, null, null)]
    [InlineData(QrScanState.ScannedWaitingConfirm, "https://avatar.zalo.me/u.png", "User Name", null)]
    [InlineData(QrScanState.Approved, null, null, "session_code_789")]
    [InlineData(QrScanState.Expired, null, null, null)]
    [InlineData(QrScanState.ErrorNegative20NeedsQr2, null, null, null)]
    public void QrWaitScanResponse_StatesAndSerialization(
        QrScanState state,
        string? avatar,
        string? displayName,
        string? sessionCode)
    {
        var dto = new QrWaitScanResponse
        {
            State = state,
            UserAvatar = avatar,
            UserDisplayName = displayName,
            SessionCode = sessionCode,
            ErrorMessage = state == QrScanState.Expired ? "QR Expired" : null
        };

        var json = JsonSerializer.Serialize(dto, JsonOptions);
        var deserialized = JsonSerializer.Deserialize<QrWaitScanResponse>(json, JsonOptions);

        deserialized.Should().NotBeNull();
        deserialized!.State.Should().Be(state);
        deserialized.UserAvatar.Should().Be(avatar);
        deserialized.UserDisplayName.Should().Be(displayName);
        deserialized.SessionCode.Should().Be(sessionCode);
    }

    /// <summary>
    /// Verifies LoginSessionCookie key retention, expiration, and raw cookie dictionary.
    /// </summary>
    [Fact]
    [Trait("Category", "Models")]
    public void LoginSessionCookie_SerializationRoundTrip()
    {
        var dto = new LoginSessionCookie
        {
            ZpwSecKey = "sec_key_xyz",
            ZpwEncKey = "enc_aes_key_456",
            SessionId = "sess_001_abc",
            UserId = "user_10099",
            ExpiresAt = 1735689600000,
            RawCookies = new Dictionary<string, string>
            {
                { "zpw_sek", "sec_key_xyz" },
                { "zpw_enk", "enc_aes_key_456" },
                { "_zlang", "vi" }
            }
        };

        var json = JsonSerializer.Serialize(dto, JsonOptions);
        var deserialized = JsonSerializer.Deserialize<LoginSessionCookie>(json, JsonOptions);

        deserialized.Should().NotBeNull();
        deserialized!.ZpwSecKey.Should().Be("sec_key_xyz");
        deserialized.ZpwEncKey.Should().Be("enc_aes_key_456");
        deserialized.SessionId.Should().Be("sess_001_abc");
        deserialized.UserId.Should().Be("user_10099");
        deserialized.ExpiresAt.Should().Be(1735689600000);
        deserialized.RawCookies.Should().ContainKey("zpw_sek");
        deserialized.RawCookies["_zlang"].Should().Be("vi");
    }

    /// <summary>
    /// Verifies GatewayHandshakePayload header dictionaries and protocol strings.
    /// </summary>
    [Fact]
    [Trait("Category", "Models")]
    public void GatewayHandshakePayload_SerializationRoundTrip()
    {
        var dto = new GatewayHandshakePayload
        {
            ServerUrl = "wss://chat-gateway.zalo.me/ws",
            ProtocolVersion = "v2",
            ClientVersion = "24.11.1",
            SessionKey = "session_token_gateway",
            Timestamp = 1716005000000,
            Headers = new Dictionary<string, string>
            {
                { "User-Agent", "Zenix Desktop 1.0" },
                { "X-Zalo-Client", "PC" }
            }
        };

        var json = JsonSerializer.Serialize(dto, JsonOptions);
        var deserialized = JsonSerializer.Deserialize<GatewayHandshakePayload>(json, JsonOptions);

        deserialized.Should().NotBeNull();
        deserialized!.ServerUrl.Should().Be("wss://chat-gateway.zalo.me/ws");
        deserialized.ProtocolVersion.Should().Be("v2");
        deserialized.ClientVersion.Should().Be("24.11.1");
        deserialized.Headers.Should().ContainKey("X-Zalo-Client");
    }

    /// <summary>
    /// Verifies DohResolutionResult IP address lists and resolution states.
    /// </summary>
    [Fact]
    [Trait("Category", "Models")]
    public void DohResolutionResult_SerializationRoundTrip()
    {
        var dto = new DohResolutionResult
        {
            Hostname = "chat.zalo.me",
            IpAddresses = new List<string> { "49.213.117.20", "2405:4800:5400::20" },
            TtlSeconds = 300,
            ResolvedAtTimestamp = 1716010000000,
            IsSuccess = true,
            ProviderName = "Cloudflare"
        };

        var json = JsonSerializer.Serialize(dto, JsonOptions);
        var deserialized = JsonSerializer.Deserialize<DohResolutionResult>(json, JsonOptions);

        deserialized.Should().NotBeNull();
        deserialized!.Hostname.Should().Be("chat.zalo.me");
        deserialized.IpAddresses.Should().HaveCount(2);
        deserialized.IpAddresses[0].Should().Be("49.213.117.20");
        deserialized.IsSuccess.Should().BeTrue();
        deserialized.ProviderName.Should().Be("Cloudflare");
    }

    /// <summary>
    /// Verifies generic ZaloApiResponse envelope for success and failure cases.
    /// </summary>
    [Fact]
    [Trait("Category", "Models")]
    public void ZaloApiResponseGeneric_SuccessAndFailure()
    {
        var successResponse = new ZaloApiResponse<string>
        {
            ErrorCode = 0,
            ErrorMessage = "Success",
            Data = "Authorized payload"
        };
        successResponse.IsSuccess.Should().BeTrue();

        var successJson = JsonSerializer.Serialize(successResponse, JsonOptions);
        var deserializedSuccess = JsonSerializer.Deserialize<ZaloApiResponse<string>>(successJson, JsonOptions);
        deserializedSuccess.Should().NotBeNull();
        deserializedSuccess!.IsSuccess.Should().BeTrue();
        deserializedSuccess.Data.Should().Be("Authorized payload");

        var failureResponse = new ZaloApiResponse<string>
        {
            ErrorCode = -1004,
            ErrorMessage = "Session expired or invalid key",
            Data = null
        };
        failureResponse.IsSuccess.Should().BeFalse();

        var failureJson = JsonSerializer.Serialize(failureResponse, JsonOptions);
        var deserializedFailure = JsonSerializer.Deserialize<ZaloApiResponse<string>>(failureJson, JsonOptions);
        deserializedFailure.Should().NotBeNull();
        deserializedFailure!.IsSuccess.Should().BeFalse();
        deserializedFailure.ErrorCode.Should().Be(-1004);
        deserializedFailure.Data.Should().BeNull();
    }

    /// <summary>
    /// Verifies non-generic ZaloApiResponse envelope error codes and IsSuccess property.
    /// </summary>
    [Fact]
    [Trait("Category", "Models")]
    public void ZaloApiResponseNonGeneric_SuccessAndFailure()
    {
        var ok = new ZaloApiResponse { ErrorCode = 0, ErrorMessage = "OK" };
        ok.IsSuccess.Should().BeTrue();

        var err = new ZaloApiResponse { ErrorCode = -1, ErrorMessage = "Internal Error" };
        err.IsSuccess.Should().BeFalse();

        var json = JsonSerializer.Serialize(err, JsonOptions);
        var deserialized = JsonSerializer.Deserialize<ZaloApiResponse>(json, JsonOptions);
        deserialized.Should().NotBeNull();
        deserialized!.IsSuccess.Should().BeFalse();
        deserialized.ErrorCode.Should().Be(-1);
    }
}
