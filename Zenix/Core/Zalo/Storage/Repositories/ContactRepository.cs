// Copyright (c) Zenix. All rights reserved.
// Licensed under the proprietary and confidential license.
// Unauthorized copying, distribution, modification, or disclosure of this file,
// via any medium, is strictly prohibited without prior written permission.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Zenix.Core.Zalo.Contracts;
using Zenix.Core.Zalo.Helpers;
using Zenix.Core.Zalo.Models.Enums;
using Zenix.Core.Zalo.Models.Profiles;

namespace Zenix.Core.Zalo.Storage.Repositories;

/// <summary>
/// SQLite-backed repository managing contact records, alphabetical grouping ('#' and 'A'-'Z'),
/// and Vietnamese diacritic collation.
/// </summary>
public sealed class ContactRepository : IContactRepository
{
    private readonly ZaloSqliteDalService _dal;

    /// <summary>
    /// Initializes a new instance of the <see cref="ContactRepository"/> class.
    /// </summary>
    /// <param name="dal">The active SQLite Data Access Layer instance.</param>
    public ContactRepository(ZaloSqliteDalService dal)
    {
        ArgumentNullException.ThrowIfNull(dal);
        _dal = dal;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<UserContact>> GetAllContactsAsync(CancellationToken cancellationToken = default)
    {
        const string sql = @"
            SELECT 
                contact_id, display_name, local_custom_name, phone_number,
                avatar_url, gender, is_friend, is_blocked, is_muted,
                sort_key, section_header, last_seen_timestamp, updated_at
            FROM contacts
            ORDER BY section_header, sort_key;";

        return await _dal.ExecuteQueryAsync(sql, MapContact, null, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task<UserContact?> GetByIdAsync(string userId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);

        const string sql = @"
            SELECT 
                contact_id, display_name, local_custom_name, phone_number,
                avatar_url, gender, is_friend, is_blocked, is_muted,
                sort_key, section_header, last_seen_timestamp, updated_at
            FROM contacts
            WHERE contact_id = @id
            LIMIT 1;";

        var parameters = new Dictionary<string, object?> { ["id"] = userId };
        var results = await _dal.ExecuteQueryAsync(sql, MapContact, parameters, cancellationToken).ConfigureAwait(false);
        return results.FirstOrDefault();
    }

    /// <inheritdoc/>
    public async Task UpsertContactAsync(UserContact contact, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(contact);

        const string sql = @"
            INSERT INTO contacts (
                contact_id, display_name, local_custom_name, phone_number,
                avatar_url, gender, is_friend, is_blocked, is_muted,
                sort_key, section_header, unaccented_name, last_seen_timestamp, updated_at
            ) VALUES (
                @id, @displayName, @localCustomName, @phoneNumber,
                @avatarUrl, @gender, @isFriend, @isBlocked, @isMuted,
                @sortKey, @sectionHeader, @unaccentedName, @lastSeen, @updated
            ) ON CONFLICT(contact_id) DO UPDATE SET
                display_name = excluded.display_name,
                local_custom_name = excluded.local_custom_name,
                phone_number = excluded.phone_number,
                avatar_url = excluded.avatar_url,
                gender = excluded.gender,
                is_friend = excluded.is_friend,
                is_blocked = excluded.is_blocked,
                is_muted = excluded.is_muted,
                sort_key = excluded.sort_key,
                section_header = excluded.section_header,
                unaccented_name = excluded.unaccented_name,
                last_seen_timestamp = excluded.last_seen_timestamp,
                updated_at = excluded.updated_at;";

        var parameters = BuildParameters(contact);
        await _dal.ExecuteNonQueryAsync(sql, parameters, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task UpsertBatchAsync(IEnumerable<UserContact> contacts, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(contacts);

        const string sql = @"
            INSERT INTO contacts (
                contact_id, display_name, local_custom_name, phone_number,
                avatar_url, gender, is_friend, is_blocked, is_muted,
                sort_key, section_header, unaccented_name, last_seen_timestamp, updated_at
            ) VALUES (
                @id, @displayName, @localCustomName, @phoneNumber,
                @avatarUrl, @gender, @isFriend, @isBlocked, @isMuted,
                @sortKey, @sectionHeader, @unaccentedName, @lastSeen, @updated
            ) ON CONFLICT(contact_id) DO UPDATE SET
                display_name = excluded.display_name,
                local_custom_name = excluded.local_custom_name,
                phone_number = excluded.phone_number,
                avatar_url = excluded.avatar_url,
                gender = excluded.gender,
                is_friend = excluded.is_friend,
                is_blocked = excluded.is_blocked,
                is_muted = excluded.is_muted,
                sort_key = excluded.sort_key,
                section_header = excluded.section_header,
                unaccented_name = excluded.unaccented_name,
                last_seen_timestamp = excluded.last_seen_timestamp,
                updated_at = excluded.updated_at;";

        await _dal.ExecuteInTransactionAsync(async (connection, transaction) =>
        {
            await using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = sql;

            foreach (var contact in contacts)
            {
                command.Parameters.Clear();
                var p = BuildParameters(contact);
                foreach (var (k, v) in p)
                {
                    command.Parameters.AddWithValue("@" + k, v ?? DBNull.Value);
                }

                await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }
        }, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<UserContact>> SearchAsync(string query, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return await GetAllContactsAsync(cancellationToken).ConfigureAwait(false);
        }

        var normalizedQuery = "%" + query.Trim() + "%";
        var foldedQuery = "%" + ZaloStringHelper.FoldDiacritics(query.Trim()) + "%";

        const string sql = @"
            SELECT 
                contact_id, display_name, local_custom_name, phone_number,
                avatar_url, gender, is_friend, is_blocked, is_muted,
                sort_key, section_header, last_seen_timestamp, updated_at
            FROM contacts
            WHERE display_name LIKE @pattern 
               OR local_custom_name LIKE @pattern
               OR phone_number LIKE @pattern
               OR sort_key LIKE @foldedPattern
               OR unaccented_name LIKE @foldedPattern
            ORDER BY section_header, sort_key;";

        var parameters = new Dictionary<string, object?>
        {
            ["pattern"] = normalizedQuery,
            ["foldedPattern"] = foldedQuery
        };

        return await _dal.ExecuteQueryAsync(sql, MapContact, parameters, cancellationToken).ConfigureAwait(false);
    }

    private static Dictionary<string, object?> BuildParameters(UserContact contact)
    {
        return new Dictionary<string, object?>
        {
            ["id"] = contact.UserId,
            ["displayName"] = contact.DisplayName,
            ["localCustomName"] = contact.LocalCustomName,
            ["phoneNumber"] = contact.PhoneNumber,
            ["avatarUrl"] = contact.AvatarUrl,
            ["gender"] = (int)contact.Gender,
            ["isFriend"] = contact.IsFriend ? 1 : 0,
            ["isBlocked"] = contact.IsBlocked ? 1 : 0,
            ["isMuted"] = contact.IsMuted ? 1 : 0,
            ["sortKey"] = contact.SortKey,
            ["sectionHeader"] = contact.SectionHeader,
            ["unaccentedName"] = ZaloStringHelper.FoldDiacritics(contact.EffectiveName),
            ["lastSeen"] = contact.LastSeenTimestamp,
            ["updated"] = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
        };
    }

    private static UserContact MapContact(SqliteDataReader reader)
    {
        return new UserContact
        {
            UserId = reader.GetString(reader.GetOrdinal("contact_id")),
            DisplayName = reader.GetString(reader.GetOrdinal("display_name")),
            LocalCustomName = reader.IsDBNull(reader.GetOrdinal("local_custom_name")) ? null : reader.GetString(reader.GetOrdinal("local_custom_name")),
            PhoneNumber = reader.IsDBNull(reader.GetOrdinal("phone_number")) ? null : reader.GetString(reader.GetOrdinal("phone_number")),
            AvatarUrl = reader.IsDBNull(reader.GetOrdinal("avatar_url")) ? null : reader.GetString(reader.GetOrdinal("avatar_url")),
            Gender = (ZaloGender)reader.GetInt32(reader.GetOrdinal("gender")),
            IsFriend = reader.GetInt32(reader.GetOrdinal("is_friend")) != 0,
            IsBlocked = reader.GetInt32(reader.GetOrdinal("is_blocked")) != 0,
            IsMuted = reader.GetInt32(reader.GetOrdinal("is_muted")) != 0,
            SortKey = reader.GetString(reader.GetOrdinal("sort_key")),
            SectionHeader = reader.GetString(reader.GetOrdinal("section_header")),
            LastSeenTimestamp = reader.GetInt64(reader.GetOrdinal("last_seen_timestamp"))
        };
    }
}
