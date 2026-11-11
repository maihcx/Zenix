// Copyright (c) Zenix. All rights reserved.
// Licensed under the proprietary and confidential license.
// Unauthorized copying, distribution, modification, or disclosure of this file,
// via any medium, is strictly prohibited without prior written permission.

using Zenix.Core.Zalo.Models.Enums;

namespace Zenix.Core.Zalo.Models.Messages.Payloads;

/// <summary>
/// Represents the payload for a Zalo animated or static sticker.
/// </summary>
public sealed record StickerPayload : ChatMessagePayload
{
    /// <inheritdoc />
    public override MessageKind Kind => MessageKind.Sticker;

    /// <summary>
    /// Gets the unique sticker identifier within the pack.
    /// </summary>
    public int StickerId { get; init; }

    /// <summary>
    /// Gets the sticker category or pack identifier.
    /// </summary>
    public int CategoryId { get; init; }

    /// <summary>
    /// Gets the image or animation URL of the sticker.
    /// </summary>
    public required string Url { get; init; }

    /// <summary>
    /// Gets the optional animation sprite-sheet URL.
    /// </summary>
    public string? SpriteUrl { get; init; }

    /// <summary>
    /// Gets the display width of the sticker.
    /// </summary>
    public int Width { get; init; }

    /// <summary>
    /// Gets the display height of the sticker.
    /// </summary>
    public int Height { get; init; }
}
