// Copyright (c) Zenix. All rights reserved.
// Licensed under the proprietary and confidential license.
// Unauthorized copying, distribution, modification, or disclosure of this file,
// via any medium, is strictly prohibited without prior written permission.

using Zenix.Core.Zalo.Models.Enums;

namespace Zenix.Core.Zalo.Models.Profiles;

/// <summary>
/// Represents membership information for a user within a group conversation.
/// </summary>
public sealed record GroupMember
{
    /// <summary>
    /// Gets the group identifier to which this membership belongs.
    /// </summary>
    public required string GroupId { get; init; }

    /// <summary>
    /// Gets the unique user identifier of the group member.
    /// </summary>
    public required string UserId { get; init; }

    /// <summary>
    /// Gets the member's display name within the group.
    /// </summary>
    public required string DisplayName { get; init; }

    /// <summary>
    /// Gets the avatar image URL of the member.
    /// </summary>
    public string? AvatarUrl { get; init; }

    /// <summary>
    /// Gets the administrative role of the member in the group.
    /// </summary>
    public GroupMemberRole Role { get; init; } = GroupMemberRole.Member;

    /// <summary>
    /// Gets the Unix epoch timestamp in milliseconds when the member joined the group.
    /// </summary>
    public long JoinedTimestamp { get; init; }

    /// <summary>
    /// Gets a value indicating whether this member is restricted or muted from sending messages.
    /// </summary>
    public bool IsMuted { get; init; }
}
