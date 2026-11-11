// Copyright (c) Zenix. All rights reserved.
// Licensed under the proprietary and confidential license.
// Unauthorized copying, distribution, modification, or disclosure of this file,
// via any medium, is strictly prohibited without prior written permission.

namespace Zenix.Core.Zalo.Models.Messages;

/// <summary>
/// Represents a user @mention token within the text content of a message.
/// </summary>
public sealed record MentionSpan
{
    /// <summary>
    /// Gets the unique user identifier of the mentioned user.
    /// </summary>
    public required string UserId { get; init; }

    /// <summary>
    /// Gets the zero-based character index where the mention begins.
    /// </summary>
    public int Offset { get; init; }

    /// <summary>
    /// Gets the length in characters of the mention span in the text.
    /// </summary>
    public int Length { get; init; }

    /// <summary>
    /// Gets the displayed text representing the mention (e.g. "@NguyenVanA").
    /// </summary>
    public required string DisplayText { get; init; }
}
