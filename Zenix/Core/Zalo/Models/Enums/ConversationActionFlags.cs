// Copyright (c) Zenix. All rights reserved.
// Licensed under the proprietary and confidential license.
// Unauthorized copying, distribution, modification, or disclosure of this file,
// via any medium, is strictly prohibited without prior written permission.

using System;

namespace Zenix.Core.Zalo.Models.Enums;

/// <summary>
/// Bitmask flags representing UI presentation, organization, and permissions for a conversation.
/// </summary>
[Flags]
public enum ConversationActionFlags
{
    /// <summary>
    /// No flags set.
    /// </summary>
    None = 0,

    /// <summary>
    /// Thread is pinned to the top of the conversation list.
    /// </summary>
    Pinned = 1,

    /// <summary>
    /// Thread is archived.
    /// </summary>
    Archived = 2,

    /// <summary>
    /// Thread is hidden behind a PIN or privacy lock.
    /// </summary>
    Hidden = 4,

    /// <summary>
    /// User is restricted from pinning this thread.
    /// </summary>
    CannotPin = 8,

    /// <summary>
    /// User is restricted from archiving this thread.
    /// </summary>
    CannotArchive = 16,

    /// <summary>
    /// User is restricted from muting this thread.
    /// </summary>
    CannotMute = 32,

    /// <summary>
    /// User is restricted from deleting this thread.
    /// </summary>
    CannotDelete = 64,

    /// <summary>
    /// Pin action should not appear in the context menu.
    /// </summary>
    NoPinMenu = 128,

    /// <summary>
    /// User is restricted from hiding this thread.
    /// </summary>
    CannotHide = 256,

    /// <summary>
    /// Thread is manually marked as unread by the user.
    /// </summary>
    UnreadMark = 512
}
