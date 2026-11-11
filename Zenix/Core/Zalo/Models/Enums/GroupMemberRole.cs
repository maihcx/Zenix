// Copyright (c) Zenix. All rights reserved.
// Licensed under the proprietary and confidential license.
// Unauthorized copying, distribution, modification, or disclosure of this file,
// via any medium, is strictly prohibited without prior written permission.

namespace Zenix.Core.Zalo.Models.Enums;

/// <summary>
/// Specifies the administrative privilege role of a member in a group.
/// </summary>
public enum GroupMemberRole
{
    /// <summary>
    /// Standard group participant with regular member permissions.
    /// </summary>
    Member = 0,

    /// <summary>
    /// Group administrator with moderation and membership management privileges.
    /// </summary>
    Admin = 1,

    /// <summary>
    /// Group owner/creator with ultimate control over settings and admin roles.
    /// </summary>
    Creator = 2
}
