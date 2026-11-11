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
using Zenix.Core.Zalo.Models.Enums;
using Zenix.Core.Zalo.Models.Profiles;

namespace Zenix.Core.Zalo.Storage.Repositories;

/// <summary>
/// SQLite-backed repository managing group chat profiles, settings, and member rosters with role administration.
/// </summary>
public sealed class GroupRepository : IGroupRepository
{
    private readonly ZaloSqliteDalService _dal;

    /// <summary>
    /// Initializes a new instance of the <see cref="GroupRepository"/> class.
    /// </summary>
    /// <param name="dal">The active SQLite Data Access Layer instance.</param>
    public GroupRepository(ZaloSqliteDalService dal)
    {
        ArgumentNullException.ThrowIfNull(dal);
        _dal = dal;
    }

    /// <inheritdoc/>
    public async Task<GroupProfile?> GetGroupByIdAsync(string groupId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(groupId);

        const string sql = @"
            SELECT 
                group_id, name, avatar_url, creator_id, member_count,
                is_e2ee, is_muted, is_disappearing, disappearing_duration_seconds,
                created_timestamp, description, pinned_msg_id, updated_at
            FROM group_profiles
            WHERE group_id = @id
            LIMIT 1;";

        var parameters = new Dictionary<string, object?> { ["id"] = groupId };
        var results = await _dal.ExecuteQueryAsync(sql, MapGroupProfile, parameters, cancellationToken).ConfigureAwait(false);
        return results.FirstOrDefault();
    }

    /// <inheritdoc/>
    public async Task UpsertGroupAsync(GroupProfile group, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(group);

        const string sql = @"
            INSERT INTO group_profiles (
                group_id, name, avatar_url, creator_id, member_count,
                is_e2ee, is_muted, is_disappearing, disappearing_duration_seconds,
                created_timestamp, description, updated_at
            ) VALUES (
                @id, @name, @avatarUrl, @creatorId, @memberCount,
                @isE2ee, @isMuted, @isDisappearing, @disappearingDuration,
                @createdTimestamp, @description, @updated
            ) ON CONFLICT(group_id) DO UPDATE SET
                name = excluded.name,
                avatar_url = excluded.avatar_url,
                creator_id = excluded.creator_id,
                member_count = excluded.member_count,
                is_e2ee = excluded.is_e2ee,
                is_muted = excluded.is_muted,
                is_disappearing = excluded.is_disappearing,
                disappearing_duration_seconds = excluded.disappearing_duration_seconds,
                created_timestamp = excluded.created_timestamp,
                description = excluded.description,
                updated_at = excluded.updated_at;";

        var parameters = new Dictionary<string, object?>
        {
            ["id"] = group.GroupId,
            ["name"] = group.Name,
            ["avatarUrl"] = group.AvatarUrl,
            ["creatorId"] = group.CreatorId,
            ["memberCount"] = group.MemberCount,
            ["isE2ee"] = group.IsE2ee ? 1 : 0,
            ["isMuted"] = group.IsMuted ? 1 : 0,
            ["isDisappearing"] = group.IsDisappearing ? 1 : 0,
            ["disappearingDuration"] = group.DisappearingDurationSeconds,
            ["createdTimestamp"] = group.CreatedTimestamp,
            ["description"] = group.Description,
            ["updated"] = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
        };

        await _dal.ExecuteNonQueryAsync(sql, parameters, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<GroupMember>> GetMembersAsync(string groupId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(groupId);

        const string sql = @"
            SELECT group_id, user_id, display_name, avatar_url, role, joined_at, is_muted
            FROM group_members
            WHERE group_id = @groupId
            ORDER BY role DESC, joined_at ASC;";

        var parameters = new Dictionary<string, object?> { ["groupId"] = groupId };
        return await _dal.ExecuteQueryAsync(sql, MapGroupMember, parameters, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task UpsertMemberAsync(GroupMember member, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(member);

        const string sql = @"
            INSERT INTO group_members (
                group_id, user_id, display_name, avatar_url, role, joined_at, is_muted
            ) VALUES (
                @groupId, @userId, @displayName, @avatarUrl, @role, @joinedAt, @isMuted
            ) ON CONFLICT(group_id, user_id) DO UPDATE SET
                display_name = excluded.display_name,
                avatar_url = excluded.avatar_url,
                role = excluded.role,
                joined_at = excluded.joined_at,
                is_muted = excluded.is_muted;";

        var parameters = BuildMemberParameters(member);
        await _dal.ExecuteNonQueryAsync(sql, parameters, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task UpsertMembersBatchAsync(
        string groupId,
        IEnumerable<GroupMember> members,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(groupId);
        ArgumentNullException.ThrowIfNull(members);

        const string sql = @"
            INSERT INTO group_members (
                group_id, user_id, display_name, avatar_url, role, joined_at, is_muted
            ) VALUES (
                @groupId, @userId, @displayName, @avatarUrl, @role, @joinedAt, @isMuted
            ) ON CONFLICT(group_id, user_id) DO UPDATE SET
                display_name = excluded.display_name,
                avatar_url = excluded.avatar_url,
                role = excluded.role,
                joined_at = excluded.joined_at,
                is_muted = excluded.is_muted;";

        await _dal.ExecuteInTransactionAsync(async (connection, transaction) =>
        {
            await using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = sql;

            foreach (var member in members)
            {
                command.Parameters.Clear();
                var p = BuildMemberParameters(member);
                foreach (var (k, v) in p)
                {
                    command.Parameters.AddWithValue("@" + k, v ?? DBNull.Value);
                }

                await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }
        }, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task RemoveMemberAsync(string groupId, string userId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(groupId);
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);

        const string sql = @"
            DELETE FROM group_members 
            WHERE group_id = @groupId AND user_id = @userId;";

        var parameters = new Dictionary<string, object?>
        {
            ["groupId"] = groupId,
            ["userId"] = userId
        };

        await _dal.ExecuteNonQueryAsync(sql, parameters, cancellationToken).ConfigureAwait(false);
    }

    private static Dictionary<string, object?> BuildMemberParameters(GroupMember member)
    {
        return new Dictionary<string, object?>
        {
            ["groupId"] = member.GroupId,
            ["userId"] = member.UserId,
            ["displayName"] = member.DisplayName,
            ["avatarUrl"] = member.AvatarUrl,
            ["role"] = (int)member.Role,
            ["joinedAt"] = member.JoinedTimestamp,
            ["isMuted"] = member.IsMuted ? 1 : 0
        };
    }

    private static GroupProfile MapGroupProfile(SqliteDataReader reader)
    {
        return new GroupProfile
        {
            GroupId = reader.GetString(reader.GetOrdinal("group_id")),
            Name = reader.GetString(reader.GetOrdinal("name")),
            AvatarUrl = reader.IsDBNull(reader.GetOrdinal("avatar_url")) ? null : reader.GetString(reader.GetOrdinal("avatar_url")),
            CreatorId = reader.GetString(reader.GetOrdinal("creator_id")),
            MemberCount = reader.GetInt32(reader.GetOrdinal("member_count")),
            IsE2ee = reader.GetInt32(reader.GetOrdinal("is_e2ee")) != 0,
            IsMuted = reader.GetInt32(reader.GetOrdinal("is_muted")) != 0,
            IsDisappearing = reader.GetInt32(reader.GetOrdinal("is_disappearing")) != 0,
            DisappearingDurationSeconds = reader.GetInt32(reader.GetOrdinal("disappearing_duration_seconds")),
            CreatedTimestamp = reader.GetInt64(reader.GetOrdinal("created_timestamp")),
            Description = reader.IsDBNull(reader.GetOrdinal("description")) ? null : reader.GetString(reader.GetOrdinal("description"))
        };
    }

    private static GroupMember MapGroupMember(SqliteDataReader reader)
    {
        return new GroupMember
        {
            GroupId = reader.GetString(reader.GetOrdinal("group_id")),
            UserId = reader.GetString(reader.GetOrdinal("user_id")),
            DisplayName = reader.GetString(reader.GetOrdinal("display_name")),
            AvatarUrl = reader.IsDBNull(reader.GetOrdinal("avatar_url")) ? null : reader.GetString(reader.GetOrdinal("avatar_url")),
            Role = (GroupMemberRole)reader.GetInt32(reader.GetOrdinal("role")),
            JoinedTimestamp = reader.GetInt64(reader.GetOrdinal("joined_at")),
            IsMuted = reader.GetInt32(reader.GetOrdinal("is_muted")) != 0
        };
    }
}
