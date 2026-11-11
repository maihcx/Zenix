// Copyright (c) Zenix. All rights reserved.
// Licensed under the proprietary and confidential license.
// Unauthorized copying, distribution, modification, or disclosure of this file,
// via any medium, is strictly prohibited without prior written permission.

using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Zenix.Core.Zalo.Models.Dto;

/// <summary>
/// Represents authenticated session cookies and cryptographic keys acquired after login.
/// </summary>
public sealed record LoginSessionCookie
{
    /// <summary>
    /// Gets the primary Zalo password/security secret key (zpw_sek).
    /// </summary>
    [JsonPropertyName("zpw_sec_key")]
    public required string ZpwSecKey { get; init; }

    /// <summary>
    /// Gets the AES payload encryption key (zpw_enk) used to encrypt and decrypt chat payloads.
    /// </summary>
    [JsonPropertyName("zpw_enc_key")]
    public required string ZpwEncKey { get; init; }

    /// <summary>
    /// Gets the primary session identifier.
    /// </summary>
    [JsonPropertyName("session_id")]
    public required string SessionId { get; init; }

    /// <summary>
    /// Gets the authenticated user ID.
    /// </summary>
    [JsonPropertyName("user_id")]
    public required string UserId { get; init; }

    /// <summary>
    /// Gets the Unix epoch timestamp in milliseconds when this session expires.
    /// </summary>
    [JsonPropertyName("expires_at")]
    public long ExpiresAt { get; init; }

    /// <summary>
    /// Gets the complete dictionary of raw cookie name-value pairs.
    /// </summary>
    [JsonPropertyName("raw_cookies")]
    public IReadOnlyDictionary<string, string> RawCookies { get; init; } = new Dictionary<string, string>();
}
