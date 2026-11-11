// Copyright (c) Zenix. All rights reserved.
// Licensed under the proprietary and confidential license.
// Unauthorized copying, distribution, modification, or disclosure of this file,
// via any medium, is strictly prohibited without prior written permission.

using Zenix.Core.Zalo.Models.Enums;

namespace Zenix.Core.Zalo.Models.Messages;

/// <summary>
/// Represents a condensed summary of a quoted or replied-to message.
/// </summary>
public sealed record QuotedMessageSummary
{
    /// <summary>
    /// Gets the unique identifier of the quoted message.
    /// </summary>
    public required string MessageId { get; init; }

    /// <summary>
    /// Gets the user identifier of the original sender.
    /// </summary>
    public required string SenderId { get; init; }

    /// <summary>
    /// Gets the display name of the original sender at the time of quotation.
    /// </summary>
    public required string SenderName { get; init; }

    /// <summary>
    /// Gets the text snippet or label representing the quoted content.
    /// </summary>
    public required string Snippet { get; init; }

    /// <summary>
    /// Gets the message kind of the quoted message.
    /// </summary>
    public MessageKind Kind { get; init; } = MessageKind.Text;

    /// <summary>
    /// Gets the optional thumbnail image URL if the quoted message was visual media.
    /// </summary>
    public string? ThumbnailUrl { get; init; }
}
