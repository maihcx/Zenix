// --------------------------------------------------------------------------------------------------------------------
// <copyright file="IZaloMessageService.cs" company="Zenix">
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
/// Encapsulates event data for newly received or updated chat messages.
/// </summary>
/// <param name="Message">The incoming or modified chat message entity.</param>
public record MessageReceivedEventArgs(ChatMessage Message);

/// <summary>
/// Encapsulates event data for delivery status transitions (e.g., Sending -> Sent -> Delivered -> Seen).
/// </summary>
/// <param name="CliMsgId">The local client message identifier.</param>
/// <param name="ServerMsgId">The server-assigned message identifier, if confirmed.</param>
/// <param name="NewStatus">The updated delivery status.</param>
public record MessageStatusChangedEventArgs(
    string CliMsgId,
    string? ServerMsgId,
    MessageDeliveryStatus NewStatus
);

/// <summary>
/// Encapsulates event data when a message is recalled by the sender.
/// </summary>
/// <param name="ConversationId">The conversation containing the recalled message.</param>
/// <param name="ServerMsgId">The server message identifier that was recalled.</param>
/// <param name="RecalledBy">The user display name who recalled the message.</param>
public record MessageRecalledEventArgs(
    string ConversationId,
    string ServerMsgId,
    string RecalledBy
);

/// <summary>
/// Defines high-level messaging operations for conversations, including sending text/media messages,
/// handling mentions, drafts, reactions, recall, and soft-delete undo timers.
/// </summary>
public interface IZaloMessageService
{
    /// <summary>
    /// Occurs when a new message is received from a peer or synchronized from another device.
    /// </summary>
    event EventHandler<MessageReceivedEventArgs>? MessageReceived;

    /// <summary>
    /// Occurs when an outgoing message's delivery status transitions.
    /// </summary>
    event EventHandler<MessageStatusChangedEventArgs>? MessageStatusChanged;

    /// <summary>
    /// Occurs when an existing message in a conversation has been recalled by its author.
    /// </summary>
    event EventHandler<MessageRecalledEventArgs>? MessageRecalled;

    /// <summary>
    /// Sends a text message to a conversation with optional mention spans and quote context.
    /// Creates an initial local message in <see cref="MessageDeliveryStatus.Sending"/> status before network dispatch.
    /// </summary>
    /// <param name="conversationId">The target conversation identifier.</param>
    /// <param name="text">The plain text content to send.</param>
    /// <param name="mentions">Optional mention spans embedded in the message.</param>
    /// <param name="quote">Optional quoted message context being replied to.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task resolving to the created chat message entity.</returns>
    Task<ChatMessage> SendTextMessageAsync(
        string conversationId, 
        string text, 
        IReadOnlyList<MentionSpan>? mentions = null, 
        QuotedMessage? quote = null, 
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends a rich media or attachment payload to a conversation.
    /// </summary>
    /// <param name="conversationId">The target conversation identifier.</param>
    /// <param name="payload">The polymorphic payload representing photo, video, audio, or document.</param>
    /// <param name="quote">Optional quoted message context.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task resolving to the created chat message entity.</returns>
    Task<ChatMessage> SendMediaMessageAsync(
        string conversationId, 
        MessagePayload payload, 
        QuotedMessage? quote = null, 
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds or toggles an emoji reaction on a specific message.
    /// </summary>
    /// <param name="conversationId">The conversation identifier.</param>
    /// <param name="serverMsgId">The server message identifier.</param>
    /// <param name="emoji">The emoji character or icon code.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task representing the asynchronous reaction update.</returns>
    Task AddReactionAsync(
        string conversationId, 
        string serverMsgId, 
        string emoji, 
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Recalls a previously sent message so that it is replaced with a recalled placeholder for all participants.
    /// </summary>
    /// <param name="conversationId">The conversation identifier.</param>
    /// <param name="serverMsgId">The server message identifier to recall.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task representing the asynchronous recall operation.</returns>
    Task RecallMessageAsync(
        string conversationId, 
        string serverMsgId, 
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Initiates a local message deletion with an undo countdown window (5–30 seconds) before permanent database removal.
    /// </summary>
    /// <param name="cliMsgId">The local client message identifier to delete.</param>
    /// <param name="undoWindowSeconds">The countdown duration in seconds during which the user may undo deletion.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A cancellation token source allowing the deletion to be reverted before deadline.</returns>
    Task<CancellationTokenSource> DeleteMessageWithUndoAsync(
        string cliMsgId, 
        int undoWindowSeconds = 15, 
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Loads a paginated slice of historical messages from local SQLite storage.
    /// </summary>
    /// <param name="conversationId">The target conversation identifier.</param>
    /// <param name="beforeTimestamp">Load messages chronologically prior to this timestamp, or null for newest.</param>
    /// <param name="pageSize">The maximum number of messages to return.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A list of chat messages ordered chronologically.</returns>
    Task<IReadOnlyList<ChatMessage>> GetMessageHistoryAsync(
        string conversationId, 
        DateTimeOffset? beforeTimestamp = null, 
        int pageSize = 50, 
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Persists an unsent composer draft associated with a conversation to survive app restarts.
    /// </summary>
    /// <param name="conversationId">The target conversation identifier.</param>
    /// <param name="draftText">The draft text content.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task representing the asynchronous draft save operation.</returns>
    Task SaveDraftAsync(
        string conversationId, 
        string draftText, 
        CancellationToken cancellationToken = default);
}
