// Copyright (c) Zenix. All rights reserved.
// Licensed under the proprietary and confidential license.
// Unauthorized copying, distribution, modification, or disclosure of this file,
// via any medium, is strictly prohibited without prior written permission.

namespace Zenix.Core.Zalo.Models.Enums;

/// <summary>
/// Specifies the transport connection state of the network client to the gateway.
/// </summary>
public enum NetworkConnectionState
{
    /// <summary>
    /// Gateway transport is completely disconnected.
    /// </summary>
    Disconnected = 0,

    /// <summary>
    /// Actively establishing a transport handshake with the gateway.
    /// </summary>
    Connecting = 1,

    /// <summary>
    /// Handshake succeeded and real-time bidirectional gateway session is active.
    /// </summary>
    Connected = 2,

    /// <summary>
    /// Re-establishing connection following network disruption.
    /// </summary>
    Reconnecting = 3
}
