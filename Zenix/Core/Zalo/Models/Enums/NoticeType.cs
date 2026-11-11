// Copyright (c) Zenix. All rights reserved.
// Licensed under the proprietary and confidential license.
// Unauthorized copying, distribution, modification, or disclosure of this file,
// via any medium, is strictly prohibited without prior written permission.

namespace Zenix.Core.Zalo.Models.Enums;

/// <summary>
/// Specifies the classification of a group announcement or system notice.
/// </summary>
public enum NoticeType
{
    /// <summary>
    /// General system or informational broadcast.
    /// </summary>
    General = 0,

    /// <summary>
    /// Notice that a member joined the group.
    /// </summary>
    MemberJoined = 1,

    /// <summary>
    /// Notice that a member left or was removed from the group.
    /// </summary>
    MemberLeft = 2,

    /// <summary>
    /// Notice that a pinned announcement or message was updated.
    /// </summary>
    PinUpdated = 3,

    /// <summary>
    /// Notice that group security or encryption settings changed.
    /// </summary>
    SecurityChanged = 4
}
