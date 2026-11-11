// Copyright (c) Zenix. All rights reserved.
// Licensed under the proprietary and confidential license.
// Unauthorized copying, distribution, modification, or disclosure of this file,
// via any medium, is strictly prohibited without prior written permission.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Zenix.Core.Zalo.Models.Messages;

namespace Zenix.Core.Zalo.Storage.Repositories;

/// <summary>
/// Represents composer content held across conversation navigation or unsent message failure,
/// including text, mention spans, and optional quoted reply.
/// </summary>
public sealed record ComposerDraft
{
    /// <summary>
    /// Gets the composer text draft.
    /// </summary>
    public string Text { get; init; } = string.Empty;

    /// <summary>
    /// Gets the list of @mention spans with character offsets into <see cref="Text"/>.
    /// </summary>
    public IReadOnlyList<MentionSpan> Mentions { get; init; } = Array.Empty<MentionSpan>();

    /// <summary>
    /// Gets the message being replied to, if any.
    /// </summary>
    public ChatMessage? ReplyTo { get; init; }

    /// <summary>
    /// Merges another draft below this draft separated by a newline, shifting other mention offsets
    /// by the length of the first draft text plus one newline delimiter.
    /// </summary>
    /// <param name="other">The subsequent draft to append.</param>
    /// <returns>A merged draft instance.</returns>
    public ComposerDraft Appending(ComposerDraft other)
    {
        ArgumentNullException.ThrowIfNull(other);

        if (string.IsNullOrEmpty(Text))
        {
            return other with { ReplyTo = ReplyTo ?? other.ReplyTo };
        }

        if (string.IsNullOrEmpty(other.Text))
        {
            return this;
        }

        var shift = Text.Length + 1; // UTF-16 character count + newline
        var mergedMentions = new List<MentionSpan>(Mentions);

        foreach (var m in other.Mentions)
        {
            mergedMentions.Add(new MentionSpan
            {
                UserId = m.UserId,
                Offset = m.Offset + shift,
                Length = m.Length,
                DisplayText = m.DisplayText
            });
        }

        return new ComposerDraft
        {
            Text = Text + "\n" + other.Text,
            Mentions = mergedMentions,
            ReplyTo = ReplyTo ?? other.ReplyTo
        };
    }
}

/// <summary>
/// Thread-safe in-memory and disk draft store tracking unsent drafts, mention offset shifting,
/// and "not sent" notice visibility across conversation switches.
/// </summary>
public sealed class FailedDraftStore
{
    private readonly Dictionary<string, ComposerDraft> _drafts = new(StringComparer.Ordinal);
    private readonly HashSet<string> _silent = new(StringComparer.Ordinal);
    private readonly object _lock = new();

    /// <summary>
    /// Gets the monotonic revision counter incremented on each non-empty draft stash.
    /// </summary>
    public int Revision
    {
        get
        {
            lock (_lock)
            {
                return _revision;
            }
        }
    }

    private int _revision;

    /// <summary>
    /// Determines whether a draft is currently held for the specified conversation.
    /// </summary>
    /// <param name="conversationId">The conversation identifier.</param>
    /// <returns><c>true</c> if a draft is held; otherwise, <c>false</c>.</returns>
    public bool Has(string conversationId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(conversationId);

        lock (_lock)
        {
            return _drafts.ContainsKey(conversationId);
        }
    }

    /// <summary>
    /// Stashes a draft for a conversation. Merges multiple failures in order and shifts mention offsets.
    /// </summary>
    /// <param name="draft">The draft to store.</param>
    /// <param name="conversationId">The target conversation identifier.</param>
    /// <param name="isSilent">If <c>true</c>, returning the draft displays no "not sent" notice.</param>
    public void Stash(ComposerDraft draft, string conversationId, bool isSilent = false)
    {
        ArgumentNullException.ThrowIfNull(draft);
        ArgumentException.ThrowIfNullOrWhiteSpace(conversationId);

        if (string.IsNullOrEmpty(draft.Text))
        {
            return;
        }

        lock (_lock)
        {
            if (!_drafts.ContainsKey(conversationId))
            {
                if (isSilent)
                {
                    _silent.Add(conversationId);
                }
            }
            else if (!isSilent)
            {
                // A silent stash merged with a non-silent one stays normal (displays notice)
                _silent.Remove(conversationId);
            }

            if (_drafts.TryGetValue(conversationId, out var existing))
            {
                _drafts[conversationId] = existing.Appending(draft);
            }
            else
            {
                _drafts[conversationId] = draft;
            }

            _revision++;
        }
    }

    /// <summary>
    /// Retrieves and removes the held draft for a conversation.
    /// </summary>
    /// <param name="conversationId">The conversation identifier.</param>
    /// <returns>The held draft, or <c>null</c> if none was stored.</returns>
    public ComposerDraft? Take(string conversationId)
    {
        return TakeWithNotice(conversationId)?.Draft;
    }

    /// <summary>
    /// Retrieves and removes the held draft and returns whether returning it deserves a "not sent" notice.
    /// </summary>
    /// <param name="conversationId">The conversation identifier.</param>
    /// <returns>A tuple of the draft and notice visibility flag, or <c>null</c> if none was stored.</returns>
    public (ComposerDraft Draft, bool Notice)? TakeWithNotice(string conversationId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(conversationId);

        lock (_lock)
        {
            if (!_drafts.Remove(conversationId, out var draft))
            {
                return null;
            }

            var wasSilent = _silent.Remove(conversationId);
            return (draft, !wasSilent);
        }
    }

    /// <summary>
    /// Empties the store completely and returns all held drafts.
    /// </summary>
    /// <returns>A collection of conversation IDs and their respective drafts.</returns>
    public IReadOnlyList<(string ConversationId, ComposerDraft Draft)> Drain()
    {
        lock (_lock)
        {
            var result = _drafts.Select(kv => (kv.Key, kv.Value)).ToList();
            _drafts.Clear();
            _silent.Clear();
            return result;
        }
    }

    /// <summary>
    /// Persists a conversation draft to the underlying SQLite drafts table.
    /// </summary>
    /// <param name="dal">The active SQLite DAL instance.</param>
    /// <param name="conversationId">The conversation identifier.</param>
    /// <param name="draft">The draft to persist.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task representing the asynchronous persist operation.</returns>
    public static async Task PersistToDiskAsync(
        ZaloSqliteDalService dal,
        string conversationId,
        ComposerDraft draft,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dal);
        ArgumentException.ThrowIfNullOrWhiteSpace(conversationId);
        ArgumentNullException.ThrowIfNull(draft);

        const string sql = @"
            INSERT INTO drafts (conversation_id, draft_text, mentions_json, reply_to_json, updated_at)
            VALUES (@convId, @text, @mentions, @replyTo, @updated)
            ON CONFLICT(conversation_id) DO UPDATE SET
                draft_text = excluded.draft_text,
                mentions_json = excluded.mentions_json,
                reply_to_json = excluded.reply_to_json,
                updated_at = excluded.updated_at;";

        var mentionsJson = draft.Mentions.Count > 0 ? JsonSerializer.Serialize(draft.Mentions) : null;
        var replyToJson = draft.ReplyTo != null ? JsonSerializer.Serialize(draft.ReplyTo) : null;

        var parameters = new Dictionary<string, object?>
        {
            ["convId"] = conversationId,
            ["text"] = draft.Text,
            ["mentions"] = mentionsJson,
            ["replyTo"] = replyToJson,
            ["updated"] = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
        };

        await dal.ExecuteNonQueryAsync(sql, parameters, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Loads a saved draft from the underlying SQLite drafts table, if present.
    /// </summary>
    /// <param name="dal">The active SQLite DAL instance.</param>
    /// <param name="conversationId">The conversation identifier.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The stored draft, or <c>null</c> if none found.</returns>
    public static async Task<ComposerDraft?> LoadFromDiskAsync(
        ZaloSqliteDalService dal,
        string conversationId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dal);
        ArgumentException.ThrowIfNullOrWhiteSpace(conversationId);

        const string sql = @"
            SELECT draft_text, mentions_json, reply_to_json
            FROM drafts
            WHERE conversation_id = @convId
            LIMIT 1;";

        var parameters = new Dictionary<string, object?> { ["convId"] = conversationId };
        var results = await dal.ExecuteQueryAsync(sql, reader =>
        {
            var text = reader.GetString(0);
            IReadOnlyList<MentionSpan> mentions = Array.Empty<MentionSpan>();
            if (!reader.IsDBNull(1))
            {
                mentions = JsonSerializer.Deserialize<List<MentionSpan>>(reader.GetString(1)) ?? (IReadOnlyList<MentionSpan>)Array.Empty<MentionSpan>();
            }

            ChatMessage? replyTo = null;
            if (!reader.IsDBNull(2))
            {
                replyTo = JsonSerializer.Deserialize<ChatMessage>(reader.GetString(2));
            }

            return new ComposerDraft
            {
                Text = text,
                Mentions = mentions,
                ReplyTo = replyTo
            };
        }, parameters, cancellationToken).ConfigureAwait(false);

        return results.FirstOrDefault();
    }
}
