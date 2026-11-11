// Copyright (c) Zenix. All rights reserved.
// Licensed under the proprietary and confidential license.
// Unauthorized copying, distribution, modification, or disclosure of this file,
// via any medium, is strictly prohibited without prior written permission.

namespace Zenix.Core.Zalo.Models.Enums;

/// <summary>
/// Specifies the numeric identifier code for system-generated group events.
/// </summary>
public enum SystemEventCode
{
    /// <summary>
    /// Unspecified system event.
    /// </summary>
    Unknown = 0,

    /// <summary>
    /// Group title was updated.
    /// </summary>
    GroupRenamed = 101,

    /// <summary>
    /// Group avatar image was updated.
    /// </summary>
    GroupAvatarChanged = 102,

    /// <summary>
    /// End-to-end encryption safety number was rotated or changed.
    /// </summary>
    E2EESafetyNumberChanged = 201,

    /// <summary>
    /// Voice or video call was missed or unanswered.
    /// </summary>
    CallMissed = 301
}
