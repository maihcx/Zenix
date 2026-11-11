// Copyright (c) Zenix. All rights reserved.
// Licensed under the proprietary and confidential license.
// Unauthorized copying, distribution, modification, or disclosure of this file,
// via any medium, is strictly prohibited without prior written permission.

namespace Zenix.Core.Zalo.Models.Profiles;

/// <summary>
/// Represents profile and metadata for a Zalo group chat conversation.
/// </summary>
public sealed record GroupProfile
{
    /// <summary>
    /// Gets the unique group identifier.
    /// </summary>
    public required string GroupId { get; init; }

    /// <summary>
    /// Gets the title or name of the group.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Gets the URL of the group avatar image, if configured.
    /// </summary>
    public string? AvatarUrl { get; init; }

    /// <summary>
    /// Gets the user ID of the group creator.
    /// </summary>
    public required string CreatorId { get; init; }

    /// <summary>
    /// Gets the total count of members in the group.
    /// </summary>
    public int MemberCount { get; init; }

    /// <summary>
    /// Gets a value indicating whether end-to-end encryption (E2EE) is active for this group.
    /// </summary>
    public bool IsE2ee { get; init; }

    /// <summary>
    /// Gets a value indicating whether notifications for this group are muted.
    /// </summary>
    public bool IsMuted { get; init; }

    /// <summary>
    /// Gets a value indicating whether disappearing messages are enabled in this group.
    /// </summary>
    public bool IsDisappearing { get; init; }

    /// <summary>
    /// Gets the disappearing message duration in seconds, or 0 if disabled.
    /// </summary>
    public int DisappearingDurationSeconds { get; init; }

    /// <summary>
    /// Gets the Unix epoch timestamp in milliseconds when the group was created.
    /// </summary>
    public long CreatedTimestamp { get; init; }

    /// <summary>
    /// Gets the group topic or description text.
    /// </summary>
    public string? Description { get; init; }
}
