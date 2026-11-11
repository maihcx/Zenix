// Copyright (c) Zenix. All rights reserved.
// Licensed under the proprietary and confidential license.
// Unauthorized copying, distribution, modification, or disclosure of this file,
// via any medium, is strictly prohibited without prior written permission.

namespace Zenix.Core.Zalo.Models.Enums;

/// <summary>
/// Specifies the delivery lifecycle status of a chat message.
/// </summary>
public enum MessageDeliveryStatus
{
    /// <summary>
    /// Message failed to send or deliver due to transport or network error.
    /// </summary>
    Failed = -2,

    /// <summary>
    /// Message is stored locally as an unsent draft.
    /// </summary>
    Draft = 0,

    /// <summary>
    /// Message has been acknowledged and accepted by the server.
    /// </summary>
    Sent = 1,

    /// <summary>
    /// Message has been delivered to the recipient device.
    /// </summary>
    Delivered = 2,

    /// <summary>
    /// Message has been seen or read by the recipient.
    /// </summary>
    Seen = 3,

    /// <summary>
    /// Message has been seen by all recipients in a group conversation.
    /// </summary>
    SeenAll = 4,

    /// <summary>
    /// Alias for SeenAll indicating full group read receipt.
    /// </summary>
    SeenFull = SeenAll,

    /// <summary>
    /// Message is actively being queued and transmitted to the server.
    /// </summary>
    Sending = 10
}
