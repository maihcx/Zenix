// Copyright (c) Zenix. All rights reserved.
// Licensed under the proprietary and confidential license.
// Unauthorized copying, distribution, modification, or disclosure of this file,
// via any medium, is strictly prohibited without prior written permission.

using Zenix.Core.Zalo.Models.Enums;

namespace Zenix.Core.Zalo.Models.Messages.Payloads;

/// <summary>
/// Represents the payload for a shared geographic location pin or point of interest.
/// </summary>
public sealed record LocationPayload : ChatMessagePayload
{
    /// <inheritdoc />
    public override MessageKind Kind => MessageKind.Location;

    /// <summary>
    /// Gets the latitude coordinate in decimal degrees.
    /// </summary>
    public double Latitude { get; init; }

    /// <summary>
    /// Gets the longitude coordinate in decimal degrees.
    /// </summary>
    public double Longitude { get; init; }

    /// <summary>
    /// Gets the human-readable street address or venue description.
    /// </summary>
    public string? Address { get; init; }

    /// <summary>
    /// Gets the title or place name of the location pin.
    /// </summary>
    public string? PlaceName { get; init; }

    /// <summary>
    /// Gets the map preview thumbnail image URL.
    /// </summary>
    public string? ThumbnailUrl { get; init; }
}
