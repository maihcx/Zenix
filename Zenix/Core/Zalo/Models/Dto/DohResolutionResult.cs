// Copyright (c) Zenix. All rights reserved.
// Licensed under the proprietary and confidential license.
// Unauthorized copying, distribution, modification, or disclosure of this file,
// via any medium, is strictly prohibited without prior written permission.

using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Zenix.Core.Zalo.Models.Dto;

/// <summary>
/// Represents the resolved IP addresses and metadata from a DNS-over-HTTPS (DoH) query.
/// </summary>
public sealed record DohResolutionResult
{
    /// <summary>
    /// Gets the requested domain name (e.g. "chat.zalo.me").
    /// </summary>
    [JsonPropertyName("hostname")]
    public required string Hostname { get; init; }

    /// <summary>
    /// Gets the resolved IPv4 and IPv6 IP addresses.
    /// </summary>
    [JsonPropertyName("ip_addresses")]
    public IReadOnlyList<string> IpAddresses { get; init; } = Array.Empty<string>();

    /// <summary>
    /// Gets the DNS record Time-to-Live (TTL) in seconds.
    /// </summary>
    [JsonPropertyName("ttl_seconds")]
    public int TtlSeconds { get; init; }

    /// <summary>
    /// Gets the Unix epoch timestamp in milliseconds when resolution completed.
    /// </summary>
    [JsonPropertyName("resolved_at_timestamp")]
    public long ResolvedAtTimestamp { get; init; }

    /// <summary>
    /// Gets a value indicating whether the DoH query succeeded and resolved at least one IP address.
    /// </summary>
    [JsonPropertyName("is_success")]
    public bool IsSuccess { get; init; }

    /// <summary>
    /// Gets the upstream DoH provider name (e.g. "Cloudflare", "Google").
    /// </summary>
    [JsonPropertyName("provider_name")]
    public string? ProviderName { get; init; }
}
