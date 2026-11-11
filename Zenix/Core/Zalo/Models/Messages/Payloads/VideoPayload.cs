// Copyright (c) Zenix. All rights reserved.
// Licensed under the proprietary and confidential license.
// Unauthorized copying, distribution, modification, or disclosure of this file,
// via any medium, is strictly prohibited without prior written permission.

using Zenix.Core.Zalo.Models.Enums;

namespace Zenix.Core.Zalo.Models.Messages.Payloads;

/// <summary>
/// Represents the payload for a video message.
/// </summary>
public sealed record VideoPayload : ChatMessagePayload
{
    /// <inheritdoc />
    public override MessageKind Kind => MessageKind.Video;

    /// <summary>
    /// Gets the video media streaming or download URL.
    /// </summary>
    public required string Url { get; init; }

    /// <summary>
    /// Gets the preview cover thumbnail image URL.
    /// </summary>
    public string? ThumbnailUrl { get; init; }

    /// <summary>
    /// Gets the playback duration in seconds.
    /// </summary>
    public int DurationSeconds { get; init; }

    /// <summary>
    /// Gets the pixel width of the video frame.
    /// </summary>
    public int Width { get; init; }

    /// <summary>
    /// Gets the pixel height of the video frame.
    /// </summary>
    public int Height { get; init; }

    /// <summary>
    /// Gets the file size in bytes of the video media.
    /// </summary>
    public long FileSizeBytes { get; init; }

    /// <summary>
    /// Gets the optional text caption accompanying the video.
    /// </summary>
    public string? Caption { get; init; }
}
