// Copyright (c) Zenix. All rights reserved.
// Licensed under the proprietary and confidential license.
// Unauthorized copying, distribution, modification, or disclosure of this file,
// via any medium, is strictly prohibited without prior written permission.

using Zenix.Core.Zalo.Helpers;
using Zenix.Core.Zalo.Models.Enums;

namespace Zenix.Core.Zalo.Models.Profiles;

/// <summary>
/// Represents a contact or friend in the user's address book.
/// </summary>
public sealed record UserContact
{
    private readonly string? _sortKey;
    private readonly string? _sectionHeader;

    /// <summary>
    /// Gets the unique user identifier for this contact.
    /// </summary>
    public required string UserId { get; init; }

    /// <summary>
    /// Gets the primary display name of the contact.
    /// </summary>
    public required string DisplayName { get; init; }

    /// <summary>
    /// Gets the local custom alias or nickname assigned to this contact by the current user.
    /// </summary>
    public string? LocalCustomName { get; init; }

    /// <summary>
    /// Gets the effective display name, preferring the local custom alias over the global display name.
    /// </summary>
    public string EffectiveName => !string.IsNullOrWhiteSpace(LocalCustomName) ? LocalCustomName : DisplayName;

    /// <summary>
    /// Gets the contact's phone number, if shared.
    /// </summary>
    public string? PhoneNumber { get; init; }

    /// <summary>
    /// Gets the contact's avatar image URL.
    /// </summary>
    public string? AvatarUrl { get; init; }

    /// <summary>
    /// Gets the gender of the contact.
    /// </summary>
    public ZaloGender Gender { get; init; } = ZaloGender.Unknown;

    /// <summary>
    /// Gets a value indicating whether this contact is an established friend.
    /// </summary>
    public bool IsFriend { get; init; } = true;

    /// <summary>
    /// Gets a value indicating whether this contact is blocked.
    /// </summary>
    public bool IsBlocked { get; init; }

    /// <summary>
    /// Gets a value indicating whether notifications from this contact are muted.
    /// </summary>
    public bool IsMuted { get; init; }

    /// <summary>
    /// Gets the normalized alphabetical collation sort key for indexing.
    /// </summary>
    public string SortKey
    {
        get => _sortKey ?? ZaloStringHelper.ComputeSortKey(EffectiveName);
        init => _sortKey = value;
    }

    /// <summary>
    /// Gets the single-character grouping header (e.g., 'A'-'Z' or '#') for grouped contact listings.
    /// </summary>
    public string SectionHeader
    {
        get => _sectionHeader ?? ZaloStringHelper.GetSectionHeader(EffectiveName);
        init => _sectionHeader = value;
    }

    /// <summary>
    /// Gets the Unix epoch timestamp in milliseconds when the contact was last active, or 0 if unknown/hidden.
    /// </summary>
    public long LastSeenTimestamp { get; init; }
}
