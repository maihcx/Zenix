// Copyright (c) Zenix. All rights reserved.
// Licensed under the proprietary and confidential license.
// Unauthorized copying, distribution, modification, or disclosure of this file,
// via any medium, is strictly prohibited without prior written permission.

using System;
using System.Collections.Generic;
using Zenix.Core.Zalo.Models.Enums;
using Zenix.Core.Zalo.Models.Messages;

namespace Zenix.Core.Zalo.Models.Messages.Payloads;

/// <summary>
/// Represents the payload for a group poll / voting card.
/// </summary>
public sealed record PollPayload : ChatMessagePayload
{
    /// <inheritdoc />
    public override MessageKind Kind => MessageKind.Poll;

    /// <summary>
    /// Gets the unique identifier for this poll.
    /// </summary>
    public required string PollId { get; init; }

    /// <summary>
    /// Gets the question or title of the poll.
    /// </summary>
    public required string Question { get; init; }

    /// <summary>
    /// Gets the list of choices or options available in the poll.
    /// </summary>
    public IReadOnlyList<PollOption> Options { get; init; } = Array.Empty<PollOption>();

    /// <summary>
    /// Gets a value indicating whether voters may select multiple choices.
    /// </summary>
    public bool AllowMultipleChoices { get; init; }

    /// <summary>
    /// Gets a value indicating whether members are allowed to add custom options to the poll.
    /// </summary>
    public bool AllowAddOptions { get; init; }

    /// <summary>
    /// Gets a value indicating whether voting has been closed.
    /// </summary>
    public bool IsClosed { get; init; }

    /// <summary>
    /// Gets the user ID of the poll creator.
    /// </summary>
    public required string CreatorId { get; init; }

    /// <summary>
    /// Gets the Unix epoch timestamp in milliseconds when the poll expires, or 0 if no expiration.
    /// </summary>
    public long ExpirationTimestamp { get; init; }
}
