// Copyright (c) Zenix. All rights reserved.
// Licensed under the proprietary and confidential license.
// Unauthorized copying, distribution, modification, or disclosure of this file,
// via any medium, is strictly prohibited without prior written permission.

using Zenix.Core.Zalo.Models.Enums;

namespace Zenix.Core.Zalo.Models.Messages.Payloads;

/// <summary>
/// Represents the payload for an image or photograph message.
/// </summary>
public sealed record PhotoPayload : ChatMessagePayload
{
    /// <inheritdoc />
    public override MessageKind Kind => MessageKind.Photo;

    /// <summary>
    /// Gets the full resolution image URL.
    /// </summary>
    public required string Url { get; init; }

    /// <summary>
    /// Gets the thumbnail preview image URL.
    /// </summary>
    public string? ThumbnailUrl { get; init; }

    /// <summary>
    /// Gets the width in pixels of the image.
    /// </summary>
    public int Width { get; init; }

    /// <summary>
    /// Gets the height in pixels of the image.
    /// </summary>
    public int Height { get; init; }

    /// <summary>
    /// Gets the file size of the photo in bytes.
    /// </summary>
    public long FileSizeBytes { get; init; }

    /// <summary>
    /// Gets the optional caption text accompanying the photo.
    /// </summary>
    public string? Caption { get; init; }

    /// <summary>
    /// Gets the cryptographic hash or checksum of the file.
    /// </summary>
    public string? Checksum { get; init; }
}
