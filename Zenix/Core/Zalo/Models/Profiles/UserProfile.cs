// Copyright (c) Zenix. All rights reserved.
// Licensed under the proprietary and confidential license.
// Unauthorized copying, distribution, modification, or disclosure of this file,
// via any medium, is strictly prohibited without prior written permission.

using System;
using Zenix.Core.Zalo.Models.Enums;

namespace Zenix.Core.Zalo.Models.Profiles;

/// <summary>
/// Represents the profile metadata for an authenticated Zalo user.
/// </summary>
public sealed record UserProfile
{
    /// <summary>
    /// Gets the unique Zalo user identifier.
    /// </summary>
    public required string UserId { get; init; }

    /// <summary>
    /// Gets the public display name of the user.
    /// </summary>
    public required string DisplayName { get; init; }

    /// <summary>
    /// Gets the user's phone number, if available.
    /// </summary>
    public string? PhoneNumber { get; init; }

    /// <summary>
    /// Gets the URL of the user's avatar image.
    /// </summary>
    public string? AvatarUrl { get; init; }

    /// <summary>
    /// Gets the URL of the user's cover banner image.
    /// </summary>
    public string? CoverUrl { get; init; }

    /// <summary>
    /// Gets the user's declared gender.
    /// </summary>
    public ZaloGender Gender { get; init; } = ZaloGender.Unknown;

    /// <summary>
    /// Gets the user's date of birth, if provided.
    /// </summary>
    public DateTimeOffset? DateOfBirth { get; init; }

    /// <summary>
    /// Gets the custom bio or status message set by the user.
    /// </summary>
    public string? StatusMessage { get; init; }

    /// <summary>
    /// Gets a value indicating whether this account is an official or business account.
    /// </summary>
    public bool IsBusiness { get; init; }

    /// <summary>
    /// Gets the Unix epoch timestamp in milliseconds when the profile was last synchronized.
    /// </summary>
    public long LastUpdatedTimestamp { get; init; }
}
