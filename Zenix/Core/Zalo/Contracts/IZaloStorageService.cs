// Copyright (c) Zenix. All rights reserved.
// Licensed under the proprietary and confidential license.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Zenix.Core.Zalo.Models.Enums;
using Zenix.Core.Zalo.Models.Messages;
using Zenix.Core.Zalo.Models.Profiles;

namespace Zenix.Core.Zalo.Contracts;

/// <summary>
/// Repository interface for conversation threads in the local SQLite database.
/// </summary>
public interface IConversationRepository
{
    /// <summary>
    /// Retrieves all conversations ordered by last activity timestamp descending.
    /// </summary>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A list of conversation summaries.</returns>
    Task<IReadOnlyList<ConversationSummary>> GetAllConversationsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves a single conversation by its identifier.
    /// </summary>
    /// <param name="conversationId">The unique conversation identifier.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The conversation summary, or <c>null</c> if not found.</returns>
    Task<ConversationSummary?> GetByIdAsync(string conversationId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Inserts or updates a conversation record in local storage.
    /// </summary>
    /// <param name="conversation">The conversation entity to persist.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task representing the asynchronous upsert operation.</returns>
    Task UpsertAsync(ConversationSummary conversation, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates the unread message counter for a conversation.
    /// </summary>
    /// <param name="conversationId">The conversation identifier.</param>
    /// <param name="unreadCount">The new unread count (0 for read).</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task representing the asynchronous update operation.</returns>
    Task UpdateUnreadCountAsync(string conversationId, int unreadCount, CancellationToken cancellationToken = default);
}

/// <summary>
/// Repository interface for chat messages in the local SQLite database.
/// </summary>
public interface IMessageRepository
{
    /// <summary>
    /// Inserts a chat message into local storage with constraint tolerance for duplicates.
    /// </summary>
    /// <param name="message">The chat message entity to persist.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task representing the asynchronous insert operation.</returns>
    Task InsertAsync(ChatMessage message, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves paginated messages for a conversation ordered chronologically.
    /// </summary>
    /// <param name="conversationId">The conversation identifier.</param>
    /// <param name="beforeTimestamp">Load messages before this timestamp, or null for newest.</param>
    /// <param name="pageSize">The maximum count of messages to return.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A list of chat messages.</returns>
    Task<IReadOnlyList<ChatMessage>> GetByConversationAsync(
        string conversationId, 
        DateTimeOffset? beforeTimestamp = null, 
        int pageSize = 50, 
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates the delivery status of a local message.
    /// </summary>
    /// <param name="cliMsgId">The local client message identifier.</param>
    /// <param name="serverMsgId">The optional server-assigned message identifier.</param>
    /// <param name="status">The updated delivery status.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task representing the asynchronous update operation.</returns>
    Task UpdateStatusAsync(
        string cliMsgId, 
        string? serverMsgId, 
        MessageDeliveryStatus status, 
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Permanently deletes a message by client message identifier.
    /// </summary>
    /// <param name="cliMsgId">The local client message identifier to delete.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task representing the asynchronous delete operation.</returns>
    Task DeleteAsync(string cliMsgId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Marks a message as recalled, replacing its payload with <see cref="RecalledPayload"/> and preserving its slot.
    /// </summary>
    /// <param name="cliMsgId">The client message identifier.</param>
    /// <param name="senderName">The sender display name.</param>
    /// <param name="recalledAt">The timestamp in milliseconds when recalled.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task representing the asynchronous recall operation.</returns>
    Task RecallAsync(string cliMsgId, string senderName, long recalledAt, CancellationToken cancellationToken = default);

    /// <summary>
    /// Inserts a batch of chat messages within a single transaction.
    /// </summary>
    /// <param name="messages">The chat messages to persist.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task representing the asynchronous batch insert operation.</returns>
    Task InsertBatchAsync(IEnumerable<ChatMessage> messages, CancellationToken cancellationToken = default);
}

/// <summary>
/// Repository interface for contacts in the local SQLite database.
/// </summary>
public interface IContactRepository
{
    /// <summary>
    /// Retrieves all contacts ordered by section header and sort key.
    /// </summary>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A list of cached user contacts.</returns>
    Task<IReadOnlyList<UserContact>> GetAllContactsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Persists a batch of contact entries into local storage within a single database transaction.
    /// </summary>
    /// <param name="contacts">The contacts to insert or update.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task representing the asynchronous batch upsert operation.</returns>
    Task UpsertBatchAsync(IEnumerable<UserContact> contacts, CancellationToken cancellationToken = default);
    /// <summary>
    /// Inserts or updates a single contact in local storage.
    /// </summary>
    /// <param name="contact">The contact entity to persist.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task representing the asynchronous upsert operation.</returns>
    Task UpsertContactAsync(UserContact contact, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves a contact by their unique user identifier.
    /// </summary>
    /// <param name="userId">The user identifier.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The contact entity, or <c>null</c> if not found.</returns>
    Task<UserContact?> GetByIdAsync(string userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Searches cached contacts by display name, local custom alias, or phone number.
    /// </summary>
    /// <param name="query">The search term query.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A list of matching contacts.</returns>
    Task<IReadOnlyList<UserContact>> SearchAsync(string query, CancellationToken cancellationToken = default);
}

/// <summary>
/// Repository interface for group profiles and group members in the local SQLite database.
/// </summary>
public interface IGroupRepository
{
    /// <summary>
    /// Retrieves a group profile by its unique group identifier.
    /// </summary>
    /// <param name="groupId">The group identifier.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The group profile, or <c>null</c> if not found.</returns>
    Task<GroupProfile?> GetGroupByIdAsync(string groupId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Inserts or updates a group profile in local storage.
    /// </summary>
    /// <param name="group">The group profile entity to persist.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task representing the asynchronous upsert operation.</returns>
    Task UpsertGroupAsync(GroupProfile group, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves all members belonging to a group.
    /// </summary>
    /// <param name="groupId">The group identifier.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A list of group members.</returns>
    Task<IReadOnlyList<GroupMember>> GetMembersAsync(string groupId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Inserts or updates a group member record in local storage.
    /// </summary>
    /// <param name="member">The member entity to persist.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task representing the asynchronous upsert operation.</returns>
    Task UpsertMemberAsync(GroupMember member, CancellationToken cancellationToken = default);

    /// <summary>
    /// Persists a batch of group member records into local storage within a single database transaction.
    /// </summary>
    /// <param name="groupId">The group identifier.</param>
    /// <param name="members">The collection of members to persist.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task representing the asynchronous batch upsert operation.</returns>
    Task UpsertMembersBatchAsync(string groupId, IEnumerable<GroupMember> members, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes a member from a group conversation.
    /// </summary>
    /// <param name="groupId">The group identifier.</param>
    /// <param name="userId">The user identifier of the member to remove.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task representing the asynchronous delete operation.</returns>
    Task RemoveMemberAsync(string groupId, string userId, CancellationToken cancellationToken = default);
}

/// <summary>
/// Defines the master SQLite DAL 4.0 storage service managing partitioned local database files,
/// connection lifecycles, WAL mode pragmas, and schema migrations.
/// </summary>
public interface IZaloStorageService : IAsyncDisposable
{
    /// <summary>
    /// Gets the conversation repository.
    /// </summary>
    IConversationRepository Conversations { get; }

    /// <summary>
    /// Gets the message repository.
    /// </summary>
    IMessageRepository Messages { get; }

    /// <summary>
    /// Gets the contact repository.
    /// </summary>
    IContactRepository Contacts { get; }

    /// <summary>
    /// Gets the group repository.
    /// </summary>
    IGroupRepository Groups { get; }

    /// <summary>
    /// Initializes the database connection pool for the specified user account, applying WAL pragmas
    /// and running schema migration scripts if <c>PRAGMA user_version</c> is below target.
    /// </summary>
    /// <param name="accountId">The authenticated user's account identifier.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task representing the asynchronous initialization operation.</returns>
    Task InitializeAccountDatabaseAsync(string accountId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Flushes write queues and safely closes all active SQLite connections for the active account.
    /// </summary>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task representing the asynchronous close operation.</returns>
    Task CloseDatabaseAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Safely purges and deletes all local SQLite database files (including WAL and SHM sidecars) for the specified account.
    /// </summary>
    /// <param name="accountId">The account identifier to wipe.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task representing the asynchronous wipe operation.</returns>
    Task WipeAccountDataAsync(string accountId, CancellationToken cancellationToken = default);
}
