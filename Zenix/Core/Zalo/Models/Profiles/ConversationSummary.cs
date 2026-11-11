// Copyright (c) Zenix. All rights reserved.
// Licensed under the proprietary and confidential license.
// Unauthorized copying, distribution, modification, or disclosure of this file,
// via any medium, is strictly prohibited without prior written permission.

using Zenix.Core.Zalo.Models.Enums;

namespace Zenix.Core.Zalo.Models.Profiles;

/// <summary>
/// Represents a condensed summary of a chat conversation for list and navigation views.
/// </summary>
public sealed record ConversationSummary
{
    /// <summary>
    /// Gets the unique conversation thread identifier (User ID for direct, Group ID for group).
    /// </summary>
    public required string ConversationId { get; init; }

    /// <summary>
    /// Gets the conversation type (Direct or Group).
    /// </summary>
    public ChatConversationType ConversationType { get; init; } = ChatConversationType.Direct;

    /// <summary>
    /// Gets the display title of the conversation.
    /// </summary>
    public required string Title { get; init; }

    /// <summary>
    /// Gets the URL of the conversation avatar image.
    /// </summary>
    public string? AvatarUrl { get; init; }

    /// <summary>
    /// Gets the text snippet preview of the most recent message.
    /// </summary>
    public string? Snippet { get; init; }

    /// <summary>
    /// Gets the display name of the sender of the most recent message.
    /// </summary>
    public string? LastMessageSenderName { get; init; }

    /// <summary>
    /// Gets the Unix epoch timestamp in milliseconds of the latest activity or message.
    /// </summary>
    public long LastActivityTimestamp { get; init; }

    /// <summary>
    /// Gets the count of unread messages in this conversation.
    /// </summary>
    public int UnreadCount { get; init; }

    /// <summary>
    /// Gets the bitmask action and status flags associated with this conversation thread.
    /// </summary>
    public ConversationActionFlags Flags { get; init; }

    /// <summary>
    /// Gets the delivery status of the last message sent in this conversation.
    /// </summary>
    public MessageDeliveryStatus LastDeliveryStatus { get; init; } = MessageDeliveryStatus.Sent;

    /// <summary>
    /// Gets a value indicating whether notifications for this conversation are muted.
    /// </summary>
    public bool IsMuted { get; init; }

    /// <summary>
    /// Gets a value indicating whether this conversation is pinned to the top of the chat list.
    /// </summary>
    public bool IsPinned => Flags.HasFlag(ConversationActionFlags.Pinned);

    /// <summary>
    /// Gets a value indicating whether this conversation is archived.
    /// </summary>
    public bool IsArchived => Flags.HasFlag(ConversationActionFlags.Archived);

    /// <summary>
    /// Gets a value indicating whether this conversation is hidden behind a PIN.
    /// </summary>
    public bool IsHidden => Flags.HasFlag(ConversationActionFlags.Hidden);
}
