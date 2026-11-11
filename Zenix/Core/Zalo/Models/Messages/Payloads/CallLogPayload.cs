// Copyright (c) Zenix. All rights reserved.
// Licensed under the proprietary and confidential license.
// Unauthorized copying, distribution, modification, or disclosure of this file,
// via any medium, is strictly prohibited without prior written permission.

using Zenix.Core.Zalo.Models.Enums;

namespace Zenix.Core.Zalo.Models.Messages.Payloads;

/// <summary>
/// Represents the payload for a voice or video call log entry.
/// </summary>
public sealed record CallLogPayload : ChatMessagePayload
{
    /// <inheritdoc />
    public override MessageKind Kind => MessageKind.CallLog;

    /// <summary>
    /// Gets the unique call session identifier.
    /// </summary>
    public required string CallId { get; init; }

    /// <summary>
    /// Gets the connected duration of the call in seconds (0 for missed/unanswered calls).
    /// </summary>
    public int CallDurationSeconds { get; init; }

    /// <summary>
    /// Gets a value indicating whether this was a video call (true) or voice call (false).
    /// </summary>
    public bool IsVideo { get; init; }

    /// <summary>
    /// Gets a value indicating whether the call was missed or unanswered.
    /// </summary>
    public bool IsMissed { get; init; }

    /// <summary>
    /// Gets the user ID of the caller who initiated the call.
    /// </summary>
    public required string CallerId { get; init; }
}
