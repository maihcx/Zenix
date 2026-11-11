// Copyright (c) Zenix. All rights reserved.
// Licensed under the proprietary and confidential license.
// Unauthorized copying, distribution, modification, or disclosure of this file,
// via any medium, is strictly prohibited without prior written permission.

using Zenix.Core.Zalo.Models.Enums;

namespace Zenix.Core.Zalo.Models.Messages.Payloads;

/// <summary>
/// Represents the payload for a recalled or un-sent message (revoked for everyone).
/// </summary>
public sealed record RecalledPayload : ChatMessagePayload
{
    /// <inheritdoc />
    public override MessageKind Kind => MessageKind.Recalled;

    /// <summary>
    /// Gets the unique identifier of the original recalled message.
    /// </summary>
    public required string OriginalMessageId { get; init; }

    /// <summary>
    /// Gets the localized notice text explaining the message was recalled.
    /// </summary>
    public string RecallNotice { get; init; } = "Tin nhắn đã được thu hồi";

    /// <summary>
    /// Gets the Unix epoch timestamp in milliseconds when the message was recalled.
    /// </summary>
    public long RecallTimestamp { get; init; }
}
