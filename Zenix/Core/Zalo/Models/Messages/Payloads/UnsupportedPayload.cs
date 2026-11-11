// Copyright (c) Zenix. All rights reserved.
// Licensed under the proprietary and confidential license.
// Unauthorized copying, distribution, modification, or disclosure of this file,
// via any medium, is strictly prohibited without prior written permission.

using Zenix.Core.Zalo.Models.Enums;

namespace Zenix.Core.Zalo.Models.Messages.Payloads;

/// <summary>
/// Represents the payload for an unrecognized, future, or unsupported message type from the wire.
/// </summary>
public sealed record UnsupportedPayload : ChatMessagePayload
{
    /// <inheritdoc />
    public override MessageKind Kind => MessageKind.Text;

    /// <summary>
    /// Gets the raw message kind integer received over the wire.
    /// </summary>
    public int RawKind { get; init; }

    /// <summary>
    /// Gets the verbatim raw JSON payload string preserved for auditing or forward-compatibility.
    /// </summary>
    public required string RawJson { get; init; }
}
