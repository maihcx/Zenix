// Copyright (c) Zenix. All rights reserved.
// Licensed under the proprietary and confidential license.
// Unauthorized copying, distribution, modification, or disclosure of this file,
// via any medium, is strictly prohibited without prior written permission.

using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Zenix.Core.Zalo.Models.Dto;

/// <summary>
/// Represents the initialization and handshake payload sent to connect to the Zalo WebSocket gateway.
/// </summary>
public sealed record GatewayHandshakePayload
{
    /// <summary>
    /// Gets the WebSocket server gateway URL.
    /// </summary>
    [JsonPropertyName("server_url")]
    public required string ServerUrl { get; init; }

    /// <summary>
    /// Gets the gateway protocol version (e.g. "v2").
    /// </summary>
    [JsonPropertyName("protocol_version")]
    public string ProtocolVersion { get; init; } = "v2";

    /// <summary>
    /// Gets the client software version identifier.
    /// </summary>
    [JsonPropertyName("client_version")]
    public string ClientVersion { get; init; } = "24.11.1";

    /// <summary>
    /// Gets the session authentication key.
    /// </summary>
    [JsonPropertyName("session_key")]
    public required string SessionKey { get; init; }

    /// <summary>
    /// Gets the Unix epoch timestamp in milliseconds when the handshake was prepared.
    /// </summary>
    [JsonPropertyName("timestamp")]
    public long Timestamp { get; init; }

    /// <summary>
    /// Gets custom HTTP/WebSocket connection headers.
    /// </summary>
    [JsonPropertyName("headers")]
    public IReadOnlyDictionary<string, string> Headers { get; init; } = new Dictionary<string, string>();
}
