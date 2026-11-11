// Copyright (c) Zenix. All rights reserved.
// Licensed under the proprietary and confidential license.
// Unauthorized copying, distribution, modification, or disclosure of this file,
// via any medium, is strictly prohibited without prior written permission.

using System;
using System.Collections.Generic;

namespace Zenix.Core.Zalo.Models.Messages;

/// <summary>
/// Represents a voting option within a group poll.
/// </summary>
public sealed record PollOption
{
    /// <summary>
    /// Gets the unique identifier for this poll option.
    /// </summary>
    public required string OptionId { get; init; }

    /// <summary>
    /// Gets the option text description.
    /// </summary>
    public required string Content { get; init; }

    /// <summary>
    /// Gets the total number of votes cast for this option.
    /// </summary>
    public int VoteCount { get; init; }

    /// <summary>
    /// Gets the list of user IDs who voted for this option.
    /// </summary>
    public IReadOnlyList<string> VoterUserIds { get; init; } = Array.Empty<string>();
}
