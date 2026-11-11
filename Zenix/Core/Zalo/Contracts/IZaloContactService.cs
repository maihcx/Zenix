// Copyright (c) Zenix. All rights reserved.
// Licensed under the proprietary and confidential license.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Zenix.Core.Zalo.Models.Profiles;

namespace Zenix.Core.Zalo.Contracts;

/// <summary>
/// Defines address book, contact profile, and group administration operations.
/// </summary>
public interface IZaloContactService
{
    /// <summary>
    /// Occurs when a contact profile or address book entry is added or updated.
    /// </summary>
    event EventHandler<UserContact>? ContactUpdated;

    /// <summary>
    /// Occurs when a group profile, member list, or role assignment changes.
    /// </summary>
    event EventHandler<GroupProfile>? GroupUpdated;

    /// <summary>
    /// Synchronizes the user's full friend list and address book contacts from the server into local storage.
    /// </summary>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task representing the asynchronous synchronization operation.</returns>
    Task SyncContactsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves all friends and contacts cached in local storage, grouped and sorted by section header.
    /// </summary>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A list of contacts ordered alphabetically by sort key.</returns>
    Task<IReadOnlyList<UserContact>> GetContactsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Fetches a specific contact profile by user identifier.
    /// </summary>
    /// <param name="contactId">The unique user or contact identifier.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The contact profile if found; otherwise, <c>null</c>.</returns>
    Task<UserContact?> GetContactByIdAsync(string contactId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Fetches all active groups that the current user belongs to.
    /// </summary>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A list of group profiles.</returns>
    Task<IReadOnlyList<GroupProfile>> GetGroupsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Fetches comprehensive group details, including the full roster of group members.
    /// </summary>
    /// <param name="groupId">The group identifier (e.g. "g123456789").</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The group profile if found; otherwise, <c>null</c>.</returns>
    Task<GroupProfile?> GetGroupByIdAsync(string groupId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Searches cached contacts, groups, and message history matching a search query.
    /// </summary>
    /// <param name="query">The search term to match against names, aliases, or phone numbers.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A collection of matching contacts.</returns>
    Task<IReadOnlyList<UserContact>> SearchContactsAsync(string query, CancellationToken cancellationToken = default);
}
