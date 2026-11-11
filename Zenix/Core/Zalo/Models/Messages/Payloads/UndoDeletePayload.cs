// Copyright (c) Zenix. All rights reserved.
// Licensed under the proprietary and confidential license.
// Unauthorized copying, distribution, modification, or disclosure of this file,
// via any medium, is strictly prohibited without prior written permission.

using Zenix.Core.Zalo.Models.Enums;

namespace Zenix.Core.Zalo.Models.Messages.Payloads;

/// <summary>
/// Represents the payload notifying that a previously deleted message was restored or deletion reverted locally.
/// </summary>
public sealed record UndoDeletePayload : ChatMessagePayload
{
    /// <inheritdoc />
    public override MessageKind Kind => MessageKind.UndoDelete;

    /// <summary>
    /// Gets the unique identifier of the target message whose deletion was undone.
    /// </summary>
    public required string TargetMessageId { get; init; }

    /// <summary>
    /// Gets the Unix epoch timestamp in milliseconds when the deletion event occurred.
    /// </summary>
    public long DeleteTimestamp { get; init; }
}
