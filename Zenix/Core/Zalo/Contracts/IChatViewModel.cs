// Copyright (c) Zenix. All rights reserved.
// Licensed under the proprietary and confidential license.

using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Input;
using Zenix.Core.Zalo.Models.Messages;
using Zenix.Core.Zalo.Models.Profiles;

namespace Zenix.Core.Zalo.Contracts;

/// <summary>
/// Defines the UI-agnostic ViewModel contract for the active chat viewport.
/// Strictly decoupled from UI visual elements (zero references to visual controls or window hosts).
/// All properties and commands are bound via WinUI 3 compiled bindings ({x:Bind}).
/// </summary>
public interface IChatViewModel : INotifyPropertyChanged
{
    /// <summary>
    /// Gets the current active conversation summary, or null if no conversation is selected.
    /// </summary>
    ConversationSummary? ActiveConversation { get; }

    /// <summary>
    /// Gets the observable collection of chat messages rendered in the message thread.
    /// </summary>
    ObservableCollection<ChatMessage> Messages { get; }

    /// <summary>
    /// Gets or sets the draft text currently entered in the message input box.
    /// </summary>
    string InputText { get; set; }

    /// <summary>
    /// Gets a value indicating whether message history is currently being loaded from disk or network.
    /// </summary>
    bool IsLoadingHistory { get; }

    /// <summary>
    /// Gets a value indicating whether a file upload or download is currently in flight.
    /// </summary>
    bool IsTransferringMedia { get; }

    /// <summary>
    /// Gets the current media transfer progress percentage (0.0 to 100.0).
    /// </summary>
    double TransferProgress { get; }

    /// <summary>
    /// Gets the quoted message context being replied to, or null if no reply is active.
    /// </summary>
    QuotedMessageSummary? ReplyingToMessage { get; }

    /// <summary>
    /// Gets the command that submits the current <see cref="InputText"/> as a new outgoing chat message.
    /// </summary>
    ICommand SendMessageCommand { get; }

    /// <summary>
    /// Gets the command that prompts the abstracted file picker and attaches photo or document media.
    /// </summary>
    ICommand AttachFileCommand { get; }

    /// <summary>
    /// Gets the command that sets or clears the current <see cref="ReplyingToMessage"/> reply context.
    /// </summary>
    ICommand ReplyToMessageCommand { get; }

    /// <summary>
    /// Gets the command that cancels an active reply quote.
    /// </summary>
    ICommand CancelReplyCommand { get; }

    /// <summary>
    /// Gets the command that recalls a sent message by server message identifier.
    /// </summary>
    ICommand RecallMessageCommand { get; }

    /// <summary>
    /// Gets the command that initiates a soft deletion with undo countdown for a message.
    /// </summary>
    ICommand DeleteMessageCommand { get; }

    /// <summary>
    /// Gets the command that adds or toggles an emoji reaction on a message.
    /// </summary>
    ICommand ToggleReactionCommand { get; }

    /// <summary>
    /// Selects and activates a conversation thread, loading historical messages into the view.
    /// </summary>
    /// <param name="conversation">The conversation thread to display.</param>
    void SelectConversation(ConversationSummary conversation);
}
