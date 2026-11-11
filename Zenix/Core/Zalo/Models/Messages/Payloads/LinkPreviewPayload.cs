// Copyright (c) Zenix. All rights reserved.
// Licensed under the proprietary and confidential license.
// Unauthorized copying, distribution, modification, or disclosure of this file,
// via any medium, is strictly prohibited without prior written permission.

using Zenix.Core.Zalo.Models.Enums;

namespace Zenix.Core.Zalo.Models.Messages.Payloads;

/// <summary>
/// Represents the payload for a rich URL link preview card.
/// </summary>
public sealed record LinkPreviewPayload : ChatMessagePayload
{
    /// <inheritdoc />
    public override MessageKind Kind => MessageKind.LinkPreview;

    /// <summary>
    /// Gets the destination webpage URL.
    /// </summary>
    public required string Url { get; init; }

    /// <summary>
    /// Gets the OpenGraph title of the webpage.
    /// </summary>
    public string? Title { get; init; }

    /// <summary>
    /// Gets the summary description or snippet of the linked webpage.
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// Gets the thumbnail or representative banner image URL of the linked webpage.
    /// </summary>
    public string? ThumbnailUrl { get; init; }

    /// <summary>
    /// Gets the website domain or host authority (e.g. "zalo.me").
    /// </summary>
    public string? Domain { get; init; }
}
