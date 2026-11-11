// Copyright (c) Zenix. All rights reserved.
// Licensed under the proprietary and confidential license.
// Unauthorized copying, distribution, modification, or disclosure of this file,
// via any medium, is strictly prohibited without prior written permission.

using Zenix.Core.Zalo.Models.Enums;

namespace Zenix.Core.Zalo.Models.Messages.Payloads;

/// <summary>
/// Represents the payload for a shared contact business card.
/// </summary>
public sealed record ContactCardPayload : ChatMessagePayload
{
    /// <inheritdoc />
    public override MessageKind Kind => MessageKind.ContactCard;

    /// <summary>
    /// Gets the unique user identifier of the shared contact.
    /// </summary>
    public required string ContactUserId { get; init; }

    /// <summary>
    /// Gets the display name of the shared contact.
    /// </summary>
    public required string ContactName { get; init; }

    /// <summary>
    /// Gets the phone number of the contact, if included in the card.
    /// </summary>
    public string? ContactPhoneNumber { get; init; }

    /// <summary>
    /// Gets the avatar image URL of the shared contact.
    /// </summary>
    public string? ContactAvatarUrl { get; init; }
}
