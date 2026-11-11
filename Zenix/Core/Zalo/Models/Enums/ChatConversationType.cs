// Copyright (c) Zenix. All rights reserved.
// Licensed under the proprietary and confidential license.
// Unauthorized copying, distribution, modification, or disclosure of this file,
// via any medium, is strictly prohibited without prior written permission.

namespace Zenix.Core.Zalo.Models.Enums;

/// <summary>
/// Specifies the type of chat conversation thread.
/// </summary>
public enum ChatConversationType
{
    /// <summary>
    /// Direct one-to-one conversation between two individual users.
    /// </summary>
    Direct = 0,

    /// <summary>
    /// Multi-user group chat conversation.
    /// </summary>
    Group = 1
}
