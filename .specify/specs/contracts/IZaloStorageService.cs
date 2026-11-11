// --------------------------------------------------------------------------------------------------------------------
// <copyright file="IZaloStorageService.cs" company="Zenix">
//   Copyright (c) Zenix. All rights reserved.
// </copyright>
// --------------------------------------------------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Zenix.Core.Zalo.Models;

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
}
