// Copyright (c) Zenix. All rights reserved.
// Licensed under the proprietary and confidential license.
// Unauthorized copying, distribution, modification, or disclosure of this file,
// via any medium, is strictly prohibited without prior written permission.

namespace Zenix.Core.Zalo.Models.Messages;

/// <summary>
/// Represents an emoji or emotive reaction attached to a chat message.
/// </summary>
public sealed record MessageReaction
{
    /// <summary>
    /// Gets the unique user identifier of the member who reacted.
    /// </summary>
    public required string UserId { get; init; }

    /// <summary>
    /// Gets the emoji symbol or icon identifier representing the reaction (e.g., "❤️", "👍").
    /// </summary>
    public required string Icon { get; init; }

    /// <summary>
    /// Gets the Unix epoch timestamp in milliseconds when the reaction was placed.
    /// </summary>
    public long Timestamp { get; init; }
}
