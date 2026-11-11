// Copyright (c) Zenix. All rights reserved.
// Licensed under the proprietary and confidential license.
// Unauthorized copying, distribution, modification, or disclosure of this file,
// via any medium, is strictly prohibited without prior written permission.

using System;
using System.Collections.Generic;
using Zenix.Core.Zalo.Models.Enums;

namespace Zenix.Core.Zalo.Models.Messages;

/// <summary>
/// Represents a fully-formed chat message item within a conversation thread.
/// </summary>
public sealed record ChatMessage
{
    /// <summary>
    /// Gets the server-assigned unique message identifier.
    /// </summary>
    public required string MessageId { get; init; }

    /// <summary>
    /// Gets the client-generated message identifier used for optimistic rendering and deduplication.
    /// </summary>
    public required string ClientMessageId { get; init; }

    /// <summary>
    /// Gets the conversation identifier to which this message belongs.
    /// </summary>
    public required string ConversationId { get; init; }

    /// <summary>
    /// Gets the conversation type (Direct or Group).
    /// </summary>
    public ChatConversationType ConversationType { get; init; } = ChatConversationType.Direct;

    /// <summary>
    /// Gets the user ID of the sender.
    /// </summary>
    public required string SenderId { get; init; }

    /// <summary>
    /// Gets the display name snapshot of the sender at the time of sending.
    /// </summary>
    public string? SenderName { get; init; }

    /// <summary>
    /// Gets the avatar image URL snapshot of the sender.
    /// </summary>
    public string? SenderAvatarUrl { get; init; }

    /// <summary>
    /// Gets a value indicating whether this message was sent by the currently authenticated user.
    /// </summary>
    public bool IsSelf { get; init; }

    /// <summary>
    /// Gets the high-level message kind.
    /// </summary>
    public MessageKind Kind { get; init; } = MessageKind.Text;

    /// <summary>
    /// Gets the strongly-typed polymorphic message payload.
    /// </summary>
    public required ChatMessagePayload Payload { get; init; }

    /// <summary>
    /// Gets the message delivery lifecycle status.
    /// </summary>
    public MessageDeliveryStatus DeliveryStatus { get; init; } = MessageDeliveryStatus.Sent;

    /// <summary>
    /// Gets the Unix epoch timestamp in milliseconds when the message was sent or recorded.
    /// </summary>
    public long Timestamp { get; init; }

    /// <summary>
    /// Gets the identifier of the quoted message if this is a reply, or null.
    /// </summary>
    public string? QuoteMessageId { get; init; }

    /// <summary>
    /// Gets the summary of the quoted reply message, if present.
    /// </summary>
    public QuotedMessageSummary? QuotedMessage { get; init; }

    /// <summary>
    /// Gets the collection of @mention spans contained in this message.
    /// </summary>
    public IReadOnlyList<MentionSpan> Mentions { get; init; } = Array.Empty<MentionSpan>();

    /// <summary>
    /// Gets the collection of emotive reactions attached to this message.
    /// </summary>
    public IReadOnlyList<MessageReaction> Reactions { get; init; } = Array.Empty<MessageReaction>();

    /// <summary>
    /// Gets a value indicating whether this message was edited after delivery.
    /// </summary>
    public bool IsEdited { get; init; }

    /// <summary>
    /// Gets a value indicating whether this message was recalled.
    /// </summary>
    public bool IsRecalled { get; init; }

    /// <summary>
    /// Gets a value indicating whether this message is pinned to the conversation header.
    /// </summary>
    public bool IsPinned { get; init; }
}
