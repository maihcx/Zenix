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
/// SQLite-backed repository managing conversation thread metadata, sorting by activity timestamp,
/// and unread message counters.
/// </summary>
public sealed class ConversationRepository : IConversationRepository
{
    private readonly ZaloSqliteDalService _dal;

    /// <summary>
    /// Initializes a new instance of the <see cref="ConversationRepository"/> class.
    /// </summary>
    /// <param name="dal">The active SQLite Data Access Layer instance.</param>
    public ConversationRepository(ZaloSqliteDalService dal)
    {
        ArgumentNullException.ThrowIfNull(dal);
        _dal = dal;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<ConversationSummary>> GetAllConversationsAsync(CancellationToken cancellationToken = default)
    {
        const string sql = @"
            SELECT 
                conversation_id, type, title, avatar_url, snippet, 
                last_sender_name, last_activity_at, unread_count, 
                flags, last_delivery_status, is_muted
            FROM conversations
            ORDER BY last_activity_at DESC;";

        return await _dal.ExecuteQueryAsync(sql, MapConversation, null, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task<ConversationSummary?> GetByIdAsync(string conversationId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(conversationId);

        const string sql = @"
            SELECT 
                conversation_id, type, title, avatar_url, snippet, 
                last_sender_name, last_activity_at, unread_count, 
                flags, last_delivery_status, is_muted
            FROM conversations
            WHERE conversation_id = @id
            LIMIT 1;";

        var parameters = new Dictionary<string, object?> { ["id"] = conversationId };
        var results = await _dal.ExecuteQueryAsync(sql, MapConversation, parameters, cancellationToken).ConfigureAwait(false);
        return results.FirstOrDefault();
    }

    /// <inheritdoc/>
    public async Task UpsertAsync(ConversationSummary conversation, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(conversation);

        const string sql = @"
            INSERT INTO conversations (
                conversation_id, type, title, avatar_url, snippet, 
                last_sender_name, last_activity_at, unread_count, 
                flags, last_delivery_status, is_muted
            ) VALUES (
                @id, @type, @title, @avatar, @snippet, 
                @lastSender, @lastActivity, @unread, 
                @flags, @deliveryStatus, @isMuted
            ) ON CONFLICT(conversation_id) DO UPDATE SET
                type = excluded.type,
                title = excluded.title,
                avatar_url = excluded.avatar_url,
                snippet = excluded.snippet,
                last_sender_name = excluded.last_sender_name,
                last_activity_at = excluded.last_activity_at,
                unread_count = excluded.unread_count,
                flags = excluded.flags,
                last_delivery_status = excluded.last_delivery_status,
                is_muted = excluded.is_muted;";

        var parameters = new Dictionary<string, object?>
        {
            ["id"] = conversation.ConversationId,
            ["type"] = (int)conversation.ConversationType,
            ["title"] = conversation.Title,
            ["avatar"] = conversation.AvatarUrl,
            ["snippet"] = conversation.Snippet,
            ["lastSender"] = conversation.LastMessageSenderName,
            ["lastActivity"] = conversation.LastActivityTimestamp,
            ["unread"] = conversation.UnreadCount,
            ["flags"] = (int)conversation.Flags,
            ["deliveryStatus"] = (int)conversation.LastDeliveryStatus,
            ["isMuted"] = conversation.IsMuted ? 1 : 0
        };

        await _dal.ExecuteNonQueryAsync(sql, parameters, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task UpdateUnreadCountAsync(string conversationId, int unreadCount, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(conversationId);

        const string sql = @"
            UPDATE conversations 
            SET unread_count = @unread 
            WHERE conversation_id = @id;";

        var parameters = new Dictionary<string, object?>
        {
            ["id"] = conversationId,
            ["unread"] = Math.Max(0, unreadCount)
        };

        await _dal.ExecuteNonQueryAsync(sql, parameters, cancellationToken).ConfigureAwait(false);
    }

    private static ConversationSummary MapConversation(SqliteDataReader reader)
    {
        return new ConversationSummary
        {
            ConversationId = reader.GetString(reader.GetOrdinal("conversation_id")),
            ConversationType = (ChatConversationType)reader.GetInt32(reader.GetOrdinal("type")),
            Title = reader.GetString(reader.GetOrdinal("title")),
            AvatarUrl = reader.IsDBNull(reader.GetOrdinal("avatar_url")) ? null : reader.GetString(reader.GetOrdinal("avatar_url")),
            Snippet = reader.IsDBNull(reader.GetOrdinal("snippet")) ? null : reader.GetString(reader.GetOrdinal("snippet")),
            LastMessageSenderName = reader.IsDBNull(reader.GetOrdinal("last_sender_name")) ? null : reader.GetString(reader.GetOrdinal("last_sender_name")),
            LastActivityTimestamp = reader.GetInt64(reader.GetOrdinal("last_activity_at")),
            UnreadCount = reader.GetInt32(reader.GetOrdinal("unread_count")),
            Flags = (ConversationActionFlags)reader.GetInt32(reader.GetOrdinal("flags")),
            LastDeliveryStatus = (MessageDeliveryStatus)reader.GetInt32(reader.GetOrdinal("last_delivery_status")),
            IsMuted = reader.GetInt32(reader.GetOrdinal("is_muted")) != 0
        };
    }
}
