// Copyright (c) Zenix. All rights reserved.
// Licensed under the proprietary and confidential license.
// Unauthorized copying, distribution, modification, or disclosure of this file,
// via any medium, is strictly prohibited without prior written permission.

using System.Text.Json.Serialization;

namespace Zenix.Core.Zalo.Models.Dto;

/// <summary>
/// Represents the wire response when requesting a QR code login token from Zalo auth servers.
/// </summary>
public sealed record QrCodeTokenResponse
{
    /// <summary>
    /// Gets the unique QR login session token.
    /// </summary>
    [JsonPropertyName("token")]
    public required string Token { get; init; }

    /// <summary>
    /// Gets the QR code image URL or base64 data string.
    /// </summary>
    [JsonPropertyName("qr_code_url")]
    public required string QrCodeUrl { get; init; }

    /// <summary>
    /// Gets the validity duration of the QR code in seconds.
    /// </summary>
    [JsonPropertyName("expires_in")]
    public int ExpiresInSeconds { get; init; }

    /// <summary>
    /// Gets the Unix epoch timestamp in milliseconds when the QR code was generated.
    /// </summary>
    [JsonPropertyName("created_timestamp")]
    public long CreatedTimestamp { get; init; }
}
