// Copyright (c) Zenix. All rights reserved.
// Licensed under the proprietary and confidential license.
// Unauthorized copying, distribution, modification, or disclosure of this file,
// via any medium, is strictly prohibited without prior written permission.

using System.Collections.Generic;
using Zenix.Core.Zalo.Models.Enums;
using Zenix.Core.Zalo.Models.Messages;

namespace Zenix.Core.Zalo.Models.Messages.Payloads;

/// <summary>
/// Represents the payload for a greeting e-card or formatted celebratory message.
/// </summary>
public sealed record ECardPayload : ChatMessagePayload
{
    /// <inheritdoc />
    public override MessageKind Kind => MessageKind.ECard;

    /// <summary>
    /// Gets the template identifier for the e-card layout.
    /// </summary>
    public required string TemplateId { get; init; }

    /// <summary>
    /// Gets the headline or title text on the e-card.
    /// </summary>
    public required string Title { get; init; }

    /// <summary>
    /// Gets the personalized greeting body text.
    /// </summary>
    public string? Message { get; init; }

    /// <summary>
    /// Gets the background artwork or banner image URL.
    /// </summary>
    public string? ImageUrl { get; init; }

    /// <summary>
    /// Gets the optional collection of interactive call-to-action buttons.
    /// </summary>
    public IReadOnlyList<CardButton>? Buttons { get; init; }
}
