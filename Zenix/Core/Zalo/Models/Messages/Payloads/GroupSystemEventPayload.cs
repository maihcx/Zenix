// Copyright (c) Zenix. All rights reserved.
// Licensed under the proprietary and confidential license.
// Unauthorized copying, distribution, modification, or disclosure of this file,
// via any medium, is strictly prohibited without prior written permission.

using Zenix.Core.Zalo.Models.Enums;

namespace Zenix.Core.Zalo.Models.Messages.Payloads;

/// <summary>
/// Represents the payload for a group administrative or system event notice.
/// </summary>
public sealed record GroupSystemEventPayload : ChatMessagePayload
{
    /// <inheritdoc />
    public override MessageKind Kind => MessageKind.GroupSystemEvent;

    /// <summary>
    /// Gets the system event code identifying the type of change.
    /// </summary>
    public SystemEventCode EventCode { get; init; } = SystemEventCode.Unknown;

    /// <summary>
    /// Gets the user ID of the member who initiated the action.
    /// </summary>
    public required string ActorUserId { get; init; }

    /// <summary>
    /// Gets the display name of the actor.
    /// </summary>
    public required string ActorName { get; init; }

    /// <summary>
    /// Gets the target user ID affected by this event, if applicable.
    /// </summary>
    public string? TargetUserId { get; init; }

    /// <summary>
    /// Gets the target member's name, if applicable.
    /// </summary>
    public string? TargetName { get; init; }

    /// <summary>
    /// Gets the human-readable description of the system event.
    /// </summary>
    public string? EventDescription { get; init; }

    /// <summary>
    /// Gets the previous value before the change (e.g. former group name).
    /// </summary>
    public string? OldValue { get; init; }

    /// <summary>
    /// Gets the new value after the change (e.g. updated group name).
    /// </summary>
    public string? NewValue { get; init; }
}
