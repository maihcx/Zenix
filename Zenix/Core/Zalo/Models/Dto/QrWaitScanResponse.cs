// Copyright (c) Zenix. All rights reserved.
// Licensed under the proprietary and confidential license.
// Unauthorized copying, distribution, modification, or disclosure of this file,
// via any medium, is strictly prohibited without prior written permission.

using System.Text.Json.Serialization;
using Zenix.Core.Zalo.Models.Enums;

namespace Zenix.Core.Zalo.Models.Dto;

/// <summary>
/// Represents the polling response while awaiting QR code scan and confirmation on mobile.
/// </summary>
public sealed record QrWaitScanResponse
{
    /// <summary>
    /// Gets the scan status code.
    /// </summary>
    [JsonPropertyName("status")]
    public QrScanState State { get; init; } = QrScanState.Initial;

    /// <summary>
    /// Gets the authorized session code or token upon successful scan approval.
    /// </summary>
    [JsonPropertyName("session_code")]
    public string? SessionCode { get; init; }

    /// <summary>
    /// Gets the avatar URL of the user confirming the scan on mobile.
    /// </summary>
    [JsonPropertyName("user_avatar")]
    public string? UserAvatar { get; init; }

    /// <summary>
    /// Gets the display name of the user confirming the scan on mobile.
    /// </summary>
    [JsonPropertyName("user_display_name")]
    public string? UserDisplayName { get; init; }

    /// <summary>
    /// Gets the error message if the scan encountered an error or expired.
    /// </summary>
    [JsonPropertyName("error_message")]
    public string? ErrorMessage { get; init; }
}
