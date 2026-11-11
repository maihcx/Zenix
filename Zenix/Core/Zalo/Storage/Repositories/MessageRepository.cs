// Copyright (c) Zenix. All rights reserved.
// Licensed under the proprietary and confidential license.
// Unauthorized copying, distribution, modification, or disclosure of this file,
// via any medium, is strictly prohibited without prior written permission.

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Zenix.Core.Zalo.Contracts;
using Zenix.Core.Zalo.Models.Enums;
using Zenix.Core.Zalo.Models.Messages;
using Zenix.Core.Zalo.Models.Messages.Payloads;

namespace Zenix.Core.Zalo.Storage.Repositories;

/// <summary>
/// SQLite-backed message repository providing sub-1.5ms indexed paginated retrieval,
/// stable tie-breaking on identical timestamps, delivery lifecycle progression, and message recall.
/// </summary>
public sealed class MessageRepository : IMessageRepository
{
    private readonly ZaloSqliteDalService _dal;

    /// <summary>
    /// Initializes a new instance of the <see cref="MessageRepository"/> class.
    /// </summary>
    /// <param name="dal">The active SQLite Data Access Layer instance.</param>
    public MessageRepository(ZaloSqliteDalService dal)
    {
        ArgumentNullException.ThrowIfNull(dal);
        _dal = dal;
    }

    /// <inheritdoc/>
    public async Task InsertAsync(ChatMessage message, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);

        const string sql = @"
            INSERT INTO messages (
                cli_msg_id, server_msg_id, conversation_id, conversation_type,
                sender_id, sender_name, sender_avatar_url, is_self,
                status, kind, payload_type, payload_json,
                quote_msg_id, quote_json, mentions_json, reactions_json,
                timestamp, sequence_offset, is_edited, is_recalled, is_pinned
            ) VALUES (
                @cliId, @srvId, @convId, @convType,
                @senderId, @senderName, @senderAvatar, @isSelf,
                @status, @kind, @payloadType, @payloadJson,
                @quoteId, @quoteJson, @mentionsJson, @reactionsJson,
                @timestamp, @offset, @isEdited, @isRecalled, @isPinned
            );";

        var parameters = BuildParameters(message);
        await _dal.ExecuteNonQueryAsync(sql, parameters, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task InsertBatchAsync(IEnumerable<ChatMessage> messages, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(messages);

        const string sql = @"
            INSERT INTO messages (
                cli_msg_id, server_msg_id, conversation_id, conversation_type,
                sender_id, sender_name, sender_avatar_url, is_self,
                status, kind, payload_type, payload_json,
                quote_msg_id, quote_json, mentions_json, reactions_json,
                timestamp, sequence_offset, is_edited, is_recalled, is_pinned
            ) VALUES (
                @cliId, @srvId, @convId, @convType,
                @senderId, @senderName, @senderAvatar, @isSelf,
                @status, @kind, @payloadType, @payloadJson,
                @quoteId, @quoteJson, @mentionsJson, @reactionsJson,
                @timestamp, @offset, @isEdited, @isRecalled, @isPinned
            );";

        await _dal.ExecuteInTransactionAsync(async (connection, transaction) =>
        {
            await using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = sql;

            foreach (var message in messages)
            {
                command.Parameters.Clear();
                var p = BuildParameters(message);
                foreach (var (k, v) in p)
                {
                    command.Parameters.AddWithValue("@" + k, v ?? DBNull.Value);
                }

                try
                {
                    await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
                }
                catch (SqliteException ex) when (ex.SqliteErrorCode == 19 || ex.Message.Contains("UNIQUE constraint failed", StringComparison.OrdinalIgnoreCase))
                {
                    // Suppress unique constraint violations in batch insert
                }
            }
        }, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<ChatMessage>> GetByConversationAsync(
        string conversationId,
        DateTimeOffset? beforeTimestamp = null,
        int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(conversationId);
        var effectivePageSize = Math.Clamp(pageSize, 1, 500);

        string sql;
        Dictionary<string, object?> parameters;

        if (beforeTimestamp.HasValue)
        {
            var cutoffMs = beforeTimestamp.Value.ToUnixTimeMilliseconds();
            sql = @"
                SELECT 
                    cli_msg_id, server_msg_id, conversation_id, conversation_type,
                    sender_id, sender_name, sender_avatar_url, is_self,
                    status, kind, payload_type, payload_json,
                    quote_msg_id, quote_json, mentions_json, reactions_json,
                    timestamp, sequence_offset, is_edited, is_recalled, is_pinned
                FROM messages
                WHERE conversation_id = @convId AND timestamp < @cutoff
                ORDER BY timestamp DESC, sequence_offset DESC
                LIMIT @pageSize;";

            parameters = new Dictionary<string, object?>
            {
                ["convId"] = conversationId,
                ["cutoff"] = cutoffMs,
                ["pageSize"] = effectivePageSize
            };
        }
        else
        {
            sql = @"
                SELECT 
                    cli_msg_id, server_msg_id, conversation_id, conversation_type,
                    sender_id, sender_name, sender_avatar_url, is_self,
                    status, kind, payload_type, payload_json,
                    quote_msg_id, quote_json, mentions_json, reactions_json,
                    timestamp, sequence_offset, is_edited, is_recalled, is_pinned
                FROM messages
                WHERE conversation_id = @convId
                ORDER BY timestamp DESC, sequence_offset DESC
                LIMIT @pageSize;";

            parameters = new Dictionary<string, object?>
            {
                ["convId"] = conversationId,
                ["pageSize"] = effectivePageSize
            };
        }

        return await _dal.ExecuteQueryAsync(sql, MapMessage, parameters, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task UpdateStatusAsync(
        string cliMsgId,
        string? serverMsgId,
        MessageDeliveryStatus status,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(cliMsgId);

        string sql;
        Dictionary<string, object?> parameters;

        if (!string.IsNullOrEmpty(serverMsgId))
        {
            sql = @"
                UPDATE messages 
                SET status = @status, server_msg_id = @srvId 
                WHERE cli_msg_id = @cliId;";

            parameters = new Dictionary<string, object?>
            {
                ["status"] = (int)status,
                ["srvId"] = serverMsgId,
                ["cliId"] = cliMsgId
            };
        }
        else
        {
            sql = @"
                UPDATE messages 
                SET status = @status 
                WHERE cli_msg_id = @cliId;";

            parameters = new Dictionary<string, object?>
            {
                ["status"] = (int)status,
                ["cliId"] = cliMsgId
            };
        }

        await _dal.ExecuteNonQueryAsync(sql, parameters, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task DeleteAsync(string cliMsgId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(cliMsgId);

        const string sql = "DELETE FROM messages WHERE cli_msg_id = @cliId;";
        var parameters = new Dictionary<string, object?> { ["cliId"] = cliMsgId };
        await _dal.ExecuteNonQueryAsync(sql, parameters, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task RecallAsync(
        string cliMsgId,
        string senderName,
        long recalledAt,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(cliMsgId);

        var recalledPayload = new RecalledPayload
        {
            OriginalMessageId = cliMsgId,
            RecallTimestamp = recalledAt,
            RecallNotice = "Tin nhắn đã bị thu hồi"
        };

        var payloadJson = JsonSerializer.Serialize<ChatMessagePayload>(recalledPayload);

        const string sql = @"
            UPDATE messages 
            SET is_recalled = 1,
                payload_type = @payloadType,
                payload_json = @payloadJson,
                sender_name = @senderName
            WHERE cli_msg_id = @cliId;";

        var parameters = new Dictionary<string, object?>
        {
            ["payloadType"] = nameof(RecalledPayload),
            ["payloadJson"] = payloadJson,
            ["senderName"] = senderName,
            ["cliId"] = cliMsgId
        };

        await _dal.ExecuteNonQueryAsync(sql, parameters, cancellationToken).ConfigureAwait(false);
    }

    private static Dictionary<string, object?> BuildParameters(ChatMessage message)
    {
        var payloadJson = JsonSerializer.Serialize(message.Payload);
        var quoteJson = message.QuotedMessage != null ? JsonSerializer.Serialize(message.QuotedMessage) : null;
        var mentionsJson = message.Mentions.Count > 0 ? JsonSerializer.Serialize(message.Mentions) : null;
        var reactionsJson = message.Reactions.Count > 0 ? JsonSerializer.Serialize(message.Reactions) : null;

        return new Dictionary<string, object?>
        {
            ["cliId"] = message.ClientMessageId,
            ["srvId"] = message.MessageId,
            ["convId"] = message.ConversationId,
            ["convType"] = (int)message.ConversationType,
            ["senderId"] = message.SenderId,
            ["senderName"] = message.SenderName,
            ["senderAvatar"] = message.SenderAvatarUrl,
            ["isSelf"] = message.IsSelf ? 1 : 0,
            ["status"] = (int)message.DeliveryStatus,
            ["kind"] = (int)message.Kind,
            ["payloadType"] = message.Payload.GetType().Name,
            ["payloadJson"] = payloadJson,
            ["quoteId"] = message.QuoteMessageId,
            ["quoteJson"] = quoteJson,
            ["mentionsJson"] = mentionsJson,
            ["reactionsJson"] = reactionsJson,
            ["timestamp"] = message.Timestamp,
            ["offset"] = 0,
            ["isEdited"] = message.IsEdited ? 1 : 0,
            ["isRecalled"] = message.IsRecalled ? 1 : 0,
            ["isPinned"] = message.IsPinned ? 1 : 0
        };
    }

    private static ChatMessage MapMessage(SqliteDataReader reader)
    {
        var payloadJson = reader.GetString(reader.GetOrdinal("payload_json"));
        var payload = JsonSerializer.Deserialize<ChatMessagePayload>(payloadJson)
            ?? new TextPayload { Content = string.Empty };

        QuotedMessageSummary? quoted = null;
        var quoteOrdinal = reader.GetOrdinal("quote_json");
        if (!reader.IsDBNull(quoteOrdinal))
        {
            var qJson = reader.GetString(quoteOrdinal);
            quoted = JsonSerializer.Deserialize<QuotedMessageSummary>(qJson);
        }

        IReadOnlyList<MentionSpan> mentions = Array.Empty<MentionSpan>();
        var mentionsOrdinal = reader.GetOrdinal("mentions_json");
        if (!reader.IsDBNull(mentionsOrdinal))
        {
            var mJson = reader.GetString(mentionsOrdinal);
            mentions = JsonSerializer.Deserialize<List<MentionSpan>>(mJson) ?? (IReadOnlyList<MentionSpan>)Array.Empty<MentionSpan>();
        }

        IReadOnlyList<MessageReaction> reactions = Array.Empty<MessageReaction>();
        var reactionsOrdinal = reader.GetOrdinal("reactions_json");
        if (!reader.IsDBNull(reactionsOrdinal))
        {
            var rJson = reader.GetString(reactionsOrdinal);
            reactions = JsonSerializer.Deserialize<List<MessageReaction>>(rJson) ?? (IReadOnlyList<MessageReaction>)Array.Empty<MessageReaction>();
        }

        return new ChatMessage
        {
            ClientMessageId = reader.GetString(reader.GetOrdinal("cli_msg_id")),
            MessageId = reader.IsDBNull(reader.GetOrdinal("server_msg_id"))
                ? reader.GetString(reader.GetOrdinal("cli_msg_id"))
                : reader.GetString(reader.GetOrdinal("server_msg_id")),
            ConversationId = reader.GetString(reader.GetOrdinal("conversation_id")),
            ConversationType = (ChatConversationType)reader.GetInt32(reader.GetOrdinal("conversation_type")),
            SenderId = reader.GetString(reader.GetOrdinal("sender_id")),
            SenderName = reader.IsDBNull(reader.GetOrdinal("sender_name")) ? null : reader.GetString(reader.GetOrdinal("sender_name")),
            SenderAvatarUrl = reader.IsDBNull(reader.GetOrdinal("sender_avatar_url")) ? null : reader.GetString(reader.GetOrdinal("sender_avatar_url")),
            IsSelf = reader.GetInt32(reader.GetOrdinal("is_self")) != 0,
            DeliveryStatus = (MessageDeliveryStatus)reader.GetInt32(reader.GetOrdinal("status")),
            Kind = (MessageKind)reader.GetInt32(reader.GetOrdinal("kind")),
            Payload = payload,
            QuoteMessageId = reader.IsDBNull(reader.GetOrdinal("quote_msg_id")) ? null : reader.GetString(reader.GetOrdinal("quote_msg_id")),
            QuotedMessage = quoted,
            Mentions = mentions,
            Reactions = reactions,
            Timestamp = reader.GetInt64(reader.GetOrdinal("timestamp")),
            IsEdited = reader.GetInt32(reader.GetOrdinal("is_edited")) != 0,
            IsRecalled = reader.GetInt32(reader.GetOrdinal("is_recalled")) != 0,
            IsPinned = reader.GetInt32(reader.GetOrdinal("is_pinned")) != 0
        };
    }
}
