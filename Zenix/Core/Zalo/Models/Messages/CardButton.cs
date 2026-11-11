// Copyright (c) Zenix. All rights reserved.
// Licensed under the proprietary and confidential license.
// Unauthorized copying, distribution, modification, or disclosure of this file,
// via any medium, is strictly prohibited without prior written permission.

namespace Zenix.Core.Zalo.Models.Messages;

/// <summary>
/// Represents an interactive call-to-action button within an e-card or interactive card payload.
/// </summary>
public sealed record CardButton
{
    /// <summary>
    /// Gets the unique identifier for this action button.
    /// </summary>
    public required string ButtonId { get; init; }

    /// <summary>
    /// Gets the user-facing label text displayed on the button.
    /// </summary>
    public required string Label { get; init; }

    /// <summary>
    /// Gets the destination URL or dispatch action payload.
    /// </summary>
    public required string ActionUrl { get; init; }

    /// <summary>
    /// Gets the action type identifier (e.g., "open_url", "quick_reply").
    /// </summary>
    public string ActionType { get; init; } = "open_url";
}
